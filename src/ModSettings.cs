using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace InfraSpaceQoL
{
    internal sealed class ModSettings
    {
        [JsonProperty("Logistics")]
        public LogisticsSettings Logistics = new LogisticsSettings();

        [JsonProperty("Research")]
        public ResearchSettings Research = new ResearchSettings();

        [JsonProperty("Workforce")]
        public WorkforceSettings Workforce = new WorkforceSettings();

        [JsonProperty("Terraforming")]
        public TerraformingSettings Terraforming = new TerraformingSettings();

        internal static ModSettings Current { get; private set; } = new ModSettings();

        internal static void Load()
        {
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = Path.Combine(directory ?? string.Empty, "settings.json");
            if (!File.Exists(path))
            {
                Current = new ModSettings();
                Debug.Log("[InfraSpaceQoL] settings.json not found; using the default settings.");
                return;
            }

            try
            {
                ModSettings settings = JsonConvert.DeserializeObject<ModSettings>(File.ReadAllText(path));
                if (settings == null)
                {
                    throw new InvalidDataException("The JSON document did not contain a settings object.");
                }

                if (settings.Logistics == null)
                {
                    settings.Logistics = new LogisticsSettings();
                }
                if (settings.Research == null)
                {
                    settings.Research = new ResearchSettings();
                }
                if (settings.Workforce == null)
                {
                    settings.Workforce = new WorkforceSettings();
                }
                if (settings.Terraforming == null)
                {
                    settings.Terraforming = new TerraformingSettings();
                }
                if (settings.Research.RedScienceCostMultiplier < 0m ||
                    settings.Research.RedScienceCostMultiplier > 1m)
                {
                    Debug.LogWarning("[InfraSpaceQoL] RedScienceCostMultiplier must be between 0 and 1; using 0.5.");
                    settings.Research.RedScienceCostMultiplier = 0.5m;
                }

                Current = settings;
            }
            catch (Exception exception)
            {
                Current = new ModSettings();
                Debug.LogError("[InfraSpaceQoL] Could not read settings.json; using defaults. " + exception.Message);
            }
        }
    }

    internal sealed class LogisticsSettings
    {
        [JsonProperty("DirectCollectionSupply")]
        public bool DirectCollectionSupply = true;
    }

    internal sealed class ResearchSettings
    {
        [JsonProperty("RedScienceCostMultiplier")]
        public decimal RedScienceCostMultiplier = 0.5m;
    }

    internal sealed class WorkforceSettings
    {
        [JsonProperty("ReleaseWorkersWhenAllOutputsFull")]
        public bool ReleaseWorkersWhenAllOutputsFull = true;
    }

    internal sealed class TerraformingSettings
    {
        [JsonProperty("ExpandWhenLocalAreaSaturated")]
        public bool ExpandWhenLocalAreaSaturated = true;
    }
}
