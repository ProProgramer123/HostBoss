# HostBoss for Scam With Your Friends

**Setup scan status (October 18, 2026):** the revised 1.60.4 EXE still has **2/71 VirusTotal detections**. These are not confirmed false positives. The ZIP's 0/68 result does not clear its contained files. Read the [exact scan reports and installer behavior](HostBoss/SECURITY-SETUP.md) before installing.

**Easy setup:** download and extract **HostBoss-Setup-1.60.4.zip**, run its EXE from [Releases](https://github.com/ProProgramer123/HostBoss/releases/latest), close the game, select its folder and click **Install / Update**. Includes both required DLLs and the official loader setup. [Setup guide](HostBoss/SETUP.md).

An unofficial, experimental Windows mod that lets the lobby host control the end-of-day boss presentation. **HostBoss 1.60.4 uses BepInEx and the shared ScamWYF mod-lib 1.1.0.** The legacy runtime (1.4.2) and graphical disk patcher (1.4.3) remain separate historical releases.

The break-room character-creator laptop now includes **HostBoss** and **Slide Maker** apps with native app windows. Both require the player opening them to be the host. F6 remains available and shares the same Slide Maker draft and saves.

**Game 0.1.81 / Unity 6000.3.26 update:** 1.60.4 includes `Repair-GameUpdate.ps1` for the old Input System 1.19 override that can crash the updated game before BepInEx starts, reporting corrupt `globalgamemanagers.assets`. With the game closed, run that script with `-GameDir` pointing at the game folder, then install both bundled DLLs. It backs up only an older Input System override; saves, game assets and other mods stay in place. See the [update instructions](HostBoss/PLUGIN.md#game-0181-update).

## Features

- F6 host panel with gameplay controls paused while it is open.
- Full Slide Maker with the game's default templates, custom text/quotes/PNGs, slide add/duplicate/delete/reorder, and persistent save/load available anytime.
- Editable boss TTS tied to each slide, with the game's networked voice playback.
- Native app announcements, real What the Boss Heard / chosen review slides and original TTS toggles.
- Per-slide boss-cam corner choices, user-data saves, visible save feedback and automatic reopening of the last saved deck.
- Shared mod-lib F1 menu, coordinated patches and one registered F6 hotkey.
- Manual slide navigation and an explicit Start Presentation gate.
- Edit three next-day goals before starting, then show them to everyone with Show Next Day Goals. Goals reset after each review; quota rules are unchanged.
- Live quota and earnings, optional quota override, and individual firing/rehiring.
- Separate built-in quote display and recording playback buttons.
- Fire All plays the native firing cutscene without firing employees. Destroy Reputation continues into the game's fire effects; End Destroy Reputation stops effects and returns everyone to the lobby.
- AI Boss toggle restores the game's boss and restarts the presentation; manual controls are disabled while AI is enabled.
- The historical disk patcher remains available as a separate release with backup-aware install and removal.

See [the full user guide](HostBoss/README.md) for behavior, limitations, and normal-play instructions. Multiplayer compatibility and native recording playback still need live testing. No clean antivirus verdict is claimed; see [the security notes](HostBoss/SECURITY.md).

## Install a release

For version 1.60.4, put `ScamWYF.HostBoss.dll` from **HostBoss-Plugin-1.60.4.zip** in `BepInEx/plugins` and the bundled `ScamWYF.Modding.Core.dll` in `BepInEx/core`. The launcher catalogue must route both files to their correct folders before this version can be installed through it; the required entry is in [catalogue-entry.json](HostBoss/catalogue-entry.json). Use the launcher's Setup tab for BepInEx and its game compatibility fixes first. The plugin uses runtime Harmony hooks and leaves game DLLs unchanged. See the [plugin guide](HostBoss/PLUGIN.md).

**Switching from the patcher:** close the game and choose **Remove / Normal Play** in HostBoss Patcher first. The plugin refuses mixed legacy/plugin installations. Disable or remove the plugin through the launcher's Mods tab with the game closed, then restart.

For the legacy patcher, extract the entire `HostBoss-Patcher-1.4.3.zip` and run `HostBoss Patcher.exe`, keeping the adjacent `Mono.Cecil.dll` and `HostBoss` folder together. This package is separate from the launcher plugin; do not install both.

GitHub's automatically generated Source code ZIP contains source, not a ready-to-run patcher. Build it using the instructions below.

## Build from source

Requirements: Windows, the .NET SDK (8 or newer), Windows .NET Framework 4.x including its C# compiler, and your own installed copy of Scam With Your Friends with managed assemblies. The game DLLs are read only as compiler references and are never included in this repository or release package. The current source was checked against the local 0.1.81-swyf game installation; future updates can change the required methods.

From a source checkout, run in PowerShell:

```powershell
.\HostBoss\restore-plugin-dependencies.ps1
.\HostBoss\restore-modlib.ps1 -GameDir 'C:\Path\To\Scam With Your Friends'
.\HostBoss\build-plugin.ps1 -GameDir 'C:\Path\To\Scam With Your Friends'
.\HostBoss\package-plugin.ps1
```

This restores build references from the pinned official BepInEx 5.4.23.5 archive with a SHA-256 check. Also run `HostBoss/restore-modlib.ps1 -GameDir <game folder>` before building: it builds mod-lib from the unmodified v1.1.0 C# sources at commit `efe7b332d5832f7b5efce941905df8a75795f30e`, checks the pin, and packages the shared DLL with its MIT license and provenance. Neither BepInEx nor Harmony is redistributed in the plugin ZIP; the launcher's Setup supplies them. There is no dependency on the legacy HostBoss runtime.

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
