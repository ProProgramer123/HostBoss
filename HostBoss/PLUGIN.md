# HostBoss 1.5.0 BepInEx plugin

HostBoss's host-controlled boss presentation, packaged as `ScamWYF.HostBoss.dll` for the ScamWYF launcher. This is an alternative to the legacy disk patcher. The plugin uses BepInEx 5.4.23.5 and its bundled Harmony runtime; it does not modify game DLLs on disk or run an installer script.

## Install and switch from the patcher

1. Close the game. If you previously used HostBoss Patcher, choose **Remove / Normal Play** first, then restart the game after completing setup. The plugin refuses to enable when it detects legacy HostBoss references in the game assemblies.
2. Use the ScamWYF launcher's Setup tab to configure the game's supported BepInEx loader and required compatibility fixes.
3. Install HostBoss from Get mods when its catalogue entry is merged, or use Install from URL with this repository's `HostBoss-Plugin-1.5.0.zip` release asset. For manual installation, copy only `ScamWYF.HostBoss.dll` into `BepInEx/plugins`.
4. Launch the game, host your own lobby, and press **F6**. Guest-only sessions keep the native game behavior.

Use the launcher's Mods tab to disable or remove HostBoss while the game is closed. Restart for changes to take effect. The mod's own AI Boss toggle restores the native boss without uninstalling it. No separate HostBoss runtime, Mono.Cecil, or shared mod-lib DLL is required by the plugin; BepInEx and Harmony are provided by the supported loader setup.

## Features

Manual presentation start and slide controls; editable titles before starting; quota display and optional override; individual firing/rehiring; separate built-in quote and recording buttons; Fire All cutscene without firing employees; Destroy Reputation and its stop/lobby action; AI Boss mode. The panel blocks gameplay input while open.

## Build

From the source checkout:

```powershell
.\HostBoss\restore-plugin-dependencies.ps1
.\HostBoss\build-plugin.ps1 -GameDir 'C:\Path\To\Scam With Your Friends'
.\HostBoss\package-plugin.ps1
```

The build downloads no game files. Only the two build-reference DLLs are extracted from the pinned official BepInEx archive, after checking its SHA-256. Neither is included in the plugin release. Your installed game assemblies supply compiler references. Output is `HostBoss/dist/HostBoss-Plugin-1.5.0.zip`.

The plugin validates all ten method targets before patching, removes its own Harmony patches if initialization fails, and refuses mixed legacy/plugin installation. It remains experimental: compilation, metadata, launcher installation, and structural checks do not replace live review and multiplayer testing. Antivirus detections from the legacy EXE do not establish the result for this distinct plugin binary; no clean verdict is claimed. MIT licensed; not affiliated with the game's developers.
