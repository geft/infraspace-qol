# Development guide

Build, test, and release instructions for contributors. For installation and features, see the [user guide](README.md).

## Build your own copy

Git is optional: use GitHub's **Code → Download ZIP**, extract the source, and open the extracted folder. You can also clone the repository if you prefer.

One-time requirements on Windows:

- Install InfraSpace through Steam.
- Install [PowerShell 7](https://aka.ms/powershell-release).
- Install a [.NET SDK](https://dotnet.microsoft.com/download), version 8 or newer, and the [.NET Framework 4.7.2 developer pack](https://dotnet.microsoft.com/download/dotnet-framework/net472).

Double-click **`build.cmd`**, or run this from the source folder in PowerShell 7:

```powershell
pwsh -NoProfile -File .\build.ps1
```

The script finds InfraSpace in your Steam libraries, downloads the pinned Harmony DLL from the official [ISModKit](https://github.com/DionicSoftware/ISModKit) if needed, checks its SHA-256 checksum, builds the mod, runs regression checks, and prepares the installer. It reads game reference DLLs from your own installation. The first build requires internet access for Harmony and .NET package restore.

After the build succeeds, close InfraSpace and run:

```text
installer\bin\Release\InfraSpaceQoLInstaller.exe
```

Local development builds keep the version unchanged. Share the complete installer output folder so others can install without building. The [user guide](README.md#installation) lists the required package files. The game reference assemblies are not included.

For a custom install location or an offline Harmony copy, override the paths:

```powershell
pwsh -NoProfile -File .\build.ps1 `
  -ManagedDir "D:\SteamLibrary\steamapps\common\InfraSpace\InfraSpace_Data\Managed" `
  -HarmonyPath "C:\path\to\0Harmony.dll"
```

`-DotNetPath` selects a particular `dotnet.exe`. Harmony can also be found through the `ISModKitDir` environment variable or reused from an existing build. Nothing is installed into the game until you run the installer.

The mod is built against InfraSpace `1.56.460`. Game updates can change Harmony patch targets. Direct Collection Center shipments use roads and trucks; rail freight is not covered.

## Local development and versioning

For compile-only iterations, use `dotnet build` directly with your game assembly and Harmony paths:

```powershell
dotnet build .\InfraSpaceQoL.csproj -c Release `
  -p:InfraSpaceManagedDir="C:\Program Files (x86)\Steam\steamapps\common\InfraSpace\InfraSpace_Data\Managed" `
  -p:HarmonyPath="C:\path\to\0Harmony.dll"
```

Run `pwsh -NoProfile -File .\tests\Regression.ps1` after building, or use `build.ps1` to build and test together. Run `installer\build.bat` to refresh only the installer; it rejects a DLL whose version does not match `config.json`.

To prepare a new version locally:

```powershell
pwsh -NoProfile -File .\release.ps1
```

Each successful release increments the patch version (for example, `1.1.2` to `1.1.3`), builds and tests, refreshes `installer\bin\Release`, and packages `installer\bin\InfraSpaceQoL-Windows.zip`. Use `-Increment Minor`, `-Increment Major`, or `-Version major.minor.patch` when appropriate. It accepts the same dependency paths as `build.ps1`. `config.json` is the version source for the DLL, installer, and installed metadata. The source version is restored if a step fails.

To package an already built version without incrementing it, run `pwsh -NoProfile -File .\package.ps1`.

After committing and pushing the release source, publish the ZIP as a GitHub release asset. For example, with release notes saved in a text file:

```powershell
gh release create v1.1.8 .\installer\bin\InfraSpaceQoL-Windows.zip --target main --title "InfraSpace QoL 1.1.8" --notes-file release-notes.txt --latest
```

Use the new version number for each release, but always upload the asset as **InfraSpaceQoL-Windows.zip** and mark the release as latest. The README's `/releases/latest/download/InfraSpaceQoL-Windows.zip` link then follows the new release automatically. Building a package alone does not publish it.

Commit source code, scripts, and version metadata. Build folders, EXEs, DLLs, and ZIPs are ignored by Git. Game assemblies and downloaded dependencies remain on the local machine.
