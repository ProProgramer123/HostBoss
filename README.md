# HostBoss for Scam With Your Friends

An unofficial, experimental Windows mod that lets the lobby host control the end-of-day boss presentation. **HostBoss 1.5.1 is a BepInEx plugin for the ScamWYF launcher.** The legacy runtime (1.4.2) and graphical disk patcher (1.4.3) remain available as a separate alternative.

## Features

- F6 host panel with gameplay controls paused while it is open.
- Manual slide navigation, editable presentation/slide titles before starting, and an explicit Start Presentation gate.
- Edit three next-day goals before starting, then show them to everyone with Show Next Day Goals. Goals reset after each review; quota rules are unchanged.
- Live quota and earnings, optional quota override, and individual firing/rehiring.
- Separate built-in quote display and recording playback buttons.
- Fire All plays the native firing cutscene without firing employees. Destroy Reputation continues into the game's fire effects; End Destroy Reputation stops effects and returns everyone to the lobby.
- AI Boss toggle restores the game's boss and restarts the presentation; manual controls are disabled while AI is enabled.
- Backup-aware installer, updater, and uninstaller.

See [the full user guide](HostBoss/README.md) for behavior, limitations, and normal-play instructions. Multiplayer compatibility and native recording playback still need live testing. No clean antivirus verdict is claimed; see [the security notes](HostBoss/SECURITY.md).

## Install a release

For the launcher, download **HostBoss-Plugin-1.5.1.zip** from the repository's releases. Use the launcher's Setup tab for BepInEx and its game compatibility fixes, then install the ZIP through Get mods / Install from URL, or copy only `ScamWYF.HostBoss.dll` into `BepInEx/plugins`. The plugin uses runtime Harmony hooks and leaves game DLLs unchanged. See the [plugin guide](HostBoss/PLUGIN.md).

**Switching from the patcher:** close the game and choose **Remove / Normal Play** in HostBoss Patcher first. The plugin refuses mixed legacy/plugin installations. Disable or remove the plugin through the launcher's Mods tab with the game closed, then restart.

For the legacy patcher, extract the entire `HostBoss-Patcher-1.4.3.zip` and run `HostBoss Patcher.exe`, keeping the adjacent `Mono.Cecil.dll` and `HostBoss` folder together. This package is separate from the launcher plugin; do not install both.

GitHub's automatically generated Source code ZIP contains source, not a ready-to-run patcher. Build it using the instructions below.

## Build from source

Requirements: Windows, the .NET SDK (8 or newer), Windows .NET Framework 4.x including its C# compiler, and your own installed copy of Scam With Your Friends with managed assemblies. The game DLLs are read only as compiler references and are never included in this repository or release package. The current source was checked against the local 0.1.73-era game installation; future updates can change the required methods.

From a source checkout, run in PowerShell:

```powershell
.\HostBoss\restore-plugin-dependencies.ps1
.\HostBoss\build-plugin.ps1 -GameDir 'C:\Path\To\Scam With Your Friends'
.\HostBoss\package-plugin.ps1
```

This restores build references from the pinned official BepInEx 5.4.23.5 archive with a SHA-256 check. Neither the loader nor Harmony is redistributed in the plugin ZIP; the launcher's Setup supplies them. The plugin has no dependency on mod-lib or the legacy HostBoss runtime.

To build the legacy patcher instead:

```powershell
.\HostBoss\restore-dependencies.ps1
.\HostBoss\build.ps1 -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\Scam With Your Friends'
.\HostBoss\package.ps1
```

Dependency restoration downloads the pinned [Mono.Cecil 0.11.6 NuGet package](https://www.nuget.org/packages/Mono.Cecil/0.11.6) over HTTPS and checks its SHA-256 before extracting the .NET Framework DLL. The graphical installer itself makes no network requests. Build outputs remain in the checkout and are ignored by Git. Packaging writes `HostBoss/dist/HostBoss-Patcher-1.4.3.zip` plus a file checksum list. Build and packaging do not install the mod or modify the game's assemblies.

If local PowerShell policy prevents a script from running, use a permitted PowerShell environment or review the source with your administrator. Do not disable antivirus to build or install the mod.

## Validation

After building and packaging, run these in separate Windows PowerShell processes:

```powershell
powershell.exe -NoProfile -File .\HostBoss\tools\verify-plugin.ps1 -GameDir 'C:\Path\To\Scam With Your Friends'
```

For the legacy patcher build:

```powershell
powershell.exe -NoProfile -File .\HostBoss\tools\verify.ps1 -GameDir 'C:\Path\To\Scam With Your Friends'
powershell.exe -NoProfile -File .\HostBoss\tools\verify-replay.ps1
powershell.exe -NoProfile -File .\HostBoss\tools\verify-desktop.ps1
```

The scripts copy required assemblies into ignored local fixtures. They check patch structure, repeat installation, backup preservation, exact uninstall restoration, simulated game updates, playback hooks, and the packaged installer. They are structural and installation tests, not live multiplayer or antivirus clearance. The public-source layout was also built and tested locally before publication.

## Source layout

| Path | Purpose |
| --- | --- |
| `HostBoss/src/HostBoss.cs` | Runtime host panel and game controls |
| `HostBoss/src/Plugin.cs` | BepInEx metadata, Harmony hooks, conflict guard, and cleanup |
| `HostBoss/src/Patcher.cs` | Mono.Cecil assembly patches and restoration |
| `HostBoss/src/Installer.cs` | Windows Forms patcher and command-line entry points |
| `HostBoss/build.ps1` | Compile using the owner's installed game references |
| `HostBoss/package.ps1` | Produce the conventional EXE/DLL distribution |
| `HostBoss/tools` | Structural/installer verification scripts |

## Contributing and license

HostBoss's original source and scripts are available under the [MIT license](LICENSE). Mono.Cecil is separately MIT licensed; its [license notice](HostBoss/lib/Mono.Cecil.LICENSE.txt) is retained. The game's code, assets, trademarks, and third-party dependencies remain under their owners' licenses. No game assemblies, saves, recordings, logs, credentials, or local backups are published.

Please keep changes focused, describe how they were checked, and never attach game recordings, caller transcripts, saves, or proprietary DLLs to public issues. This project is not affiliated with the game's developers.
