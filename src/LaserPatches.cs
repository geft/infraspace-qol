using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace InfraSpaceQoL
{
    internal static class LaserPatches
    {
        public static void LaserSpeedBonusPostfix(string tech, ref float __result)
        {
            if (string.Equals(tech, "laserSpeed", StringComparison.Ordinal))
            {
                // The cannon advances damage and beam warm-up by
                // deltaTime * (1 + bonus). Multiply that complete upgraded rate.
                __result = (1f + __result) * (float)ModSettings.Current.Laser.SpeedMultiplier - 1f;
            }
        }

        private sealed class AutoState
        {
            internal MultiSelectable Target;
            internal float WaitingTime;
            internal float ScanDelay;
            internal Vector3? LastPosition;
            internal readonly HashSet<MultiSelectable> Skipped = new HashSet<MultiSelectable>();
        }

        private static readonly ConditionalWeakTable<LaserCannonSpaceshipPart, AutoState> States =
            new ConditionalWeakTable<LaserCannonSpaceshipPart, AutoState>();

        public static void LaserUpdatePrefix(LaserCannonSpaceshipPart __instance)
        {
            WorldScripts world = WorldScripts.Inst;
            if (world == null || world.registry == null || InfraSpaceCore.envObjectModule == null)
            {
                return;
            }
            Building station = world.registry.FindFirstBuiltBuilding("targetingStation");
            var manualTargets = new HashSet<MultiSelectable>();
            foreach (Building building in world.registry.builtBuildingsList)
            {
                var logic = building.consumerProducer == null ? null : building.consumerProducer.productionLogic as LaserProductionLogic;
                if (logic != null && logic.multiSelector != null && logic.multiSelector.HasSelectedAnyItem())
                {
                    for (int i = 0; i < logic.multiSelector.GetSelectedCount(); i++)
                    {
                        MultiSelectable selected;
                        if (logic.multiSelector.GetSelectionAt(i, out selected) && selected != null)
                        {
                            manualTargets.Add(selected);
                        }
                    }
                }
            }

            UpdateAutomation(__instance, station != null && __instance.IsDone(), manualTargets,
                __instance.IsReadyToFire(), Time.deltaTime,
                InfraSpaceCore.envObjectModule.GetEnvObjects(),
                station == null ? Vector3.zero : station.GetPosition(), IsEligibleTarget);
        }

        private static bool IsEligibleTarget(MultiSelectable target)
        {
            var obstacle = target as EnvironmentObject;
            return obstacle != null && obstacle.IsValid() && !obstacle.isDecal && obstacle.IsClickable() &&
                obstacle.BlocksBuildings() && obstacle.GetHealth() > 0f && obstacle.representation != null;
        }

        // Runs on the Unity thread, like the cannon's targeting raycasts and damage.
        // Tests supply headless targets; the game supplies environment obstacles.
        internal static void UpdateAutomation(LaserCannonSpaceshipPart cannon, bool available, ISet<MultiSelectable> manualTargets,
            bool ready, float deltaTime, IEnumerable<MultiSelectable> candidates, Vector3 origin,
            Func<MultiSelectable, bool> eligible)
        {
            AutoState state = States.GetValue(cannon, key => new AutoState());
            IDamageable current = cannon.GetTarget();
            if (state.Target != null && !ReferenceEquals(current, state.Target))
            {
                // Another targeting station or the player replaced our target.
                ReleaseTarget(cannon, state, manualTargets);
            }
            if (!available || (manualTargets != null && manualTargets.Count > 0))
            {
                ReleaseTarget(cannon, state, manualTargets);
                return;
            }
            if (deltaTime <= 0f)
            {
                return;
            }
            if (state.Target != null)
            {
                if (((IDamageable)state.Target).GetHealth() <= 0f)
                {
                    ReleaseTarget(cannon, state, manualTargets);
                }
                else
                {
                    // A roof or missing collider can prevent the native raycast
                    // from acquiring an obstacle. Skip it rather than stall forever.
                    state.WaitingTime = cannon.IsShooting() ? 0f : state.WaitingTime + deltaTime;
                    if (state.WaitingTime < 5f)
                    {
                        return;
                    }
                    state.Skipped.Add(state.Target);
                    ReleaseTarget(cannon, state, manualTargets);
                    return;
                }
            }
            current = cannon.GetTarget();
            if (!ready || (current != null && current.GetHealth() > 0f))
            {
                return;
            }
            state.ScanDelay -= deltaTime;
            if (state.ScanDelay > 0f)
            {
                return;
            }
            MultiSelectable next = FindNearestTarget(candidates, state.LastPosition ?? origin, state.Skipped, eligible);
            if (next == null)
            {
                state.ScanDelay = 1f;
                return;
            }
            state.Target = next;
            state.WaitingTime = 0f;
            state.LastPosition = next.GetPosition();
            // The native station cancels unselected targets. Mark only our own
            // target selected, without changing the player's manual selection queue.
            next.Select(false);
            cannon.SetTarget((IDamageable)next);
        }

        private static void ReleaseTarget(LaserCannonSpaceshipPart cannon, AutoState state, ISet<MultiSelectable> manualTargets)
        {
            if (state.Target == null)
            {
                return;
            }
            if (ReferenceEquals(cannon.GetTarget(), state.Target))
            {
                cannon.ClearTarget();
            }
            if (manualTargets == null || !manualTargets.Contains(state.Target))
            {
                state.Target.Deselect(false);
            }
            state.Target = null;
            state.WaitingTime = 0f;
        }

        internal static MultiSelectable FindNearestTarget(IEnumerable<MultiSelectable> candidates, Vector3 origin,
            ISet<MultiSelectable> skipped, Func<MultiSelectable, bool> eligible)
        {
            MultiSelectable nearest = null;
            double nearestDistance = double.PositiveInfinity;
            if (candidates == null)
            {
                return null;
            }
            foreach (MultiSelectable target in candidates)
            {
                if (target == null || skipped.Contains(target) || !eligible(target))
                {
                    continue;
                }
                Vector3 position = target.GetPosition();
                double dx = (double)position.x - origin.x;
                double dz = (double)position.z - origin.z;
                double distance = dx * dx + dz * dz;
                if (distance < nearestDistance)
                {
                    nearest = target;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }
    }
}
