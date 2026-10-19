# HostBoss 1.60.4 setup: behavior and scan report

Checked October 18, 2026. These are observed scan results, not a guarantee of safety or vendor-confirmed false positives. The installer is unsigned. Do not disable antivirus to install it.

## Exact files

- Revised EXE SHA-256: `fb66e7e30ff60b7bf2a6e937834bef0036892f4a531dd037df3fa540d34e34c3`. [VirusTotal EXE report](https://www.virustotal.com/gui/file/fb66e7e30ff60b7bf2a6e937834bef0036892f4a531dd037df3fa540d34e34c3/detection): **2/71 detections**, McAfee Scanner (`Ti!FB66E7E30FF6`) and SecureAge (`Malicious`). Microsoft was undetected in that report. The other engines' results do not invalidate the two detections.
- Setup ZIP SHA-256: `d370a3f73c4e1b3768e988aaeaffc874678ab069187b3efe009a80116a851d6d`. [VirusTotal ZIP report](https://www.virustotal.com/gui/file/d370a3f73c4e1b3768e988aaeaffc874678ab069187b3efe009a80116a851d6d/detection): **0/68 archive detections**. **Archive scan limitation:** the upload form explicitly warned that this archive exceeds 3 MB and contains multiple files, so the contained files would not be scanned through that submission. A ZIP result cannot clear its contents; use the EXE's separate report above. The two reports describe different exact bytes.
- Withdrawn 1.60.3 EXE SHA-256: `8ab199b873107425c09fa9b32c43fa574092b85f28cd7d2016d0924064ad7eed`. [Previous report](https://www.virustotal.com/gui/file/8ab199b873107425c09fa9b32c43fa574092b85f28cd7d2016d0924064ad7eed/detection) showed **9/71 detections**: Arctic Wolf, Bkav Pro, CrowdStrike Falcon, Cynet, Elastic, McAfee Scanner, SecureAge, SentinelOne and Trapmine.

No samples were rebuilt after these exact EXE and ZIP submissions. Vendor results may change. No vendor has confirmed that the remaining detections are false positives, and no claim of malware removal is made without evidence of malware.

## Design changes

The earlier EXE embedded an archive, extracted files into a temporary directory and started hidden Windows PowerShell with process-scoped ExecutionPolicy Bypass. Although the intended purpose was to run loader setup scripts, that wrapper was unnecessary. The revised EXE has **zero embedded resources**, uses inspectable files in the extracted ZIP, and calls the official C# setup library through ordinary on-disk .NET references. There is no shell, script execution, dynamic Assembly.Load(byte[]) loader, process injection, startup registration, credential collection or security-setting change in the installer. Upstream download code is disabled in the compiled adaptation.

The source is pinned to official ScamWYF Setup commit `02a97179b4db3e59be4d04850411595b170a85e3`, with the documented Windows/.NET Framework adaptations in `build-setup.ps1`. Those changes remove shell execution and improve inspectability; they do not establish why a vendor flagged either build. Licenses and upstream links are shipped beside the binaries.

## Intended installation actions

The installer reads the Steam path and library list to find the game, requires it to be closed and validates the selected folder. It verifies package file hashes, snapshots files it can change under `HostBoss/backup/setup-<id>`, installs missing BepInEx and Doorstop files, seeds the Mono compatibility libraries and generates necessary patched copies under `unstripped_corlib`. Game Managed assemblies are read as inputs, not modified. It backs up an outdated Input System override. It installs HostBoss under `BepInEx/plugins` and the shared mod-lib under `BepInEx/core`, preserving a newer shared library. Failed installs attempt restoration and report any file they could not restore. Saves are retained. Logs go to `%LOCALAPPDATA%/HostBoss/Setup/Logs`.

This is a game mod loader, so it deliberately adds a loader DLL and runtime hooks to the game. Functional tests cannot certify all third-party binary behavior. The EXE and ZIP scans do not constitute an independent source audit or individual scans of every dependency.

## Verification and review

Passed final copied-fixture checks for a clean offline install, repeat install, exact Managed DLL preservation, saved-deck preservation, rollback after a locked-file failure, newer-core preservation, legacy-patch refusal, modified-payload refusal and GUI initialization. The plugin package checks and all 57 slide document tests also passed. Live multiplayer was not retested. Tests never installed into the user's running game.

Only the detecting vendors can adjudicate their detections. [VirusTotal's false-positive guidance](https://docs.virustotal.com/docs/false-positive) explains vendor review; an existing report or a checksum does not provide clearance. No vendor-review submissions were sent on the publisher's behalf.
