using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace InfraSpaceQoL
{
    internal static class LogisticsPatches
    {
        private sealed class PendingRequest
        {
            internal ConsumerProducer Consumer;
            internal Resource Resource;
            internal float Priority;
        }

        private sealed class Candidate
        {
            internal CollectionProductionLogic Center;
            internal List<PathPart> Path;
            internal long RouteDistance;
        }

        private static readonly object RequestGate = new object();
        private static readonly object DispatchGate = new object();
        private static readonly ConditionalWeakTable<CollectionProductionLogic, object> InventoryGates =
            new ConditionalWeakTable<CollectionProductionLogic, object>();
        private static readonly Dictionary<ConsumerProducer, Dictionary<Resource, float>> PendingRequests =
            new Dictionary<ConsumerProducer, Dictionary<Resource, float>>();
        private static readonly FieldInfo RequestStatusDirty = typeof(ConsumerProducer).GetField(
            "requestStatusDirty", BindingFlags.Instance | BindingFlags.NonPublic);
        private static int hasPendingRequests;
        private static int firstShipmentLogged;

        internal static bool ShouldRefreshRequests(ConsumerProducer consumer)
        {
            if (!ModSettings.Current.Logistics.DirectCollectionSupply || consumer == null || consumer.building == null)
            {
                return false;
            }

            long frame = Old.GetSimulator().GetSimulationFrame();
            if ((frame + consumer.building.GetID()) % ConsumerProducer.requestUpdateFrames == 0)
            {
                return true;
            }

            return RequestStatusDirty != null && (bool)RequestStatusDirty.GetValue(consumer);
        }

        internal static void RefreshRequests(ConsumerProducer consumer)
        {
            // Mirror only factory demand the game can transport by truck. Replacing each snapshot
            // also clears stale requests when the factory fills, changes inputs, or becomes invalid.
            Dictionary<Resource, float> activeRequests = new Dictionary<Resource, float>();
            try
            {
                if (consumer != null && consumer.IsValid() && consumer.building.IsValidAndBuilt() &&
                    consumer.productionLogic is FactoryProductionLogic && consumer.building.HasAnyIncomingLane())
                {
                    List<ResourceCost> consumables = consumer.GetConsumables(verify: false);
                    FactoryProductionLogic factory = (FactoryProductionLogic)consumer.productionLogic;
                    int count = Math.Min(consumables.Count, consumer.incomingStorage.Length);
                    for (int i = 0; i < count; i++)
                    {
                        ResourceCost consumable = consumables[i];
                        Resource resource = consumable.resource;
                        if (resource == null || consumer.building.GetPipeComponentForResource(resource) != null)
                        {
                            continue;
                        }

                        int orderAmount = ConsumerProducer.GetOrderAmount(resource);
                        int outstanding = consumer.GetIncomingCarsResourceAmount(resource);
                        int remaining = consumable.amount - consumer.incomingStorage[i] - outstanding;
                        if (orderAmount <= 0 || remaining < orderAmount)
                        {
                            continue;
                        }

                        float priority = !factory.IncomingStorageFulfilled()
                            ? Helper.ProjectClamp(remaining, 0, consumable.amount, 0.5f, 1f)
                            : Helper.ProjectClamp(remaining, 0, consumable.amount, 0f, 0.5f);
                        if (priority > 0f)
                        {
                            activeRequests[resource] = priority;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[InfraSpaceQoL] Could not refresh direct-supply demand: " + exception.Message);
            }

            lock (RequestGate)
            {
                if (activeRequests.Count == 0)
                {
                    PendingRequests.Remove(consumer);
                }
                else
                {
                    PendingRequests[consumer] = activeRequests;
                }
                Interlocked.Exchange(ref hasPendingRequests, 1);
            }
        }

        internal static void RemoveConsumer(ConsumerProducer consumer)
        {
            if (consumer == null)
            {
                return;
            }

            lock (RequestGate)
            {
                PendingRequests.Remove(consumer);
                Interlocked.Exchange(ref hasPendingRequests, 1);
            }
        }

        internal static object GetInventoryGate(CollectionProductionLogic center)
        {
            return InventoryGates.GetValue(center, delegate { return new object(); });
        }

        private static List<PendingRequest> DrainRequests()
        {
            List<PendingRequest> snapshot = new List<PendingRequest>();
            lock (RequestGate)
            {
                foreach (KeyValuePair<ConsumerProducer, Dictionary<Resource, float>> consumerRequest in PendingRequests)
                {
                    foreach (KeyValuePair<Resource, float> resourceRequest in consumerRequest.Value)
                    {
                        snapshot.Add(new PendingRequest
                        {
                            Consumer = consumerRequest.Key,
                            Resource = resourceRequest.Key,
                            Priority = resourceRequest.Value
                        });
                    }
                }
                PendingRequests.Clear();
                Interlocked.Exchange(ref hasPendingRequests, 0);
            }

            return snapshot;
        }

        public static void RequestPrefix(ConsumerProducer __instance, out bool __state)
        {
            __state = ShouldRefreshRequests(__instance);
        }

        public static void RequestPostfix(ConsumerProducer __instance, bool __state)
        {
            if (__state)
            {
                RefreshRequests(__instance);
            }
        }

        public static void ConsumerDestroyedPostfix(ConsumerProducer __instance)
        {
            RemoveConsumer(__instance);
        }

        public static void OrderManagerPostfix()
        {
            if (!ModSettings.Current.Logistics.DirectCollectionSupply || Interlocked.CompareExchange(ref hasPendingRequests, 0, 0) == 0)
            {
                return;
            }

            lock (DispatchGate)
            {
                List<PendingRequest> requests = DrainRequests();
                if (requests.Count == 0)
                {
                    return;
                }

                requests = requests
                    .OrderByDescending(request => request.Priority)
                    .ThenBy(request => request.Consumer != null && request.Consumer.building != null ? request.Consumer.building.GetID() : long.MaxValue)
                    .ThenBy(request => request.Resource == null ? string.Empty : request.Resource.name, StringComparer.Ordinal)
                    .ToList();

                // This postfix runs after native supplier suggestions are dispatched. Build one
                // resource index for the batch instead of rescanning all centers per request.
                Dictionary<Resource, List<CollectionProductionLogic>> centersByResource = GroupCollectionCenters();
                foreach (PendingRequest request in requests)
                {
                    try
                    {
                        TryDispatch(request, centersByResource);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("[InfraSpaceQoL] Direct-supply dispatch failed: " + exception.Message);
                    }
                }
            }
        }

        private static Dictionary<Resource, List<CollectionProductionLogic>> GroupCollectionCenters()
        {
            var groups = new Dictionary<Resource, List<CollectionProductionLogic>>();
            foreach (CollectionProductionLogic center in WorldScripts.Inst.registry.collectionCenters)
            {
                if (center == null || center.resource == null)
                {
                    continue;
                }
                List<CollectionProductionLogic> centers;
                if (!groups.TryGetValue(center.resource, out centers))
                {
                    centers = new List<CollectionProductionLogic>();
                    groups.Add(center.resource, centers);
                }
                centers.Add(center);
            }
            return groups;
        }

        private static void TryDispatch(PendingRequest request,
            Dictionary<Resource, List<CollectionProductionLogic>> centersByResource)
        {
            ConsumerProducer destination = request.Consumer;
            Resource resource = request.Resource;
            if (destination == null || resource == null || !destination.IsValid() ||
                !destination.building.IsValidAndBuilt() || !(destination.productionLogic is FactoryProductionLogic))
            {
                return;
            }

            int orderAmount;
            float currentPriority;
            if (!TryGetCurrentDemand(destination, resource, out orderAmount, out currentPriority) || currentPriority <= 0f)
            {
                return;
            }

            List<CollectionProductionLogic> centers;
            if (!centersByResource.TryGetValue(resource, out centers))
            {
                return;
            }
            List<Candidate> candidates = new List<Candidate>();
            // Preserve native district export rules and road routing. Require a full order, then
            // try eligible centers from shortest route to longest.
            foreach (CollectionProductionLogic center in centers)
            {
                if (center == null || center.resource != resource || center.consumerProducer == null ||
                    !center.consumerProducer.building.IsValidAndBuilt() || center.GetStorage() < orderAmount)
                {
                    continue;
                }

                if (!DistrictCanSupply(center, destination.building, resource))
                {
                    continue;
                }

                List<PathPart> path = PathFinderFacadeRoads.GetPath(center.consumerProducer.building, destination.building);
                if (path == null || path.Count == 0 || !PathFinder.VerifyPath(path))
                {
                    continue;
                }

                long distance = 0L;
                bool validPath = true;
                foreach (PathPart part in path)
                {
                    if (part == null || !part.IsValid() || !part.IsBuilt())
                    {
                        validPath = false;
                        break;
                    }
                    distance += Math.Max(0, part.GetPathDist());
                }
                if (!validPath)
                {
                    continue;
                }

                candidates.Add(new Candidate { Center = center, Path = path, RouteDistance = distance });
            }

            candidates = candidates
                .OrderBy(candidate => candidate.RouteDistance)
                .ThenBy(candidate => candidate.Center.consumerProducer.building.GetID())
                .ToList();

            foreach (Candidate candidate in candidates)
            {
                if (TryDispatchFromCenter(candidate, destination, resource, orderAmount))
                {
                    return;
                }
            }
        }

        private static bool DistrictCanSupply(CollectionProductionLogic center, Building destination, Resource resource)
        {
            DistrictsManager districts = WorldScripts.Inst.districtsManager;
            byte originDistrict = districts.GetDistrict(center.consumerProducer.building.GetPosition());
            byte targetDistrict = districts.GetDistrict(destination.GetPosition());
            List<byte> allowedTargets = new List<byte>();
            districts.GetAllowedDistrictTargets(resource, originDistrict, allowedTargets);
            return allowedTargets.Count == 0 || allowedTargets.Contains(targetDistrict);
        }

        private static bool TryGetCurrentDemand(ConsumerProducer consumer, Resource resource, out int orderAmount, out float priority)
        {
            orderAmount = ConsumerProducer.GetOrderAmount(resource);
            priority = 0f;
            if (orderAmount <= 0 || consumer == null || consumer.building == null ||
                !consumer.building.HasAnyIncomingLane() || consumer.building.GetPipeComponentForResource(resource) != null)
            {
                return false;
            }

            List<ResourceCost> consumables = consumer.GetConsumables(verify: false);
            int index = -1;
            for (int i = 0; i < consumables.Count; i++)
            {
                if (consumables[i].resource == resource)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0 || index >= consumer.incomingStorage.Length)
            {
                return false;
            }

            ResourceCost cost = consumables[index];
            int remaining = cost.amount - consumer.incomingStorage[index] - consumer.GetIncomingCarsResourceAmount(resource);
            if (remaining < orderAmount)
            {
                return false;
            }

            FactoryProductionLogic factory = consumer.productionLogic as FactoryProductionLogic;
            priority = !factory.IncomingStorageFulfilled()
                ? Helper.ProjectClamp(remaining, 0, cost.amount, 0.5f, 1f)
                : Helper.ProjectClamp(remaining, 0, cost.amount, 0f, 0.5f);
            return priority > 0f;
        }

        private static bool TryDispatchFromCenter(Candidate candidate, ConsumerProducer destination, Resource resource, int orderAmount)
        {
            CollectionProductionLogic center = candidate.Center;
            ConsumerProducer source = center.consumerProducer;
            object inventoryGate = GetInventoryGate(center);
            lock (inventoryGate)
            {
                // Collection dispatch runs on other simulation workers. Serialize source stock
                // and destination reservations while checking capacity and registering the car.
                lock (destination.incomingCars)
                {
                    int currentOrderAmount;
                    float currentPriority;
                    if (!TryGetCurrentDemand(destination, resource, out currentOrderAmount, out currentPriority) ||
                        currentPriority <= 0f || currentOrderAmount != orderAmount)
                    {
                        return false;
                    }

                    int storageIndex = source.GetConsumptionListIndex(resource);
                    if (storageIndex < 0 || storageIndex >= source.incomingStorage.Length || source.incomingStorage[storageIndex] < orderAmount)
                    {
                        return false;
                    }

                    source.incomingStorage[storageIndex] -= orderAmount;
                    Car car = null;
                    try
                    {
                        car = new Car(resource, orderAmount, source.building, destination.building, candidate.Path);
                        if (!source.building.TryPutCarOnPath(car, ensureSpace: true))
                        {
                            car.Destroy();
                            // Car.Destroy queues native cleanup. Release its logical reservation now
                            // so the next center can try to fill the same demand.
                            destination.RemoveIncomingCar(car);
                            source.incomingStorage[storageIndex] += orderAmount;
                            return false;
                        }

                        WorldScripts.Inst.orderManager.lastDeliveryScheduleFrames[destination] = Old.GetSimulator().GetSimulationFrame();
                        if (Interlocked.CompareExchange(ref firstShipmentLogged, 1, 0) == 0)
                        {
                            Debug.Log("[InfraSpaceQoL] First direct truck dispatched: " + orderAmount + " " + resource.name +
                                      " from " + source.building.buildingName + " to " + destination.building.buildingName + ".");
                        }
                        return true;
                    }
                    catch
                    {
                        if (car != null)
                        {
                            car.Destroy();
                            destination.RemoveIncomingCar(car);
                        }
                        source.incomingStorage[storageIndex] += orderAmount;
                        throw;
                    }
                }
            }
        }

        public static void CollectionDispatchPrefix(CollectionProductionLogic __instance, out object __state)
        {
            __state = null;
            if (ModSettings.Current.Logistics.DirectCollectionSupply && __instance != null)
            {
                object gate = GetInventoryGate(__instance);
                Monitor.Enter(gate);
                __state = gate;
            }
        }

        public static Exception CollectionDispatchFinalizer(Exception __exception, object __state)
        {
            if (__state != null)
            {
                Monitor.Exit(__state);
            }
            return __exception;
        }

        public static bool DistributionDispatchPrefix(DistributionProductionLogic __instance, ConsumerProducer dest,
            int orderAmount, ref bool __result, out HashSet<Car> __state)
        {
            __state = null;
            if (!ModSettings.Current.Logistics.DirectCollectionSupply || __instance == null || dest == null || dest.incomingCars == null)
            {
                return true;
            }

            HashSet<Car> gate = dest.incomingCars;
            Monitor.Enter(gate);
            __state = gate;
            // Direct and distribution deliveries share input capacity. Hold the native incoming
            // car lock through dispatch to prevent both suppliers reserving the same free space.
            if (!HasRoomForDelivery(dest, __instance.resource, orderAmount))
            {
                Monitor.Exit(gate);
                __state = null;
                __result = false;
                return false;
            }

            return true;
        }

        public static Exception DistributionDispatchFinalizer(Exception __exception, HashSet<Car> __state)
        {
            if (__state != null)
            {
                Monitor.Exit(__state);
            }
            return __exception;
        }

        private static bool HasRoomForDelivery(ConsumerProducer consumer, Resource resource, int amount)
        {
            if (resource == null || amount <= 0)
            {
                return false;
            }

            List<ResourceCost> consumables = consumer.GetConsumables(verify: false);
            // Count arrived stock and registered cars against the consumer's input limit.
            for (int i = 0; i < consumables.Count && i < consumer.incomingStorage.Length; i++)
            {
                if (consumables[i].resource == resource)
                {
                    int committed = consumer.incomingStorage[i] + consumer.GetIncomingCarsResourceAmount(resource);
                    return consumables[i].amount - committed >= amount;
                }
            }
            return false;
        }
    }
}
