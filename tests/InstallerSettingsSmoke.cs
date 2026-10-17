using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using InfraSpaceQoL;
using InfraSpaceQoLInstaller;

internal static class InstallerSettingsSmoke
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string directory = Path.Combine(Path.GetTempPath(), "InfraSpaceInstallerTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            const string original = "{\"Future\":{\"Keep\":42},\"Research\":{\"DoubleRedScienceOutput\":false,\"Unknown\":\"keep me\"},\"Laser\":{\"SpeedMultiplier\":1.75}}";
            File.WriteAllText(path, original);
            var serializer = new JavaScriptSerializer();
            using (var editor = new SettingsEditor(new SettingsDocument(path)))
            {
                Check(editor.TabPages.Count == typeof(SettingsOptions).GetFields().Length, "Missing feature category");
                int controls = 0;
                foreach (FieldInfo section in typeof(SettingsOptions).GetFields())
                {
                    TabPage page = null;
                    foreach (TabPage candidate in editor.TabPages)
                        if (candidate.Text == section.Name) page = candidate;
                    Check(page != null, "Missing section " + section.Name);
                    int fieldCount = 0;
                    foreach (Control control in page.Controls[0].Controls)
                    {
                        var checkbox = control as CheckBox;
                        var number = control as NumericUpDown;
                        if (checkbox != null) { checkbox.Checked = false; fieldCount++; }
                        if (number != null) { number.Value = number.Minimum; fieldCount++; }
                    }
                    Check(fieldCount == section.FieldType.GetFields().Length, "Missing controls for " + section.Name);
                    controls += fieldCount;
                }
                string json = editor.SerializeChoices();
                var saved = serializer.Deserialize<Dictionary<string, object>>(json);
                foreach (FieldInfo section in typeof(SettingsOptions).GetFields())
                {
                    var values = (Dictionary<string, object>)saved[section.Name];
                    foreach (FieldInfo field in section.FieldType.GetFields())
                    {
                        if (field.FieldType == typeof(bool)) Check(values[field.Name].Equals(false), "Toggle was not saved");
                        else
                        {
                            var range = (SettingRangeAttribute)Attribute.GetCustomAttribute(field, typeof(SettingRangeAttribute));
                            Check(Convert.ToDecimal(values[field.Name]) == range.Minimum, "Numeric choice was not saved");
                        }
                    }
                }
                Check(((Dictionary<string, object>)saved["Future"])["Keep"].Equals(42), "Unknown section lost");
                Check(((Dictionary<string, object>)saved["Research"])["Unknown"].Equals("keep me"), "Unknown key lost");
                Check(File.ReadAllText(path) == original, "Editing changed settings before Install");
                SettingsDocument.Save(path, json);
                Check(File.ReadAllText(path + ".bak") == original, "Previous settings were not backed up");
                using (var reopened = new SettingsEditor(new SettingsDocument(path)))
                    Check(reopened.SerializeChoices() == json, "Reinstall did not preserve selected choices");
                Console.WriteLine("PASS: all " + controls + " installer controls, opt-outs, numeric choices, unknown keys, backups and reopening.");
            }

            File.WriteAllText(path, original);
            using (var documentEditor = new SettingsEditor(new SettingsDocument(path)))
            {
                var loaded = serializer.Deserialize<Dictionary<string, object>>(documentEditor.SerializeChoices());
                Check(((Dictionary<string, object>)loaded["Research"])["DoubleRedScienceOutput"].Equals(false), "Saved false replaced by default");
                Check(Convert.ToDecimal(((Dictionary<string, object>)loaded["Laser"])["SpeedMultiplier"]) == 1.75m, "Saved multiplier lost");
                Check(((Dictionary<string, object>)loaded["Laser"])["AutoClearObstacles"].Equals(true), "Missing field did not receive default");
                Check(((Dictionary<string, object>)loaded["Monuments"])["ReduceMagneticFieldGeneratorCost"].Equals(true), "Missing section did not receive default");
            }

            using (var form = new InstallerForm(args[0], directory))
            {
                // Render our form offscreen without installing into the user's game.
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.Show();
                Application.DoEvents();
                var editor = (SettingsEditor)typeof(InstallerForm).GetField("settingsEditor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                Check(editor != null, "Settings editor failed to load");
                foreach (Control control in editor.TabPages[2].Controls[0].Controls)
                    if (control is CheckBox) Check(!((CheckBox)control).Checked, "Form did not load installed settings");
                editor.SelectedIndex = 1;
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(args[1]);
                }
                form.Hide();
            }
            string freshInstall = Path.Combine(directory, "fresh");
            using (var form = new InstallerForm(args[0], freshInstall))
            {
                var editor = (SettingsEditor)typeof(InstallerForm).GetField("settingsEditor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                Check(editor != null, "First-install package settings failed to load");
                Check(!Directory.Exists(freshInstall), "Opening installer created install files");
            }
            foreach (string invalid in new[] { "null", "[1]", "{", "{\"Laser\":[]}", "{\"Laser\":{\"SpeedMultiplier\":99}}", "{\"Laser\":{\"AutoClearObstacles\":\"false\"}}" })
            {
                File.WriteAllText(path, invalid);
                bool rejected = false;
                try { using (var editor = new SettingsEditor(new SettingsDocument(path))) { editor.SerializeChoices(); } }
                catch (Exception) { rejected = true; }
                Check(rejected, "Invalid settings accepted: " + invalid);
                Check(File.ReadAllText(path) == invalid, "Invalid settings overwritten");
            }
            using (var form = new InstallerForm(args[0], directory))
            {
                var button = (Button)typeof(InstallerForm).GetField("installButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                Check(!button.Enabled, "Invalid installed settings did not block Install");
            }
            Console.WriteLine("PASS: older settings, installed-setting precedence, form rendering and invalid-setting protection.");
        }
        finally
        {
            Check(Path.GetFullPath(directory).StartsWith(Path.Combine(Path.GetTempPath(), "InfraSpaceInstallerTest-"), StringComparison.OrdinalIgnoreCase), "Unsafe test cleanup path");
            Directory.Delete(directory, true);
        }
    }
}
