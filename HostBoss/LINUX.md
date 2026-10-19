# HostBoss for Linux x64 (Steam Proton)

Experimental Linux installer for the Windows game running through Steam Proton. This is not a native Linux port of the game. The mod DLL is the same HostBoss 1.60.4 used on Windows. The Linux command-line installer includes its .NET runtime and visible setup files; no system .NET, Wine-based installer, launcher, PowerShell or internet download is needed to run setup. Requires a glibc-based Linux x64 system supported by .NET 10, Steam, Proton and the installed game. ARM64 and musl/Alpine builds are not included.

This package was cross-built on Windows. Installer logic was tested against copied game files, but the Linux executable and live Proton gameplay could not be run here. Treat Linux support as experimental. Existing Windows EXE VirusTotal reports do not cover this Linux binary or its additional runtime files.

## Install

1. In Steam, enable a Proton compatibility tool for the game and install the Windows game. Close it before installing the mod.
2. Download `HostBoss-Linux-1.60.4-x64.tar.gz` and extract it:

```bash
tar -xzf HostBoss-Linux-1.60.4-x64.tar.gz
cd HostBoss-Linux-1.60.4-x64
chmod +x hostboss-setup
./hostboss-setup --self-check
```

3. Find the game folder using **Steam > game Properties > Installed Files > Browse**. Pass that exact folder to setup:

```bash
./hostboss-setup --game-dir "$HOME/.local/share/Steam/steamapps/common/Scam With Your Friends"
```

Use your actual library path if different, including Flatpak Steam or another drive. The folder must contain `Scam With Your Friends.exe`. Run setup as your Steam user, without `sudo`. Keep all extracted runtime files and the `SetupFiles` folder beside the binary.

4. In **Steam > game Properties > General > Launch Options**, use:

```text
WINEDLLOVERRIDES="winhttp=n,b" %command% -whisper-cpu
```

The `winhttp=n,b` setting lets Proton load Doorstop from the game folder. The CPU voice flag follows the pinned official setup's Proton guidance. Preserve any other launch arguments you need after `%command%`; merge an existing `WINEDLLOVERRIDES` value rather than adding a conflicting second one. The installer prints this option and does not edit Steam settings or the Proton registry.

5. Start through Steam, host a lobby and press **F6**. Existing F6 and laptop apps work through the same plugin. Repeated setup keeps saved presentations and backs up files under the game's `HostBoss/backup`. `--mods-only` updates the two DLLs when the loader is already configured.

Logs go to `$XDG_STATE_HOME/hostboss/setup`, or `~/.local/state/hostboss/setup` when that variable is unset. If mods do not load, check the game's `BepInEx/LogOutput.log`, the launch option, and that Steam is running the game through Proton. `Permission denied` can mean a missing executable bit or a `noexec` extraction location; extract into a directory where your user can execute programs. This installer requires no changes to antivirus or system security settings.

## Build

Build the Windows 1.60.4 package first to stage its verified common files, then run `build-linux-setup.ps1 -SetupSource <clean pinned Setup checkout>` with .NET SDK 10 and Windows tar available. It publishes `linux-x64`, self-contained, without single-file packing. Source adaptations are generated into ignored build directories. Official Setup sources are pinned to `02a97179b4db3e59be4d04850411595b170a85e3`; the downloader is disabled and all installed files come from the package. Third-party notices and .NET runtime licenses remain visible in the archive. Game DLLs are read only on the installing user's machine, never redistributed.
