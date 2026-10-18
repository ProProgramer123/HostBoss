# HostBoss 1.60.0 BepInEx plugin

## Break-room laptop apps

Version 1.9.2 fixed invisible app text when opening the laptop before F1. App contents use opaque dark text, light buttons and readable text fields. Version 1.9.1 fixed app insertion on the game's stripped JSON runtime. Custom decks also remain usable when optional boss notes or review AI are unavailable: missing evidence is explicitly labeled, and AI failures keep the custom deck instead of silently leaving the default presentation active.

Open the character-creator laptop in the break room and launch **HostBoss** or **Slide Maker** from its desktop. Both use the laptop's native app windows, controls, scrolling, taskbar, minimize, maximize and close buttons. Guests see exactly **You must be host to use HostBoss apps!** in either app, and host status is checked on every action.

The laptop Slide Maker shares its draft, options, PNGs and saved decks with F6. **F6 stays available**, including its existing offline Slide Maker. Laptop apps require HostBoss installed on the player opening them. Restart after installing an update.

HostBoss 1.60.0 uses BepInEx 5.4.23.5 and the shared [ScamWYF mod-lib](https://github.com/swyf-modding/mod-lib) 1.1.0. It does not modify game DLLs on disk. Custom server naming has been removed; the other features remain.

## Install and switch from the patcher

1. Close the game. If you previously used HostBoss Patcher, choose **Remove / Normal Play** first, then restart the game after completing setup. The plugin refuses to enable when it detects legacy HostBoss references in the game assemblies.
2. Use the ScamWYF launcher's Setup tab to configure the game's supported BepInEx loader and required compatibility fixes.
3. The 1.60.0 ZIP contains two DLLs: put `ScamWYF.HostBoss.dll` in `BepInEx/plugins` and `ScamWYF.Modding.Core.dll` in `BepInEx/core`. The core library is shared and must never go in `plugins`. The launcher catalogue must route both files before installing this version through Get mods or Install from URL.
4. Launch the game and press **F6**. Slide Maker works in the main menu, lobby, during shifts and during reviews, including AI mode. Boss controls require hosting a lobby.

Use the launcher's Mods tab to disable or remove HostBoss while the game is closed. Restart for changes to take effect. The mod's own AI Boss toggle restores the native boss without uninstalling it. No separate legacy HostBoss runtime is required. Keep the shared core library if other mods use it. Mod-lib provides the F1 menu, F6 key registration, patch coordination and unload cleanup; HostBoss's page links to the slide maker and host controls.

## Features

Manual presentation start and slide controls; a full saved Slide Maker; quota display and optional override; individual firing/rehiring; separate built-in quote and recording buttons; Fire All cutscene without firing employees; Destroy Reputation and its stop/lobby action; AI Boss mode. The panel blocks gameplay input while open.

In HostBoss 1.6.0, edit up to three next-day goals in the F6 panel before starting the review presentation. Each goal allows 200 characters; blank goals are omitted. Start Presentation shares the goals through the game's built-in checklist slide. Show Next Day Goals jumps everyone to that slide. Goals reset after each review, on disconnect, or when switching boss modes. These are presentation goals and do not change quota or enforce objectives. The game's prestige completion slide is preserved.

## Slide Maker

Press **F6 → Slide Maker** anytime. The four templates come from the installed game's default review slides: Performance Review, Department Rankings, What the Boss Heard, and Tomorrow's Goals. Start with those slides, add from a template, duplicate, delete or reorder them. Edit titles, subtitles, quotes/callouts, headings, footers, theme, checklist text, metrics and ranking tables. A content preview appears below the fields; the game uses its native layout when presenting.

Use **Browse PNGs** or paste a local PNG path, then **Import PNG**. The image is embedded in the deck, preserving transparency, and resized to at most 512 pixels and 16 KB per image. The whole deck must fit 48 KB for the game's network messages; the editor reports its size and rejects oversized decks before publishing. Images can be removed or replaced. Source files can be up to 8 MB and 4096 pixels per side.

**Everyone viewing custom PNGs must have HostBoss 1.6.0 or newer.** They do not need your local image files. Text and the game's built-in artwork use the native renderer. The plugin only interprets embedded PNG data, without downloading remote URLs or writing images received from other players to disk.

Name the deck and press **Save**. Names can include ordinary punctuation and an optional `.json` extension. **Saved decks → Load selected deck** restores it. Saves are local JSON files under the game's user-data directory (`Application.persistentDataPath/HostBoss/Presentations`), with PNG data and all toggles included. The full saved path and success/error messages appear at the top of the editor. Saving the currently opened file updates it in one click and keeps the previous file as `.bak`; saving over a different existing filename requires the visible **Confirm overwrite** button. The last saved/loaded deck reopens next time. Existing saves under `BepInEx/config/HostBoss/Presentations` are copied into the new folder without overwriting files. Older flat JSON decks remain readable.

Save/load works throughout a session, even during a presentation. Draft edits stay separate from the presentation currently on screen, and saved decks survive game restarts and review completion. An offline draft can be saved even if it is currently too large to share; the network limit is enforced when applying the presentation. Version 1.8.0 stores options in a versioned save envelope, so earlier HostBoss versions cannot read new saves.

During a prepared manual review, before pressing Start Presentation, select **Use this deck for review**. Switch to **Host Controls → Start Presentation** to share it. **Restore default presentation** restores the generated review without deleting your draft or saved decks. Applied changes reset at the end of that review; the saved copy remains. Checklist goals in a custom deck are edited in Slide Maker rather than overwritten by the quick next-day-goals fields.

Saved decks are not automatically applied to every review. The host explicitly chooses when to use one. Recording buttons continue selecting the current day's built-in highlights, independently of the custom slide order.

On a Department Rankings / ranking slide, **Use built-in scammer rankings** switches the table to the game's current-day player ordering, earnings, grades and highlighted row when you apply the deck. Custom titles, quotes, theme and PNGs remain unchanged. The toggle is saved with the slide, including duplicates. Turn it off to edit and use your custom rows again; those rows remain saved while built-in mode is enabled. The preview shows a current-day-data placeholder while built-in mode is on. Reapply the saved deck in each review to pick up that day's rankings.

The template picker also includes the game's **The boss has reached a decision...** slide with its native verdict buildup layout. Add it like any other template, customize it, reorder it and save it with a deck. This is a presentation slide; adding it does not fire employees or queue Fire All.

## Native slides, announcements and camera positions

The Slide Maker has separate toggles for **Announce newly unlocked apps**, **Use the game's selected review slides**, and **Keep original boss TTS on built-in slides**. On a checklist slide, **Use real What the Boss Heard + the game's selected recording slides** replaces that placeholder with the actual notes and clips for the current day. The full-native option replaces the custom slide sequence with the game's chosen review sequence. Native firing slides are excluded; Fire All remains a separate host action.

Selected-review and real-heard modes call the same review AI API used by the game, then prepare its chosen recordings. This happens after applying the deck, while the presentation remains waiting for Start Presentation. A backend failure is reported in the editor and host controls; fabricated content is not substituted. App announcements come from the game's actual unlock slides and only appear when that review contains new unlocks. Turning native TTS off also removes boss introductions, reactions and fallback speech for selected clips, while keeping the caller recording itself.

Each custom slide has a **Boss cam position** selector: Auto, top-left, top-right, bottom-left or bottom-right. Auto restores the native placement. The choice saves with the slide and is shared in presentation metadata; viewers need HostBoss 1.8.0+ to honor custom corners. Expanded What the Boss Heard clips inherit the placeholder's selected corner. Full-native mode uses native slide identities and their default positions unless matching per-slide settings exist.

## Boss TTS

Each Slide Maker slide has **Boss TTS when this slide is shown** and an editable **Boss narration** field. Templates retain their built-in narration, including the boss decision template. You can generate narration from the slide's title and quote, write your own, or leave it blank for silence. Save/load and duplication preserve narration.

When the host starts the custom presentation, the game's own narrator speaks the current slide using its boss voice and broadcasts the audio. The previous line is canceled on a slide change. Manual slide advancement and host decisions remain manual; enabling slide TTS does not enable the AI boss. The native TTS engine and voice assets must be available. The game still controls voice availability and generation failures. Newer HostBoss is needed on the host; text narration uses the game's existing audio stream for viewers.

## Build

From the source checkout:

```powershell
.\HostBoss\restore-plugin-dependencies.ps1
.\HostBoss\restore-modlib.ps1 -GameDir 'C:\Path\To\Scam With Your Friends'
.\HostBoss\build-plugin.ps1 -GameDir 'C:\Path\To\Scam With Your Friends'
.\HostBoss\package-plugin.ps1
```

The build downloads no game files. Only the two build-reference DLLs are extracted from the pinned official BepInEx archive, after checking its SHA-256. Neither is included in the plugin release. Your installed game assemblies supply compiler references. Output is `HostBoss/dist/HostBoss-Plugin-1.60.0.zip`.

The plugin validates all eleven method targets before patching, removes its own Harmony patches if initialization fails, and refuses mixed legacy/plugin installation. It remains experimental: compilation, metadata, launcher installation, and structural checks do not replace live review and multiplayer testing. Antivirus detections from the legacy EXE do not establish the result for this distinct plugin binary; no clean verdict is claimed. MIT licensed; not affiliated with the game's developers.
