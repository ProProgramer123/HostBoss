# HostBoss setup 1.60.4

Download **HostBoss-Setup-1.60.4.zip** from the GitHub release and extract the entire ZIP. Keep `HostBoss-Setup-1.60.4.exe`, `Mono.Cecil.dll`, `ScamWYF.Setup.Core.dll`, `SHA256SUMS.txt` and the `SetupFiles` folder together. Run the EXE, select the installed game folder, then click **Install / Update** with the game closed. Start the game, host a lobby and press **F6** afterward.

Requires Windows 10/11 x64, .NET Framework 4.8 and the installed game. The extracted package works offline without a separate launcher or SDK. If access is denied, close setup and use Windows **Run as administrator**. Saved presentations are retained. Legacy HostBoss disk patches must first be removed using the old patcher's **Remove / Normal Play**.

## What changed after 1.60.3

The withdrawn 1.60.3 EXE had 9/71 VirusTotal detections. Those detections have not been confirmed as false positives. Version 1.60.4 replaces the self-extracting EXE with a conventional Windows application and inspectable files. It runs the official C# setup implementation directly, loads its dependencies normally from disk, contains no embedded payload, and launches no PowerShell or other child process. It does not use an execution-policy bypass, download code during installation, or change security settings. This redesign improves inspectability and removes unnecessary shell execution; it does not prove that a particular antivirus detection was false.

Read [the scan and behavior report](https://github.com/ProProgramer123/HostBoss/blob/main/HostBoss/SECURITY-SETUP.md) for the exact release hashes, VirusTotal results and limitations. Do not disable antivirus to install it. The EXE is unsigned; SHA-256 checksums verify matching bytes, rather than publisher identity or safety.

## Installation behavior

Both mod DLLs go into their proper folders: `BepInEx/plugins/ScamWYF.HostBoss.dll` and `BepInEx/core/ScamWYF.Modding.Core.dll`. The installer also sets up missing BepInEx files, Doorstop and compatibility overrides using pinned official setup code. The game's Managed DLLs are read as inputs and remain unchanged. A stale Input System override is backed up before generating compatible copies. Newer shared mod-lib versions are retained. Files it can change are snapshotted under `HostBoss/backup/setup-<id>`; a failed install attempts restoration and reports any file it could not restore. Logs remain in `%LOCALAPPDATA%/HostBoss/Setup/Logs`. The game must be closed; setup does not launch it or register startup tasks.

The loader's compatibility steps intentionally create patched copies of certain game assemblies under `unstripped_corlib`. These locally generated copies are not distributed. To disable HostBoss, close the game and disable it in the launcher's Mods tab; keep the shared library if other mods use it.

## Source and verification

Build/package the plugin, then run `HostBoss/build-setup.ps1`. The build requires a clean official Setup checkout at `02a97179b4db3e59be4d04850411595b170a85e3`, the checksum-verified official BepInEx 5.4.23.5 archive and Mono.Cecil 0.11.6. The build script documents its .NET Framework adaptations and disables the upstream network download method. Files, licenses, source links and checksums are visible in the extracted package. No game assemblies, saves, recordings or credentials are distributed.

`--self-check` validates distributed file hashes. `--ui-self-check` tests form initialization. `--extract <new-folder>` copies the visible setup files for inspection. `--install <game-folder>` runs full installation; `--mods-only <game-folder>` updates the two DLLs into an existing loader. CLI failures return nonzero and write to standard error. Run `tools/test-setup.ps1 -GameDir <game-folder>` for isolated copied-fixture integration tests; tests do not install into the supplied game directory.
