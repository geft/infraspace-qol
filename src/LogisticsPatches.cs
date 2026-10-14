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
            internal int DestinationPriority;
        }

        private sealed class Candidate
        {
            internal CollectionProductionLogic Center;
            internal List<PathPart> Path;
            internal long RouteDistance;
        }

        internal sealed class StorehouseRoute
        {
            internal ConsumerProducer Consumer;
            internal long Distance;
            internal long BuildingId;
        }

        internal static StorehouseRoute SelectNearestStorehouseRoute(IEnumerable<StorehouseRoute> routes)
        {
            return routes.OrderBy(route => route.Distance).ThenBy(route => route.BuildingId).FirstOrDefault();
        }

        private static ConsumerProducer FindNearestStorehouse(Building source, Resource resource,
            IEnumerable<ConsumerProducer> consumers)
        {
            var routes = new List<StorehouseRoute>();
            foreach (ConsumerProducer consumer in consumers)
            {
                int amount;
                float priority;
                if (consumer == null || !(consumer.productionLogic is StorehouseProductionLogic) ||
                    consumer.building == null || !consumer.IsValid() || !consumer.building.IsValidAndBuilt() ||
                    !TryGetCurrentDemand(consumer, resource, out amount, out priority) ||
                    !DistrictCanSupply(source, consumer.building, resource))
                {
                    continue;
                }
                long distance;
                if (TryGetRouteDistance(PathFinderFacadeRoads.GetPath(source, consumer.building), out distance))
                {
                    routes.Add(new StorehouseRoute { Consumer = consumer, Distance = distance,
                        BuildingId = consumer.building.GetID() });
                }
            }
            StorehouseRoute nearest = SelectNearestStorehouseRoute(routes);
            return nearest == null ? null : nearest.Consumer;
        }

        private static bool TryGetRouteDistance(List<PathPart> path, out long distance)
        {
            distance = 0L;
            if (path == null || path.Count == 0 || !PathFinder.VerifyPath(path))
            {
                return false;
            }
            foreach (PathPart part in path)
            {
                if (part == null || !part.IsValid() || !part.IsBuilt())
                {
                    return false;
                }
                distance += Math.Max(0, part.GetPathDist());
            }
            return true;
        }

        public static void InterestedConsumersPostfix(Resource resource,
            List<Pair<ConsumerProducer, float>> consumers, ConsumerProducer producer)
        {
            if (!ModSettings.Current.Logistics.DirectCollectionSupply || producer == null || producer.building == null ||
                !consumers.Any(item => item.first.productionLogic is StorehouseProductionLogic))
            {
                return;
            }
            // Keep native request radius and supplier eligibility; only choose between the
            // Storehouses the game already offered to this producer.
            ConsumerProducer nearest = FindNearestStorehouse(producer.building, resource,
                consumers.Where(item => item.second > 0f).Select(item => item.first));
            FilterStorehouseConsumers(consumers, nearest);
        }

        internal static void FilterStorehouseConsumers(List<Pair<ConsumerProducer, float>> consumers,
            ConsumerProducer nearest)
        {
            consumers.RemoveAll(item => item.first.productionLogic is StorehouseProductionLogic && item.first != nearest);
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
        private const float StorehouseRequestPriority = 0.25f;

        internal static bool CanCollectFromProducer(CollectionProductionLogic center, ConsumerProducer producer)
        {
            if (center == null || center.resource == null || center.consumerProducer == null ||
                center.consumerProducer.building == null || producer == null || producer.building == null ||
                !center.consumerProducer.building.IsValidAndBuilt() || !producer.building.IsValidAndBuilt() ||
                producer.outgoingResources == null || !producer.outgoingResources.Contains(center.resource) ||
                float.IsNaN(center.collectionRadius) || float.IsInfinity(center.collectionRadius) ||
                center.collectionRadius < 0f)
            {
                return false;
            }

            // Match the game's connection lookup: horizontal distance between building
            // positions, rather than truck route length or height on the terrain.
            Vector3 offset = producer.building.GetPosition() - center.consumerProducer.building.GetPosition();
            return offset.x * offset.x + offset.z * offset.z <= center.collectionRadius * center.collectionRadius;
        }

        internal static void PruneCollectionConnections(ConsumerProducer producer)
        {
            if (producer == null || producer.connectedCollectionCenters == null)
            {
                return;
            }
            foreach (KeyValuePair<Resource, List<CollectionProductionLogic>> connection in producer.connectedCollectionCenters)
            {
                connection.Value.RemoveAll(center =>
                {
                    if (center != null && center.resource == connection.Key && CanCollectFromProducer(center, producer))
                    {
                        return false;
                    }
                    if (center != null && center.connectedProducers != null &&
                        !CanCollectFromProducer(center, producer))
                    {
                        center.connectedProducers.Remove(producer);
                    }
                    return true;
                });
            }
        }

        public static void ProducerOutgoingPrefix(ConsumerProducer __instance)
        {
            if (__instance == null || __instance.building == null ||
                (Old.GetSimulator().GetSimulationFrame() + __instance.building.GetID()) % 50 != 0)
            {
                return;
            }
            // Empty stale lists before native dispatch chooses its Collection Center branch,
            // allowing ordinary delivery fallback when no center remains in range.
            PruneCollectionConnections(__instance);
        }

        public static bool CollectionReceivePrefix(CollectionProductionLogic __instance,
            ConsumerProducer producer, int amount, ref bool __result)
        {
            if (amount <= 0 || !CanCollectFromProducer(__instance, producer))
            {
                __result = false;
                return false;
            }
            return true;
        }

        internal static bool IsSupportedDestination(ConsumerProducer consumer)
        {
            return consumer != null &&
                (consumer.productionLogic is FactoryProductionLogic || consumer.productionLogic is StorehouseProductionLogic);
        }

        internal static int GetDestinationPriority(ConsumerProducer consumer)
        {
            return consumer != null && consumer.productionLogic is FactoryProductionLogic ? 0 : 1;
        }

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
            // Mirror factory and Storehouse demand the game can transport by truck. Replacing each
            // snapshot also clears stale requests when inputs change or the destination becomes invalid.
            Dictionary<Resource, float> activeRequests = new Dictionary<Resource, float>();
            try
            {
                if (consumer != null && consumer.IsValid() && consumer.building.IsValidAndBuilt() &&
                    IsSupportedDestination(consumer) && consumer.building.HasAnyIncomingLane())
                {
                    List<ResourceCost> consumables = consumer.GetConsumables(verify: false);
                    FactoryProductionLogic factory = consumer.productionLogic as FactoryProductionLogic;
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

                        float priority = factory == null
                            ? StorehouseRequestPriority
                            : (!factory.IncomingStorageFulfilled()
                                ? Helper.ProjectClamp(remaining, 0, consumable.amount, 0.5f, 1f)
                                : Helper.ProjectClamp(remaining, 0, consumable.amount, 0f, 0.5f));
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
                            Priority = resourceRequest.Value,
                            DestinationPriority = GetDestinationPriority(consumerRequest.Key)
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
                    .OrderBy(request => request.DestinationPriority)
                    .ThenByDescending(request => request.Priority)
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
            if (!IsSupportedDestination(destination) || resource == null || !destination.IsValid() ||
                !destination.building.IsValidAndBuilt())
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

                if (destination.productionLogic is StorehouseProductionLogic)
                {
                    HashSet<ConsumerProducer> consumers;
                    if (!WorldScripts.Inst.registry.consumers.TryGetValue(resource, out consumers) ||
                        FindNearestStorehouse(center.consumerProducer.building, resource, consumers) != destination)
                    {
                        continue;
                    }
                }

                List<PathPart> path = PathFinderFacadeRoads.GetPath(center.consumerProducer.building, destination.building);
                long distance;
                if (!TryGetRouteDistance(path, out distance))
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
            return DistrictCanSupply(center.consumerProducer.building, destination, resource);
        }

        private static bool DistrictCanSupply(Building source, Building destination, Resource resource)
        {
            DistrictsManager districts = WorldScripts.Inst.districtsManager;
            byte originDistrict = districts.GetDistrict(source.GetPosition());
            byte targetDistrict = districts.GetDistrict(destination.GetPosition());
            List<byte> allowedTargets = new List<byte>();
            districts.GetAllowedDistrictTargets(resource, originDistrict, allowedTargets);
            return allowedTargets.Count == 0 || allowedTargets.Contains(targetDistrict);
        }

        private static bool TryGetCurrentDemand(ConsumerProducer consumer, Resource resource, out int orderAmount, out float priority)
        {
            orderAmount = ConsumerProducer.GetOrderAmount(resource);
            priority = 0f;
            if (orderAmount <= 0 || !IsSupportedDestination(consumer) || consumer.building == null ||
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
            priority = factory == null
                ? StorehouseRequestPriority
                : (!factory.IncomingStorageFulfilled()
                    ? Helper.ProjectClamp(remaining, 0, cost.amount, 0.5f, 1f)
                    : Helper.ProjectClamp(remaining, 0, cost.amount, 0f, 0.5f));
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

            // Check before taking the destination lock: comparing other Storehouses while
            // holding it could deadlock with a worker dispatching to a different destination.
            if (dest.productionLogic is StorehouseProductionLogic &&
                FindNearestStorehouse(__instance.consumerProducer.building, __instance.resource,
                    __instance.connectedConsumers) != dest)
            {
                __result = false;
                return false;
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
