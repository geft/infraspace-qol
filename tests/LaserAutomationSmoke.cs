using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

// Run under .NET Framework with native cannon state and headless map objects.
// Unity rendering/raycasting/damage remains the game's responsibility.
public static class LaserAutomationSmoke
{
    private sealed class Obstacle : MultiSelectable, IDamageable
    {
        public float Health = 10f;
        public bool Selected;
        public bool Eligible = true;
        public Vector3 Position;
        public long GetID() { return 1; }
        public void Select(bool updateRepresentation) { Selected = true; }
        public void Deselect(bool updateRepresentation) { Selected = false; }
        public bool IsSelected() { return Selected; }
        public Vector3 GetPosition() { return Position; }
        public void SetHealth(float health) { Health = health; }
        public float GetHealth() { return Health; }
        public float GetMaxHealth() { return 10f; }
        public bool TakeDamage(float damage) { Health -= damage; return true; }
    }

    private static MethodInfo update;
    private static readonly HashSet<MultiSelectable> NoManualTargets = new HashSet<MultiSelectable>();

    private static LaserCannonSpaceshipPart Cannon()
    {
        return (LaserCannonSpaceshipPart)FormatterServices.GetUninitializedObject(typeof(LaserCannonSpaceshipPart));
    }

    private static void Step(LaserCannonSpaceshipPart cannon, IEnumerable<MultiSelectable> targets,
        bool available, ISet<MultiSelectable> manual, bool ready, float delta)
    {
        Func<MultiSelectable, bool> eligible = delegate(MultiSelectable target)
        {
            Obstacle obstacle = target as Obstacle;
            return obstacle != null && obstacle.Eligible && obstacle.Health > 0;
        };
        update.Invoke(null, new object[] { cannon, available, manual, ready, delta, targets, Vector3.zero, eligible });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run(Assembly mod)
    {
        update = mod.GetType("InfraSpaceQoL.LaserPatches", true).GetMethod("UpdateAutomation", BindingFlags.Static | BindingFlags.NonPublic);
        Obstacle near = new Obstacle { Position = new Vector3(10, 0, 0) };
        Obstacle far = new Obstacle { Position = new Vector3(10000, 0, 0) };
        Obstacle dead = new Obstacle { Health = 0 };
        Obstacle ineligible = new Obstacle { Eligible = false };
        var targets = new MultiSelectable[] { null, far, ineligible, dead, near };
        LaserCannonSpaceshipPart cannon = Cannon();
        Step(cannon, targets, false, NoManualTargets, true, 1);
        Check(!cannon.HasTarget(), "Unavailable cannon/station must not acquire targets.");
        Step(cannon, targets, true, NoManualTargets, false, 1);
        Check(!cannon.HasTarget(), "Unrepaired or busy cannon must not acquire targets.");
        Step(cannon, targets, true, NoManualTargets, true, 0);
        Check(!cannon.HasTarget(), "Paused game must not acquire targets.");
        Step(cannon, targets, true, NoManualTargets, true, 1);
        Check(ReferenceEquals(cannon.GetTarget(), near) && near.Selected, "Nearest eligible obstacle must be selected.");
        Step(cannon, targets, true, NoManualTargets, false, 1);
        Check(ReferenceEquals(cannon.GetTarget(), near) && near.Health == 10f, "Active cannon must not retarget or apply extra damage.");
        near.Health = 0;
        Step(cannon, targets, true, NoManualTargets, true, 1);
        Check(ReferenceEquals(cannon.GetTarget(), far) && !near.Selected, "Destroyed target must advance across the whole map.");

        Obstacle manual = new Obstacle();
        var queue = new HashSet<MultiSelectable> { manual };
        Step(cannon, targets, true, queue, false, 1);
        Check(!cannon.HasTarget() && !far.Selected, "Manual queue must release the automatic target.");
        cannon.SetTarget(manual);
        Step(cannon, targets, true, queue, false, 1);
        Check(ReferenceEquals(cannon.GetTarget(), manual), "Manual active target must be preserved.");
        Step(cannon, targets, true, NoManualTargets, false, 1);
        Check(ReferenceEquals(cannon.GetTarget(), manual), "Manual target survives an empty queue.");
        cannon.ClearTarget();
        Step(cannon, targets, true, NoManualTargets, true, 1);
        Check(ReferenceEquals(cannon.GetTarget(), far), "Automatic clearing resumes after manual work.");
        Step(cannon, targets, true, new HashSet<MultiSelectable> { far }, false, 1);
        Check(!cannon.HasTarget() && far.Selected, "Manually queuing the automatic target preserves its selection.");
        cannon.SetTarget(far);
        Step(cannon, targets, false, NoManualTargets, false, 1);
        Check(ReferenceEquals(cannon.GetTarget(), far), "Removing station must not clear a player-owned target.");

        cannon = Cannon();
        near.Health = 10;
        Step(cannon, targets, true, NoManualTargets, true, 1);
        typeof(LaserCannonSpaceshipPart).GetField("isShooting", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cannon, true);
        Step(cannon, targets, true, NoManualTargets, false, 10);
        Check(ReferenceEquals(cannon.GetTarget(), near), "A long-running acquired shot must not time out.");
        cannon.SetTarget(manual);
        Step(cannon, targets, true, NoManualTargets, false, 1);
        Check(ReferenceEquals(cannon.GetTarget(), manual) && !near.Selected, "Direct player retargeting preserves the new target and releases old selection.");
        cannon.ClearTarget();
        Step(cannon, targets, true, NoManualTargets, true, 1);
        Step(cannon, targets, false, NoManualTargets, false, 1);
        Check(!cannon.HasTarget() && !near.Selected, "Removing station stops automated firing.");
        Step(cannon, targets, true, NoManualTargets, true, 1);
        Step(cannon, targets, true, NoManualTargets, false, 5);
        Check(!cannon.HasTarget() && !near.Selected, "Unreachable target must time out.");
        Step(cannon, targets, true, NoManualTargets, true, 1);
        Check(ReferenceEquals(cannon.GetTarget(), far), "Timed-out target must be skipped on subsequent scans.");
        far.Health = 0;
        Step(cannon, targets, true, NoManualTargets, true, 1);
        Check(!cannon.HasTarget(), "No eligible targets leaves cannon idle.");
        Console.WriteLine("PASS: laser map-wide targeting, manual priority, shared cannon, pauses, station removal, and unreachable targets.");
    }
}
