# HostBoss 1.60.6

Windows compatibility repair for game 0.1.95-swyf.

- Wait for the game's recording receipts before freezing evidence and requesting boss notes.
- Keep the captured day's evidence when expanding custom What the Boss Heard slides.
- Preserve native recording IDs, original TTS toggles and camera choices.
- Explain when the game selected no eligible call moments.
- Include the newer official offline setup repair, which detects stale same-version compatibility DLLs using source hashes.

Download and extract **HostBoss-Setup-1.60.6.zip**, keep all files together, close the game and run its EXE. Windows 10/11 x64 with .NET Framework 4.8. No launcher or SDK is needed to install. The EXE is not a single-file distribution. Setup is unsigned and this version has not been submitted to VirusTotal; older scan reports do not cover it.

For an existing loader, the plugin ZIP and loose DLL assets are available. Install `ScamWYF.HostBoss.dll` in `BepInEx/plugins` and the shared core in `BepInEx/core`; preserve a newer installed core. Use the launcher's Setup first for current game compatibility. Remove legacy disk patches before switching to the plugin.

## Current source and builds

The current plugin is `HostBoss/plugin`, recovered from the author's own saved 1.60.3 assembly after the local source checkout was lost, then repaired for 0.1.91–0.1.95. The older `HostBoss/src` files are retained for historical builds; they are not the 1.60.6 plugin. The current Windows installer is `HostBoss/setup`, including the adapted MIT upstream sources pinned at official Setup commit `033ea4d0a5c1a6019b6132f8811357adffb23fdf`.

With the .NET SDK and your own game installation containing BepInEx and mod-lib:

```powershell
./HostBoss/build-plugin.ps1 -GameDir 'C:\Path\To\Scam With Your Friends'
```

This only compiles into `HostBoss/bin/plugin`; it does not install the DLL. To rebuild the installer with the dependencies from an extracted release:

```powershell
./HostBoss/setup/build.ps1 -PackageDir 'C:\Path\To\HostBoss-Setup-1.60.6'
```

The installer uses bundled on-disk dependencies and no downloader or child processes. Its upstream adaptations are included for review. No game DLLs, recordings, logs or saves are published.

Validation: 13 replay compatibility checks, 6 slide-resolution checks and all 111 direct game/Mirror references passed against the current game. Isolated setup tests passed for install/reinstall, rollback, saved-file preservation, stale Mirror repair, package tamper rejection and newer shared core preservation. A full multiplayer review has not been played with this fix. The historical Linux 1.60.4 package is not refreshed by this release.
