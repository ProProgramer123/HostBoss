# HostBoss Setup EXE

Download **HostBoss-Setup-1.60.3.exe** from the GitHub release. This single EXE includes HostBoss, mod-lib, BepInEx 5.4.23.5, and the pinned official ScamWYF Setup scripts, Doorstop and Mono compatibility libraries. No launcher, SDK, separate ZIP extraction or internet connection is required to install. It requires Windows 10/11 x64, .NET Framework 4.8 and an installed copy of Scam With Your Friends. Checked against game 0.1.81-swyf.

1. Close the game.
2. Run the setup EXE. Check the detected game folder, or click **Browse** and select `Scam With Your Friends.exe`.
3. Click **Install / Update** and wait for **Setup complete**.
4. Start the game, host a lobby and press **F6**.

If access is denied, close setup and use Windows **Run as administrator**, then select the same game folder. Setup does not automatically request elevation. The EXE is unsigned; a published SHA-256 verifies the downloaded file matches the release, rather than certifying antivirus clearance.

The setup installs both DLLs into their correct folders: `BepInEx/plugins/ScamWYF.HostBoss.dll` and `BepInEx/core/ScamWYF.Modding.Core.dll`. Newer shared core versions are retained. Existing files it may change are backed up under `HostBoss/backup/setup-<id>`. Failed installations attempt to restore those files; any failed restoration reports its backup path. Saved presentations and the game's Managed assemblies are not modified. The included update repair moves an outdated Input System override to backup. Remove legacy HostBoss disk patches through the old patcher's **Remove / Normal Play** before installing this plugin.

Logs are retained in `%LOCALAPPDATA%/HostBoss/Setup/Logs`. The setup extracts its embedded files to a unique temporary folder, invokes Windows PowerShell with a process-local execution-policy argument, and cleans up its own temporary folder. It does not change system execution policy, security settings, or start the game. Setup cannot be closed while installation is running. To disable HostBoss afterward, close the game and disable it in the launcher's Mods tab; keep the shared library if other mods use it.

## Inspect and build

`HostBoss-Setup-1.60.3.exe --extract C:\New\InspectionFolder` extracts the payload without installing it. `--self-check` verifies the embedded archive. `--install <game-folder>` runs full setup; `--mods-only <game-folder>` installs the two DLLs into an already configured loader. Command-line failures return a nonzero exit code and write diagnostics to standard error.

Build/package the plugin first, then run `HostBoss/build-setup.ps1`. It checks official Setup commit `02a97179b4db3e59be4d04850411595b170a85e3` and the pinned BepInEx archive SHA-256 before compiling with Windows' .NET Framework compiler. `-SetupSource` can select a clean checkout at that exact commit. One documented patch makes the upstream game-folder resolver use the explicitly selected folder before probing Steam libraries, avoiding failures on PCs without a D: drive. The patch is included in the build script. No game DLLs enter the embedded payload. Licenses and upstream source links are included in `SOURCES-AND-LICENSES.txt` and `SETUP-THIRD-PARTY.txt`; inspect them through `--extract`. The old disk patcher source remains separate.
