using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using InfraSpaceQoL;

namespace InfraSpaceQoLInstaller
{
    internal sealed class SettingsDocument
    {
        private readonly Dictionary<string, object> document;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        internal SettingsDocument(string path)
        {
            document = serializer.DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
            if (document == null) throw new InvalidDataException("Settings must contain a JSON object: " + path);
            object legacySection;
            if (document.TryGetValue("Monuments", out legacySection))
            {
                var legacy = legacySection as Dictionary<string, object>;
                object value;
                if (legacy != null && legacy.TryGetValue("ReduceMagneticFieldGeneratorCost", out value))
                {
                    var terraforming = GetSection("Terraforming");
                    if (!terraforming.ContainsKey("ReduceMagneticFieldGeneratorCost"))
                        terraforming["ReduceMagneticFieldGeneratorCost"] = value;
                    legacy.Remove("ReduceMagneticFieldGeneratorCost");
                    if (legacy.Count == 0) document.Remove("Monuments");
                }
            }
        }

        internal object Read(string section, FieldInfo field, object defaults)
        {
            Dictionary<string, object> values = GetSection(section);
            object value;
            if (!values.TryGetValue(field.Name, out value)) return field.GetValue(defaults);
            if (field.FieldType == typeof(bool) && value is bool) return value;
            if (field.FieldType == typeof(decimal) && (value is int || value is long || value is decimal || value is double))
            {
                decimal number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                SettingRangeAttribute range = (SettingRangeAttribute)Attribute.GetCustomAttribute(field, typeof(SettingRangeAttribute));
                if (range != null && number >= range.Minimum && number <= range.Maximum) return number;
            }
            throw new InvalidDataException("Invalid setting: " + section + "." + field.Name + ". Correct settings.json before installing.");
        }

        private Dictionary<string, object> GetSection(string section)
        {
            object value;
            if (!document.TryGetValue(section, out value) || value == null)
            {
                var created = new Dictionary<string, object>();
                document[section] = created;
                return created;
            }
            var values = value as Dictionary<string, object>;
            if (values == null) throw new InvalidDataException("Settings section must be an object: " + section);
            return values;
        }

        internal void Set(string section, string name, object value)
        {
            GetSection(section)[name] = value;
        }

        internal string Serialize()
        {
            return serializer.Serialize(document);
        }

        internal static void Save(string path, string json)
        {
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, json + Environment.NewLine);
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    internal sealed class SettingsEditor : TabControl
    {
        private readonly SettingsDocument document;
        private readonly List<Action> apply = new List<Action>();
        private readonly ToolTip tooltips = new ToolTip();

        internal SettingsEditor(SettingsDocument document)
        {
            this.document = document;
            var defaults = new SettingsOptions();
            foreach (FieldInfo section in typeof(SettingsOptions).GetFields())
            {
                var page = new TabPage(section.Name);
                var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
                page.Controls.Add(panel);
                TabPages.Add(page);
                int y = 14;
                object sectionDefaults = section.GetValue(defaults);
                foreach (FieldInfo field in section.FieldType.GetFields())
                {
                    object value = document.Read(section.Name, field, sectionDefaults);
                    var title = (SettingLabelAttribute)Attribute.GetCustomAttribute(field, typeof(SettingLabelAttribute));
                    var description = (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
                    Control input;
                    if (field.FieldType == typeof(bool))
                    {
                        var checkbox = new CheckBox { Text = title.Text, Checked = (bool)value,
                            Location = new Point(12, y), Size = new Size(660, 26) };
                        input = checkbox;
                        string sectionName = section.Name;
                        string fieldName = field.Name;
                        apply.Add(delegate { document.Set(sectionName, fieldName, checkbox.Checked); });
                    }
                    else
                    {
                        var range = (SettingRangeAttribute)Attribute.GetCustomAttribute(field, typeof(SettingRangeAttribute));
                        panel.Controls.Add(new Label { Text = title.Text, Location = new Point(12, y + 4),
                            Size = new Size(490, 24) });
                        var number = new NumericUpDown { Minimum = range.Minimum, Maximum = range.Maximum,
                            Increment = range.Increment, DecimalPlaces = range.DecimalPlaces, Value = (decimal)value,
                            Location = new Point(520, y), Size = new Size(120, 26) };
                        input = number;
                        string sectionName = section.Name;
                        string fieldName = field.Name;
                        apply.Add(delegate { document.Set(sectionName, fieldName, number.Value); });
                    }
                    panel.Controls.Add(input);
                    tooltips.SetToolTip(input, description.Description);
                    panel.Controls.Add(new Label { Text = description.Description, Location = new Point(32, y + 28),
                        Size = new Size(620, 42), ForeColor = SystemColors.GrayText });
                    y += 84;
                }
            }
        }

        internal string SerializeChoices()
        {
            foreach (Action action in apply) action();
            return document.Serialize();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tooltips.Dispose();
            base.Dispose(disposing);
        }
    }
}

