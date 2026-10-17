# HostBoss patcher 1.4.3: behavior and antivirus review

This is an unsigned community game mod installer. Detections have not been confirmed as false positives by the detecting vendors. Do not disable antivirus to install it. Checksums verify that files match this package; they are not a publisher signature or proof that software is safe.

## Distribution changes

Version 1.4.3 replaces the single-file wrapper with a conventional multi-file Windows .NET Framework application. Mono.Cecil.dll loads from disk using the normal .NET loader. The EXE no longer contains embedded DLLs or scripts, registers no AssemblyResolve handler, and does not call Assembly.Load(byte[]). Sources and SHA256SUMS.txt are included. These changes improve inspectability; they do not establish why a vendor flagged the previous build or guarantee a clean scan.

Extract the entire ZIP. Keep HostBoss Patcher.exe, Mono.Cecil.dll, the HostBoss directory, and the optional launchers together. Run the EXE, choose the game folder, and use Install / Update or Remove / Normal Play. The graphical installer uses managed code directly; it does not invoke PowerShell or batch files.

## Intended behavior

- Reads Steam's install path from HKCU\Software\Valve\Steam and libraryfolders.vdf to find the game. This is read-only; no registry settings are changed.
- Checks the game process name to require closing the game before patching.
- Copies the visible package files into the selected game's HostBoss directory and optional Play Normal / Play Host Boss launchers into its root.
- Uses Mono.Cecil to modify Assembly-CSharp.dll and ScriptsAssDef.dll in the game's Managed directory, adding references and calls to HostBoss.Runtime.dll. Installs that runtime next to the game assemblies. This on-disk modification is the mod's purpose.
- Keeps original assembly backups and hashes in HostBoss/backup. Checks hashes before updating or restoring files to avoid overwriting changes by other mods or Steam updates.
- Launch game starts only the selected game's EXE. Optional, readable batch launchers invoke the supplied local PowerShell scripts with process-scoped ExecutionPolicy Bypass; this is not used by the EXE installer and does not change system execution policy.
- The installer has no network requests, downloads, process injection, scheduled tasks, startup registration, credential access, antivirus exclusions, or security-setting changes. It requests asInvoker privileges.
- The installed runtime operates inside the game and uses its existing Unity/Mirror multiplayer system. Its gameplay behavior is documented in README.md.

## Previous report and vendor review

Previous EXE SHA-256: 481f8754fa88722fd0173df025d889ac091bcb909dd08d3e938415e734691a8a.

Report: https://www.virustotal.com/gui/file/481f8754fa88722fd0173df025d889ac091bcb909dd08d3e938415e734691a8a/detection

On October 17, 2026 the report showed 8/71 detections: Arctic Wolf, Bkav Pro, Elastic, McAfee Scanner, Microsoft, SecureAge, SentinelOne (Static ML), and Trapmine. Microsoft labeled the previous build Trojan:Win32/Wacatac.B!ml. These are observations, not a vendor verdict of false-positive status.

Only the detecting vendors can correct their detections. VirusTotal guidance: https://docs.virustotal.com/docs/false-positive

Microsoft developer submissions: https://www.microsoft.com/en-us/wdsi/filesubmission

Suggested review description (attach the exact flagged file, its hash, and source through the vendor's official submission process):

"Please review this detection for a possible false positive. HostBoss Patcher is an unsigned, source-included .NET Framework installer for a local Unity game mod. It modifies two named managed game assemblies on disk with Mono.Cecil and preserves backups for removal. Its GUI does not download or execute scripts, inject into processes, alter security settings, or register persistence. Version 1.4.3 uses ordinary on-disk dependencies instead of the previous embedded DLL loader. Please analyze the submitted binary and source and advise whether the detection is warranted."

The new build has not been submitted automatically. An existing report for the old SHA-256 does not describe a newly built binary. A new scan and vendor review remain necessary. Release signing requires a legitimate code-signing certificate owned by the publisher; no certificate is fabricated or self-signed for this package.

## Local validation of this build

Refer to the distributed SHA256SUMS.txt for the exact hashes of a particular build. Rebuilding produces a separate binary and requires its own scan.

The package passed self-check, installation, repeat installation, exact removal/restoration, invalid-folder rejection, and incomplete-package rejection on a copied game fixture. The EXE has zero embedded managed resources; source inspection found no AssemblyResolve or in-memory Assembly.Load loader. Package hashes were checked against every distributed file.

A Microsoft Defender custom scan was attempted, but MpCmdRun returned 0x80004005 and its log reported 'Product/Feature disabled'. No scan completed. No antivirus settings were changed. These functional and structural checks are not an antivirus clearance.
