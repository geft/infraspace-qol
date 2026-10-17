using System;
using System.ComponentModel;

namespace InfraSpaceQoL
{
    // Shared by the mod and the standalone installer so new settings appear automatically.
    public class SettingsOptions
    {
        public SpeedSettings Speed = new SpeedSettings();
        public LogisticsSettings Logistics = new LogisticsSettings();
        public ResearchSettings Research = new ResearchSettings();
        public WorkforceSettings Workforce = new WorkforceSettings();
        public TerraformingSettings Terraforming = new TerraformingSettings();
        public LaserSettings Laser = new LaserSettings();
        public MonumentSettings Monuments = new MonumentSettings();
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SettingLabelAttribute : Attribute
    {
        public readonly string Text;
        public SettingLabelAttribute(string text) { Text = text; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SettingRangeAttribute : Attribute
    {
        public readonly decimal Minimum;
        public readonly decimal Maximum;
        public readonly decimal Increment;
        public readonly int DecimalPlaces;

        public SettingRangeAttribute(int minimum, int maximum, int incrementPercent, int decimalPlaces)
        {
            Minimum = minimum;
            Maximum = maximum;
            Increment = incrementPercent / 100m;
            DecimalPlaces = decimalPlaces;
        }
    }

    public sealed class SpeedSettings
    {
        [SettingLabel("Show 4x and 8x speed controls")]
        [Description("Adds Very Fast and Superspeed buttons with their own icons and pause/resume support.")]
        public bool ExtraSpeedControls = true;
    }

    public sealed class LogisticsSettings
    {
        [SettingLabel("Direct Collection Center supply")]
        [Description("Supply factories, research labs, construction, upgrades and surplus storage over roads.")]
        public bool DirectCollectionSupply = true;

        [SettingLabel("Storehouses receive overflow only")]
        [Description("Retain supplier stock below 90% capacity. Applies even with direct supply disabled.")]
        public bool OverflowOnlyStorage = true;

        [SettingLabel("Exempt adamantite from the overflow threshold")]
        [Description("Allow full truckloads of adamantite into storage below 90% capacity.")]
        public bool AdamantiteStorageExemption = true;

        [SettingLabel("Prefer the nearest eligible Storehouse")]
        [Description("Choose storage by road distance. Applies to native suppliers and direct Collection Center supply.")]
        public bool PreferNearestStorehouse = true;

        [SettingLabel("Enforce Collection Center radius at dispatch")]
        [Description("Remove stale out-of-radius producer connections before dispatching new trucks.")]
        public bool EnforceCollectionRadius = true;

        [SettingLabel("Allow final partial construction and repair loads")]
        [Description("Direct supply can send the exact remaining materials for construction, upgrades and spaceship repairs.")]
        public bool AllowPartialFinalLoads = true;
    }

    public sealed class ResearchSettings
    {
        [SettingLabel("Red Science cost multiplier")]
        [Description("Packs consumed by labs: 0 = free, 0.5 = half cost, 1 = normal cost.")]
        [SettingRange(0, 1, 1, 2)]
        public decimal RedScienceCostMultiplier = 0.5m;

        [SettingLabel("Double Red Science output")]
        [Description("Double factory output, including output upgrades, without changing ingredients or batch time.")]
        public bool DoubleRedScienceOutput = true;
    }

    public sealed class WorkforceSettings
    {
        [SettingLabel("Release workers when all output buffers are full")]
        [Description("Factories become eligible for workers again when any output has room; workforce priorities stay in effect.")]
        public bool ReleaseWorkersWhenAllOutputsFull = true;
    }

    public sealed class TerraformingSettings
    {
        [SettingLabel("Expand saturated terraformer coverage")]
        [Description("Double coverage when 95% of the current circle reaches 99% total progress, up to the playable map.")]
        public bool ExpandWhenLocalAreaSaturated = true;
    }

    public sealed class LaserSettings
    {
        [SettingLabel("Automatically clear laser obstacles")]
        [Description("Requires a repaired cannon and a targeting station. Manual targeting queues take priority.")]
        public bool AutoClearObstacles = true;

        [SettingLabel("Laser speed multiplier")]
        [Description("Damage and beam warmup: 1 = normal, 3 = triple speed. Multiplies upgrades and applies to manual fire too.")]
        [SettingRange(1, 10, 25, 2)]
        public decimal SpeedMultiplier = 3m;
    }

    public sealed class MonumentSettings
    {
        [SettingLabel("Reduce Magnetic Field Generator adamantite cost")]
        [Description("Use 75 instead of 250 for the upfront placement cost. Disabling restores the normal placement cost.")]
        public bool ReduceMagneticFieldGeneratorCost = true;
    }
}
