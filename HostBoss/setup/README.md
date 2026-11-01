# HostBoss Setup 1.60.6 — local Windows build

Extract the entire ZIP, keep its EXE, DLLs, `SHA256SUMS.txt`, `Source` and `SetupFiles` folders together, then run **HostBoss-Setup-1.60.6.exe**. Close the game, check the detected game folder and click **Install / Update**. Start the game, host a lobby and press **F6** afterward.

Includes HostBoss 1.60.6, the shared mod-lib, BepInEx 5.4.23.5 and the updated official loader repair code. It works offline on Windows 10/11 x64 with .NET Framework 4.8; no launcher or .NET SDK is needed to run it. If access is denied, close setup and use Windows **Run as administrator**. The installer does not request elevation automatically.

This version waits for recording receipts before freezing review evidence and requesting boss notes, and reuses the captured day's evidence when applying custom presentations. Enable the slide's "Use real What the Boss Heard" toggle, apply the presentation, and wait for preparation before starting. If the game selects no eligible moments, the slide explains that no notes or recordings are available. The loader repair tracks source hashes so stale compatibility overrides such as Mirror are rebuilt even when their assembly version has not changed. Files setup can change are backed up under `HostBoss/backup/setup-1.60.6-<id>`, and failures attempt rollback. Newer shared mod-lib versions are retained.

The EXE uses ordinary on-disk dependencies, has no embedded payload and launches no PowerShell or other process. All installed files are bundled and checked before installation. Logs are in `%LOCALAPPDATA%/HostBoss/Setup/Logs`. `--self-check` verifies the extracted package; `--install <game-folder>` performs full installation; `--mods-only <game-folder>` updates the two mod DLLs with an existing loader. The game must be closed for installation.

This is an unsigned local build. It has not been submitted to VirusTotal; previous versions' scan results do not cover it. Checksums establish file identity, not antivirus clearance. Do not disable antivirus. 

Installer and recovered HostBoss source are included under Source. HostBoss code is MIT licensed; the included loader and dependency notices describe their respective licenses. Build details and official setup source revision are in SetupFiles/PROVENANCE.txt. No game DLLs, saves or recordings are distributed. Recording-wait and slide-resolution regression checks passed; all direct game references resolve against 0.1.95. A full multiplayer review has not been played with this fix.
