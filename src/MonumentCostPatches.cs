using System.Collections.Generic;

namespace InfraSpaceQoL
{
    internal static class MonumentCostPatches
    {
        public static void GeneratorPowerPostfix(string buildingName, ProductionDefinition __result)
        {
            if (buildingName == "magneticFieldGenerator_Stage4" && __result != null)
            {
                __result.powerNeeded = (float)ModSettings.Current.Terraforming.MagneticFieldGeneratorPowerMW;
            }
        }

        public static void GeneratorPreviewPostfix(BuildingDescription __instance, ProductionLogic __result)
        {
            FactoryProductionLogic factory = __result as FactoryProductionLogic;
            if (factory != null) GeneratorPowerPostfix(__instance.name, factory.productionDefinition);
        }

        public static void DescriptionInitPostfix(BuildingDescription __instance)
        {
            ApplyCost(__instance);
        }

        internal static void ApplyCost(BuildingDescription description)
        {
            if (description == null || description.name != "magneticFieldGenerator_Stage0" || description.costs == null)
            {
                return;
            }

            List<ResourceCost> adjusted = null;
            for (int i = 0; i < description.costs.Count; i++)
            {
                ResourceCost cost = description.costs[i];
                // Placement costs use hundredths; stage deliveries use whole items.
                if (cost.resourceName == "adamantine" && (cost.amount == 25000 || cost.amount == 7500))
                {
                    if (adjusted == null) adjusted = new List<ResourceCost>(description.costs);
                    cost.amount = ModSettings.Current.Terraforming.ReduceMagneticFieldGeneratorCost ? 7500 : 25000;
                    adjusted[i] = cost;
                }
            }
            if (adjusted != null) description.costs = adjusted;
        }
    }
}
