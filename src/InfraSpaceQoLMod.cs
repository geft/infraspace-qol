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
            Plan(patches, typeof(Dashboard), "Start", typeof(SpeedPatches), null, "DashboardStartPostfix", null);
            Plan(patches, typeof(TimeManager), "ButtonSelectionChanged", typeof(SpeedPatches), "TimeButtonSelectionPrefix", null, null);
            Plan(patches, typeof(TimeManager), "SetTimeState", typeof(SpeedPatches), null, "TimeStatePostfix", null);

            if (ModSettings.Current.Logistics.DirectCollectionSupply)
            {
                Plan(patches, typeof(ConsumerProducer), "RequestResources", typeof(LogisticsPatches), "RequestPrefix", "RequestPostfix", null);
                Plan(patches, typeof(ConsumerProducer), "Destroy", typeof(LogisticsPatches), null, "ConsumerDestroyedPostfix", null);
                Plan(patches, typeof(OrderManager), "UpdateSimulationThread", typeof(LogisticsPatches), null, "OrderManagerPostfix", null);
                Plan(patches, typeof(CollectionProductionLogic), "TryStartDeliveryToDistributionCenter", typeof(LogisticsPatches), "CollectionDispatchPrefix", null, "CollectionDispatchFinalizer");
                Plan(patches, typeof(DistributionProductionLogic), "TrySendCar", typeof(LogisticsPatches), "DistributionDispatchPrefix", null, "DistributionDispatchFinalizer");
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
                      ", Red Science multiplier=" + ModSettings.Current.Research.RedScienceCostMultiplier.ToString("0.###") + ".");
        }

        public override void Start()
        {
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
