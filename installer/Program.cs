using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InfraSpaceQoLInstaller
{
    internal static class Program
    {
        private static readonly string[] PackageFiles =
        {
            "InfraSpaceQoL.dll",
            "0Harmony.dll",
            "config.json",
            "settings.json"
        };

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string packageDirectory = FindPackageDirectory(AppDomain.CurrentDomain.BaseDirectory);
            if (packageDirectory == null)
            {
                MessageBox.Show(
                    "Mod files were not found. Extract the release ZIP, or build the mod before running its installer.",
                    "InfraSpace QoL Installer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string installDirectory = Path.Combine(
                userProfile,
                "AppData",
                "LocalLow",
                "Dionic Software",
                "InfraSpace",
                "mods",
                "InfraSpaceQoL");

            Application.Run(new InstallerForm(packageDirectory, installDirectory));
        }

        private static string FindPackageDirectory(string baseDirectory)
        {
            string[] candidates =
            {
                baseDirectory,
                Path.GetFullPath(Path.Combine(baseDirectory, @"bin\Release\net472")),
                Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\bin\Release\net472"))
            };

            foreach (string candidate in candidates)
            {
                if (ContainsPackageFiles(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool ContainsPackageFiles(string directory)
        {
            foreach (string fileName in PackageFiles)
            {
                if (!File.Exists(Path.Combine(directory, fileName)))
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly string packageDirectory;
        private readonly string installDirectory;
        private readonly CheckBox openFolder;
        private readonly Label status;
        private readonly Button installButton;

        internal InstallerForm(string packageDirectory, string installDirectory)
        {
            this.packageDirectory = packageDirectory;
            this.installDirectory = installDirectory;

            Text = "InfraSpace QoL Installer";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 310);
            Font = new Font("Segoe UI", 9F);

            Label title = new Label
            {
                Text = "Install InfraSpace QoL",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                Location = new Point(18, 16),
                Size = new Size(520, 32)
            };
            Controls.Add(title);

            Label description = new Label
            {
                Text = "Install the mod in InfraSpace's local mods folder. Existing settings are preserved.",
                Location = new Point(20, 55),
                Size = new Size(520, 36)
            };
            Controls.Add(description);

            Controls.Add(new Label
            {
                Text = "Package:",
                Location = new Point(20, 104),
                Size = new Size(80, 20)
            });

            Controls.Add(new Label
            {
                Text = packageDirectory,
                Location = new Point(102, 104),
                Size = new Size(438, 36),
                AutoEllipsis = true
            });

            Controls.Add(new Label
            {
                Text = "Install location:",
                Location = new Point(20, 151),
                Size = new Size(110, 20)
            });

            TextBox destination = new TextBox
            {
                Text = installDirectory,
                Location = new Point(20, 174),
                Size = new Size(520, 24),
                ReadOnly = true
            };
            Controls.Add(destination);

            openFolder = new CheckBox
            {
                Text = "Open the mod folder after installation",
                Checked = true,
                Location = new Point(20, 210),
                Size = new Size(300, 24)
            };
            Controls.Add(openFolder);

            status = new Label
            {
                Text = "Enable InfraSpace QoL in the game's Mods menu after installation.",
                Location = new Point(20, 244),
                Size = new Size(520, 22)
            };
            Controls.Add(status);

            installButton = new Button
            {
                Text = "Install",
                Location = new Point(350, 272),
                Size = new Size(90, 28)
            };
            installButton.Click += InstallButton_Click;
            Controls.Add(installButton);
            AcceptButton = installButton;

            Button closeButton = new Button
            {
                Text = "Close",
                Location = new Point(450, 272),
                Size = new Size(90, 28),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(closeButton);
            CancelButton = closeButton;
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            try
            {
                installButton.Enabled = false;
                status.Text = "Installing...";
                Refresh();

                Directory.CreateDirectory(installDirectory);
                foreach (string fileName in new[] { "InfraSpaceQoL.dll", "0Harmony.dll", "config.json" })
                {
                    File.Copy(
                        Path.Combine(packageDirectory, fileName),
                        Path.Combine(installDirectory, fileName),
                        true);
                }

                // Keep the player's settings when updating; install defaults only on first install.
                string settingsPath = Path.Combine(installDirectory, "settings.json");
                if (!File.Exists(settingsPath))
                {
                    File.Copy(Path.Combine(packageDirectory, "settings.json"), settingsPath);
                }

                MessageBox.Show(
                    "Installation complete.\r\n\r\nEnable InfraSpace QoL in the game's Mods menu.",
                    "InfraSpace QoL Installer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                if (openFolder.Checked)
                {
                    Process.Start("explorer.exe", "\"" + installDirectory + "\"");
                }

                Close();
            }
            catch (Exception exception)
            {
                status.Text = "Installation failed.";
                installButton.Enabled = true;
                MessageBox.Show(
                    exception.Message,
                    "InfraSpace QoL Installer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
