using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace InfraSpaceQoL
{
    internal sealed class ModSettings : SettingsOptions
    {
        internal static ModSettings Current { get; private set; } = new ModSettings();

        internal static string NormalizeSettingsJson(string json)
        {
            JObject document = JObject.Parse(json);
            JObject legacy = document["Monuments"] as JObject;
            if (legacy != null && legacy["ReduceMagneticFieldGeneratorCost"] != null)
            {
                JObject terraforming = document["Terraforming"] as JObject;
                if (document["Terraforming"] == null || document["Terraforming"].Type == JTokenType.Null)
                    document["Terraforming"] = terraforming = new JObject();
                if (terraforming != null && terraforming["ReduceMagneticFieldGeneratorCost"] == null)
                    terraforming["ReduceMagneticFieldGeneratorCost"] = legacy["ReduceMagneticFieldGeneratorCost"].DeepClone();
            }
            return document.ToString();
        }

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
                ModSettings settings = JsonConvert.DeserializeObject<ModSettings>(NormalizeSettingsJson(File.ReadAllText(path)));
                if (settings == null)
                {
                    throw new InvalidDataException("The JSON document did not contain a settings object.");
                }

                if (settings.Speed == null) settings.Speed = new SpeedSettings();
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
                if (settings.Terraforming.MagneticFieldGeneratorPowerMW < 1m || settings.Terraforming.MagneticFieldGeneratorPowerMW > 5000m)
                {
                    Debug.LogWarning("[InfraSpaceQoL] Magnetic Field Generator power must be between 1 and 5000 MW; using 1000.");
                    settings.Terraforming.MagneticFieldGeneratorPowerMW = 1000m;
                }
                if (settings.Laser == null)
                {
                    settings.Laser = new LaserSettings();
                }
                if (settings.Laser.SpeedMultiplier < 1m || settings.Laser.SpeedMultiplier > 10m)
                {
                    Debug.LogWarning("[InfraSpaceQoL] Laser SpeedMultiplier must be between 1 and 10; using 3.");
                    settings.Laser.SpeedMultiplier = 3m;
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

}
