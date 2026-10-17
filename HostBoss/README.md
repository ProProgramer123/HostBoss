# Host Boss runtime 1.4.2 / Patcher 1.4.3

A local mod for Scam With Your Friends. Version 1.4.2 builds against the currently installed game libraries. Live multiplayer verification on this version is still pending.

Host a game and press **F6**. The host takes over the end-of-day review:

- **AI Boss toggle:** enabling AI disables the manual host controls, cancels manual preparation, and restarts the current review at its beginning using the game's original AI generation, narration, slide advancement and timer. Toggle it off to restore the manual host presentation. Switching is disabled during the firing cutscene.
- **Start Presentation:** in manual mode, the review stays on its connecting/loading screen until you press this button. Slides and recordings prepare in the background, but the deck is only published when you start it. Fire All can queue beforehand; its cutscene waits for Start Presentation too. Refreshing recordings returns the presentation to the waiting state. AI mode keeps the game's automatic presentation behavior.
- **Edit presentation titles:** while the review is waiting to start, change the presentation title and individual slide titles in the F6 panel. Titles are shared with everyone through the deck when you press Start Presentation. Editing locks after starting. Reset titles to defaults is available before starting; titles also reset automatically at review completion or game end. Switching to AI or rebuilding the presentation discards the draft. Titles are kept only for this presentation, never saved to disk.
- Previous / next slide, synchronized through the game's existing network messages.
- Live quota, pooled money, remaining quota, and individual earnings.
- Set a custom quota. It stays active across shifts, including sandbox mode. **Auto** resumes automatic quotas at the next shift.
- End the shift and summon the review (after the day-zero tutorial).
- Finish the review and approve the team to continue, even after missing quota.
- Fire individual employees for the current shift; rehire them at any time. Fire requires a second click. The host cannot fire themselves.
- **Fire All** ends the shift if needed, plays the built-in **You're fired** review cutscene, leaving all employees in place without firing them. Confirm with a second click, or cancel before confirming. Finish the day-zero tutorial first. The review remains under host control afterward.
- **Show Quote** displays the selected built-in quote without playing its recording, and stops an active recording. **Play Recording** plays the selected native video/audio highlight separately, including highlights without an existing replay slide. A missing clip shows a retry message instead of leaving the button disabled. Browse with the quote arrows; click Play Recording again to replay it. The panel closes so you can watch. F6 reopens it; **Return to presentation** dismisses the quote/recording and resumes the deck. Use **Refresh recordings** during review to retry preparation.
- **DESTROY REPUTATION** unlocks after the Fire All cutscene. It continues into the game's native fire aftermath: screen flames, alarms, screams, lighting, player fire effects, and up to 128 ground-fire effects around the review room and office-floor starting areas. Effects last 30 seconds, lighting resets, and it can be triggered once per shift. The host retains control of when the review ends.
- **END DESTROY REPUTATION** stops the active sequences, broadcasts the game's native fire/audio cleanup event, restores lighting, and returns everyone to the lobby. It does not call the game's run-save reset method.
- Reviews have no AI generation, AI speech, automatic slide changes, skip votes, or timed ending on the modded host.
- Gameplay input is disabled while the panel is open and restored when it closes. F6 or Escape closes it.

## Install

Download **HostBoss-Patcher-1.4.3.zip**, extract the entire ZIP, and run **HostBoss Patcher.exe** from the extracted folder. Keep the adjacent **Mono.Cecil.dll** and **HostBoss** directory with it. Patcher 1.4.3 uses ordinary on-disk dependencies instead of embedded executable payloads. No .NET SDK or PowerShell command is needed for the graphical installer. Select the game folder (usually detected automatically), then click **Install / Update**. Use **Remove / Normal Play** to restore the original game files. **Launch game** starts the currently selected mode. Close the game before changing modes. The EXE uses Windows' .NET Framework; it does not download dependencies. Source files, SHA256SUMS.txt, and SECURITY.md are included. It remains unsigned; antivirus detections have not been confirmed or cleared by vendors.

This PC now has separate launchers in the game folder:

- **Play Normal.bat** restores the pre-HostBoss assemblies, then launches the game. Use this when joining other servers or diagnosing connection issues.
- **Play Host Boss.bat** enables the mod, then launches the game. Host your own lobby and press F6.

Close the game before switching. Steam's Play button uses whichever mode was selected most recently. The latest Steam update replaced the BetterSpeech DLL patches; its folder remains, but HostBoss does not reinstall it.

For another installation, extract the `HostBoss` folder beside `Scam With Your Friends.exe`, close the game, then run `HostBoss\install.bat`. Only the host needs the mod for the host controls. Guest compatibility has not yet been verified with a second player.

Individual firing means sending that employee to the lobby and keeping them there until rehired or the next day. They remain connected. Requests to add earnings through the game's normal payout method are refused while fired. Existing earnings remain. This is a custom per-shift penalty; the base game only implements team-wide firing. In-flight calls, guest synchronization, and unusual teleport interactions still need multiplayer playtesting.

The host override does not change sandbox mode into a quota-based win/loss mode. It supplies a displayed and synchronized target; sandbox keeps its normal game rules.

## Uninstall / updates

Close the game and run `HostBoss\uninstall.bat`. Backups are in `HostBoss\backup` and preserve any mods present at installation. Uninstall HostBoss **before** changing or uninstalling another DLL patch mod. The uninstaller refuses to overwrite modified HostBoss assemblies changed by another mod. If a game update already replaced the patched files, it leaves those updated files in place rather than restoring obsolete backups. Steam's Verify integrity can restore the game if needed; reinstall mods afterward.

After a game update, rerun the installer. It validates required methods before applying a new patch. Future game versions are not guaranteed compatible.

## Verification

Compiled against the installed game's stripped managed libraries. Version 1.0.0 was launched in game and its F6 panel, live quota, and employee list visually verified. Version 1.4.0 checks cover installation, branch targets, repeated installation, exact uninstall restoration, simulated Steam updates, migration of old guards, the native quote-only renderer hook, AI restart/timer calls, and end-fire/lobby calls. The standalone EXE has also been visually inspected and tested for installation/removal on a copied game. See `verification.txt`. These checks are not a live presentation or multiplayer gameplay test.

The quote-only fix uses a native replay-shaped slide and a dedicated native display hook. The previous button sent a replay message from non-replay slides, which the game silently rejected. Recording playback remains a separate action; quote-only display has no recording ID and explicitly releases video/audio streams.

The 1.1 custom transcript picker was removed. The review now waits for native recording metadata, freezes the native evidence packet, prepares native highlight IDs, and preserves `replayMoments` in the built-in slides. Automatic boss generation and narration remain disabled; recording audio is separate. Only the explicit Fire All cutscene temporarily permits its built-in firing line and effects.

## Source / build

Source is in `src`. Run `build.ps1` with the .NET SDK installed. Run restore-dependencies.ps1 to fetch the pinned Mono.Cecil 0.11.6 package from official NuGet; its SHA-256 is checked before extraction. When building outside the game folder, pass -GameDir with the installed game folder. It does not install BepInEx or Harmony. The runtime is built against the game's stripped libraries with no standard-library substitution.

Run `package.ps1` after building to generate the conventional EXE/DLL ZIP and SHA256SUMS.txt. See SECURITY.md for the package changes and vendor review information. The distributable contains no game assemblies. It patches `Assembly-CSharp.dll` and `ScriptsAssDef.dll` locally and adds `HostBoss.Runtime.dll`. Mono.Cecil is MIT licensed; see `lib\Mono.Cecil.LICENSE.txt`.

Not affiliated with the game's developers.
