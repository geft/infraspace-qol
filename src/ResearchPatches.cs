using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace InfraSpaceQoL
{
    internal static class ResearchPatches
    {
        private const decimal VanillaMultiplier = 1m;
        private static readonly FieldInfo PendingResearchProgress = typeof(ResearchManager).GetField(
            "amountOfProgressToAdd", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CachedConsumables = typeof(ResearchProductionLogic).GetField(
            "consumables", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void RedScienceOutputPostfix(ref ProductionDefinition __result)
        {
            if (__result == null || __result.producables == null)
            {
                return;
            }
            ProductionDefinition boosted = null;
            foreach (ResourceCost output in __result.producables)
            {
                if (output.amount > 0 && IsRedScienceResourceName(output.resourceName))
                {
                    // Copy the manager's cached recipe so repeated lookups and
                    // factory upgrades cannot compound the output bonus.
                    if (boosted == null)
                    {
                        boosted = new ProductionDefinition(__result);
                    }
                    int extra = (int)Math.Min(output.amount, int.MaxValue - (long)output.amount);
                    boosted.Boost("InfraSpace QoL", output.resourceName, extra);
                }
            }
            if (boosted != null)
            {
                __result = boosted;
            }
        }
        public static bool ResearchUpdatePrefix(ResearchProductionLogic __instance)
        {
            decimal multiplier = ModSettings.Current.Research.RedScienceCostMultiplier;
            if (multiplier >= VanillaMultiplier || __instance == null || __instance.consumerProducer == null)
            {
                return true;
            }

            ResearchManager manager = WorldScripts.Inst.researchManager;
            lock (manager)
            {
                TechnologyDescription technology = manager.GetCurrentResearchDescription();
                Resource redResource = FindRedScienceResource(technology);
                if (technology == null || redResource == null)
                {
                    return true;
                }

                int committedProgress;
                if (!manager.researchProgress.TryGetValue(manager.GetCurrentResearch(), out committedProgress))
                {
                    return false;
                }

                int queuedProgress = (int)PendingResearchProgress.GetValue(manager);
                // UpdateFrameUnity applies queued points later. Include them so multiple lab ticks
                // in one frame cannot all make the same Red Science consumption decision.
                long currentProgress = (long)committedProgress + queuedProgress;
                if (currentProgress >= technology.cost)
                {
                    return false;
                }

                // Track cumulative pack cost; each point consumes only the increase in ceil(progress × rate).
                // Decimal arithmetic avoids rounding values such as 7.000000... up to an extra pack.
                long redBefore = GetRedScienceCost(currentProgress, multiplier);
                long redAfter = GetRedScienceCost(currentProgress + 1L, multiplier);
                bool consumeRed = redAfter > redBefore;
                ConsumerProducer consumer = __instance.consumerProducer;
                int[] storage = consumer.incomingStorage;
                Resource[] inputs = consumer.incomingResources;
                if (storage == null || inputs == null)
                {
                    return true;
                }

                int count = Math.Min(storage.Length, inputs.Length);
                // Other science inputs remain one-per-point on mixed-science technologies.
                for (int i = 0; i < count; i++)
                {
                    if ((inputs[i] != redResource || consumeRed) && storage[i] <= 0)
                    {
                        return false;
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    if (inputs[i] != redResource || consumeRed)
                    {
                        storage[i]--;
                    }
                }

                manager.AddResearchThread(1);
                return false;
            }
        }

        internal static long GetRedScienceCost(long progress, decimal multiplier)
        {
            return (long)Math.Ceiling(progress * multiplier);
        }

        public static void ResearchManagerLockPrefix(ResearchManager __instance, out ResearchManager __state)
        {
            __state = __instance;
            // The simulation and Unity threads both access progress and current research.
            Monitor.Enter(__instance);
        }

        public static Exception ResearchManagerLockFinalizer(Exception __exception, ResearchManager __state)
        {
            if (__state != null)
            {
                Monitor.Exit(__state);
            }
            return __exception;
        }

        public static void ZeroMultiplierConsumablesPostfix(ResearchProductionLogic __instance, List<ResourceCost> consumablesList)
        {
            if (__instance == null || consumablesList == null || ModSettings.Current.Research.RedScienceCostMultiplier != 0m)
            {
                return;
            }

            ResearchManager manager = WorldScripts.Inst.researchManager;
            lock (manager)
            {
                TechnologyDescription technology = manager.GetCurrentResearchDescription();
                if (technology == null || technology.requiredResources == null)
                {
                    return;
                }

                List<Resource> redResources = new List<Resource>();
                // Both lists matter: the game uses the caller list to create storage slots and may
                // later reuse the cached list when rebuilding the lab's requested resources.
                foreach (string name in technology.requiredResources.Where(IsRedScienceResourceName))
                {
                    try
                    {
                        Resource resource = Resource.Get(name);
                        if (resource != null && string.Equals(resource.name, name, StringComparison.Ordinal) && !redResources.Contains(resource))
                        {
                            redResources.Add(resource);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("[InfraSpaceQoL] Could not resolve Red Science resource '" + name + "': " + exception.Message);
                    }
                }

                for (int i = consumablesList.Count - 1; i >= 0; i--)
                {
                    if (redResources.Contains(consumablesList[i].resource))
                    {
                        consumablesList.RemoveAt(i);
                    }
                }

                List<ResourceCost> cached = CachedConsumables.GetValue(__instance) as List<ResourceCost>;
                if (cached != null)
                {
                    for (int i = cached.Count - 1; i >= 0; i--)
                    {
                        if (redResources.Contains(cached[i].resource))
                        {
                            cached.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private static Resource FindRedScienceResource(TechnologyDescription technology)
        {
            if (technology == null || technology.requiredResources == null)
            {
                return null;
            }

            foreach (string name in technology.requiredResources)
            {
                if (!IsRedScienceResourceName(name))
                {
                    continue;
                }

                try
                {
                    Resource resource = Resource.Get(name);
                    if (resource != null && string.Equals(resource.name, name, StringComparison.Ordinal))
                    {
                        return resource;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[InfraSpaceQoL] Could not resolve Red Science resource '" + name + "': " + exception.Message);
                }
            }

            return null;
        }

        private static bool IsRedScienceResourceName(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return false;
            }

            return string.Equals(resourceName, "sciencePack4", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(resourceName, "redSciencePack", StringComparison.OrdinalIgnoreCase);
        }
    }
}
