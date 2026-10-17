using System;
using System.Collections.Generic;

namespace InfraSpaceQoL
{
    internal static class MonumentCostPatches
    {
        public static void NewProductionLogicPostfix(BuildingDescription __instance, ProductionLogic __result)
        {
            ApplyCost(__instance.name, __result as UpgradeProductionLogic);
        }

        public static void UpgradeInitPrefix(UpgradeProductionLogic __instance, ConsumerProducer consumerProducer)
        {
            // Saved construction stages retain their own serialized costs.
            ApplyCost(consumerProducer.building.buildingName, __instance);
        }

        internal static void ApplyCost(string buildingName, UpgradeProductionLogic upgrade)
        {
            if (buildingName == null || !buildingName.StartsWith("magneticFieldGenerator_Stage", StringComparison.Ordinal) ||
                upgrade == null || upgrade.costs == null)
            {
                return;
            }

            // Copy the list so a shared construction recipe cannot be changed.
            List<ResourceCost> adjusted = null;
            for (int i = 0; i < upgrade.costs.Count; i++)
            {
                ResourceCost cost = upgrade.costs[i];
                if (cost.resourceName == "adamantine" && cost.amount == 250)
                {
                    if (adjusted == null) adjusted = new List<ResourceCost>(upgrade.costs);
                    cost.amount = 75;
                    adjusted[i] = cost;
                }
            }
            if (adjusted != null) upgrade.costs = adjusted;
        }
    }
}
