using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace InfraSpaceQoL
{
    [Serializable]
    public sealed class InfraSpaceQoLMod : Mod
    {
        private static bool patchesApplied;

        public override void Load()
        {
            if (patchesApplied)
            {
                return;
            }

            ModSettings.Load();
            var patches = new List<PatchPlan>();
            Plan(patches, typeof(BuildingDescription), "Init", typeof(MonumentCostPatches),
                null, "DescriptionInitPostfix", null);
            if (ModSettings.Current.Laser.SpeedMultiplier > 1m)
            {
                Plan(patches, typeof(UpgradeManager), "GetBonus", typeof(LaserPatches),
                    null, "LaserSpeedBonusPostfix", null);
            }
            if (ModSettings.Current.Laser.AutoClearObstacles)
            {
                Plan(patches, typeof(LaserCannonSpaceshipPart), "CustomUpdate", typeof(LaserPatches),
                    "LaserUpdatePrefix", null, null);
            }
            if (ModSettings.Current.Research.DoubleRedScienceOutput)
            {
                Plan(patches, typeof(ProductionDefinitionManager), "GetProductionDefinition", typeof(ResearchPatches),
                    null, "RedScienceOutputPostfix", null);
            }
            if (ModSettings.Current.Terraforming.ExpandWhenLocalAreaSaturated)
            {
                RequireField(typeof(TerraformBuildingsManager), "terraformers", typeof(Dictionary<long, Terraformer>));
                RequireField(typeof(TerraformBuildingsManager), "progress", typeof(Unity.Collections.NativeArray<float>));
                RequireField(typeof(TerraformBuildingsManager), "progressDim", typeof(Unity.Mathematics.int2));
                RequireField(typeof(TerraformBuildingsManager), "mapCorner", typeof(float));
                RequireField(typeof(TerraformBuildingsManager), "mapSize", typeof(float));
                RequireField(typeof(TerraformBuildingsManager), "worldSize", typeof(float));
                RequireField(typeof(TerraformBuildingsManager), "jobSequentialScheduler", typeof(JobSequentialScheduler));
                Plan(patches, typeof(TerraformBuildingsManager), "Terraform", typeof(TerraformPatches),
                    "TerraformPrefix", null, "TerraformFinalizer");
            }
            if (ModSettings.Current.Workforce.ReleaseWorkersWhenAllOutputsFull)
            {
                RequireField(typeof(Market), "industries", typeof(Dictionary<Resource, List<FactoryProductionLogic>>));
                RequireField(typeof(Market), "industriesMinHeap", typeof(Dictionary<Resource, Heap<FactoryProductionLogic>>));
                RequireField(typeof(Market), "industriesMaxHeap", typeof(Dictionary<Resource, Heap<FactoryProductionLogic>>));
                RequireField(typeof(Market), "currentlyWorkingWorkersPerIndustry", typeof(Dictionary<Resource, int>));
                RequireField(typeof(Market), "unemployedWorkers", typeof(List<Worker>));
                Plan(patches, typeof(Market), "CheckEmployment", typeof(WorkforcePatches),
                    "CheckEmploymentPrefix", null, "CheckEmploymentFinalizer");
            }
            if (ModSettings.Current.Speed.ExtraSpeedControls)
            {
                Plan(patches, typeof(Dashboard), "Start", typeof(SpeedPatches), null, "DashboardStartPostfix", null);
                Plan(patches, typeof(Dashboard), "StartAfterLoad", typeof(SpeedPatches), null, "DashboardStartAfterLoadPostfix", null);
                Plan(patches, typeof(TimeManager), "ButtonSelectionChanged", typeof(SpeedPatches), "TimeButtonSelectionPrefix", null, null);
                Plan(patches, typeof(TimeManager), "SetTimeState", typeof(SpeedPatches), null, "TimeStatePostfix", null);
            }
            Plan(patches, typeof(ConsumerProducer), "UpdateOutgoingTraffic", typeof(LogisticsPatches), "ProducerOutgoingPrefix", null, null);
            Plan(patches, typeof(CollectionProductionLogic), "TryStartDeliveryToCollectionCenter", typeof(LogisticsPatches), "CollectionReceivePrefix", null, null);
            Plan(patches, typeof(OrderManager), "GetInterestedConsumers", typeof(LogisticsPatches), null, "InterestedConsumersPostfix", null);
            Plan(patches, typeof(OrderManager), "SuggestDelivery", typeof(LogisticsPatches), "DeliverySuggestionPrefix", null, null);
            Plan(patches, typeof(DistributionProductionLogic), "TrySendCar", typeof(LogisticsPatches), "DistributionDispatchPrefix", null, "DistributionDispatchFinalizer");

            if (ModSettings.Current.Logistics.DirectCollectionSupply)
            {
                Plan(patches, typeof(ConsumerProducer), "RequestResources", typeof(LogisticsPatches), "RequestPrefix", "RequestPostfix", null);
                Plan(patches, typeof(ConsumerProducer), "Destroy", typeof(LogisticsPatches), null, "ConsumerDestroyedPostfix", null);
                Plan(patches, typeof(OrderManager), "UpdateSimulationThread", typeof(LogisticsPatches), null, "OrderManagerPostfix", null);
                Plan(patches, typeof(CollectionProductionLogic), "TryStartDeliveryToDistributionCenter", typeof(LogisticsPatches), "CollectionDispatchPrefix", null, "CollectionDispatchFinalizer");
            }

            if (ModSettings.Current.Research.RedScienceCostMultiplier < 1m)
            {
                Plan(patches, typeof(ResearchProductionLogic), "UpdateSimulationThread", typeof(ResearchPatches), "ResearchUpdatePrefix", null, null);
                Plan(patches, typeof(ResearchManager), "UpdateFrameUnity", typeof(ResearchPatches), "ResearchManagerLockPrefix", null, "ResearchManagerLockFinalizer");
                Plan(patches, typeof(ResearchManager), "SetCurrentResearch", typeof(ResearchPatches), "ResearchManagerLockPrefix", null, "ResearchManagerLockFinalizer");
                if (ModSettings.Current.Research.RedScienceCostMultiplier == 0m)
                {
                    Plan(patches, typeof(ResearchProductionLogic), "AddConsumablesToList", typeof(ResearchPatches), null, "ZeroMultiplierConsumablesPostfix", null);
                }
            }

            if (ModSettings.Current.Logistics.DirectCollectionSupply)
            {
                RequireField(typeof(ConsumerProducer), "requestStatusDirty", typeof(bool));
            }
            if (ModSettings.Current.Research.RedScienceCostMultiplier < 1m)
            {
                RequireField(typeof(ResearchManager), "amountOfProgressToAdd", typeof(int));
                if (ModSettings.Current.Research.RedScienceCostMultiplier == 0m)
                {
                    RequireField(typeof(ResearchProductionLogic), "consumables", typeof(List<ResourceCost>));
                }
            }

            const string harmonyId = "DionicSoftware.InfraSpaceQoL";
            Harmony harmony = new Harmony(harmonyId);
            try
            {
                // Resolve enabled targets before this block. If Harmony rejects a patch at runtime,
                // remove only this mod's patches so unsupported game updates fail cleanly.
                foreach (PatchPlan patch in patches)
                {
                    harmony.Patch(patch.Target, patch.Prefix, patch.Postfix, null, patch.Finalizer);
                }
            }
            catch (Exception exception)
            {
                harmony.UnpatchAll(harmonyId);
                Debug.LogError("[InfraSpaceQoL] Patch installation failed; removed this mod's patches. " + exception);
                throw;
            }
            patchesApplied = true;
            Debug.Log("[InfraSpaceQoL] Loaded. Direct Collection Supply=" + ModSettings.Current.Logistics.DirectCollectionSupply +
                      ", Expand saturated terraforming=" + ModSettings.Current.Terraforming.ExpandWhenLocalAreaSaturated +
                      ", Release workers when all outputs full=" + ModSettings.Current.Workforce.ReleaseWorkersWhenAllOutputsFull +
                      ", Red Science cost multiplier=" + ModSettings.Current.Research.RedScienceCostMultiplier.ToString("0.###") +
                      ", Double Red Science output=" + ModSettings.Current.Research.DoubleRedScienceOutput +
                      ", Auto-clear laser obstacles=" + ModSettings.Current.Laser.AutoClearObstacles +
                      ", Laser speed multiplier=" + ModSettings.Current.Laser.SpeedMultiplier + ".");
        }

        public override void Start()
        {
            // Base definitions are loaded before script mods install Harmony patches.
            foreach (BuildingDescription description in Old.GetBuildingModule().buildingDescriptions.Values)
            {
                MonumentCostPatches.ApplyCost(description);
            }
        }

        public override void FrameUpdate()
        {
        }

        public override void SimulationUdpate()
        {
        }

        private static void Plan(List<PatchPlan> patches, Type targetType, string targetMethodName, Type patchType,
            string prefixName, string postfixName, string finalizerName)
        {
            MethodInfo target = AccessTools.Method(targetType, targetMethodName);
            if (target == null)
            {
                throw new MissingMethodException(targetType.FullName, targetMethodName);
            }

            HarmonyMethod prefix = GetHarmonyMethod(patchType, prefixName);
            HarmonyMethod postfix = GetHarmonyMethod(patchType, postfixName);
            HarmonyMethod finalizer = GetHarmonyMethod(patchType, finalizerName);
            patches.Add(new PatchPlan { Target = target, Prefix = prefix, Postfix = postfix, Finalizer = finalizer });
        }

        private sealed class PatchPlan
        {
            internal MethodInfo Target;
            internal HarmonyMethod Prefix;
            internal HarmonyMethod Postfix;
            internal HarmonyMethod Finalizer;
        }

        private static void RequireField(Type targetType, string name, Type fieldType)
        {
            FieldInfo field = targetType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != fieldType)
            {
                throw new MissingFieldException(targetType.FullName, name + " (expected " + fieldType.Name + ")");
            }
        }

        private static HarmonyMethod GetHarmonyMethod(Type patchType, string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            MethodInfo patchMethod = AccessTools.Method(patchType, methodName);
            if (patchMethod == null)
            {
                throw new MissingMethodException(patchType.FullName, methodName);
            }
            return new HarmonyMethod(patchMethod);
        }
    }
}
