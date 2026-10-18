using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kolkata.UI;
using ScamWYF.Modding.Core;
using ScamWYF.Modding.Core.Content;
using ScamWYF.Modding.Core.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace HostBoss {
// The OS owns the windows, taskbar, focus, resize and laptop input blocking.
// Both front ends use the same BossPanel and SlideMaker, including the same draft.
public sealed class LaptopApps {
    public const string ControlsId = "hostboss-controls";
    public const string MakerId = "hostboss-slide-maker";
    public const string HostRequired = "You must be host to use HostBoss apps!";
    public static bool Active;
    static readonly List<WeakReference> views = new List<WeakReference>();
    public static bool Owns(string id) { return id == ControlsId || id == MakerId; }
    // mod-lib 1.1's reflective JSON helper asks for a stripped two-argument overload.
    // Use the retained overload for these two apps, without changing the shared library.
    public static void EnsureDefinitions(object controller) {
        if (!Active || controller == null) return;
        var definitions = (System.Collections.IList)Hooks.Get(controller, "m_appDefinitions");
        Type definitionType = controller.GetType().Assembly.GetType("Kolkata.UI.KolkataOsAppDefinition");
        foreach (var entry in ContentRegistry.RegisteredApps) {
            if (!Owns(entry.Id)) continue;
            bool exists = false;
            foreach (object definition in definitions) if ((string)Hooks.Get(definition, "id") == entry.Id) { exists = true; break; }
            if (exists) continue;
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(entry.Spec);
            object app = Newtonsoft.Json.JsonConvert.DeserializeObject(json, definitionType, null);
            if (app == null) throw new InvalidOperationException("Could not deserialize " + entry.Id);
            Hooks.Call(controller, "NormalizeDefinition", app);
            definitions.Add(app);
            Debug.Log("[HostBoss] Added laptop app definition: " + entry.Id);
        }
    }
    public static void Register(ScamMod owner) {
        Active = true;
        foreach (bool maker in new[] { false, true }) {
            if (!ContentRegistry.RegisterApp(owner, new OsAppSpec {
                Id = maker ? MakerId : ControlsId, Title = maker ? "Slide Maker" : "HostBoss",
                IconResource = maker ? "KolkataOS/SVG/files" : "KolkataOS/SVG/script-text",
                IconSvg = maker ? "svg/files.svg" : "svg/script-text.svg", ContentType = "placeholder", ShowOnDesktop = true, DesktopOrder = maker ? 96 : 95,
                WindowLeft = 110, WindowTop = 55, WindowWidth = 850, WindowHeight = 640,
                MinWindowWidth = 550, MinWindowHeight = 400, AccentColor = "#376FC4",
                InitialState = "closed", Access = new OsAppAccessSpec { Mode = "default", UnlockDay = 0 },
                Content = new OsAppContentSpec { Title = maker ? "Slide Maker" : "HostBoss" }
            })) throw new InvalidOperationException("Could not register HostBoss laptop app.");
        }
    }
    public static void Build(object app) {
        var content = (VisualElement)Hooks.Get(app, "WindowContent"); if (content == null) return;
        foreach (var reference in views.ToArray()) {
            var old = reference.Target as LaptopView;
            if (old != null && old.Uses(content)) { old.Stop(); views.Remove(reference); }
        }
        content.Clear();
        Hooks.Set(app, "NeedsAutoSize", false);
        var view = new LaptopView(content, (string)Hooks.Get(Hooks.Get(app, "Definition"), "id") == MakerId);
        views.RemoveAll(reference => !reference.IsAlive);
        views.Add(new WeakReference(view));
        view.Start();
        Hooks.Set(app, "ContentBuilt", true);
    }
    public static void Shutdown() {
        Active = false;
        foreach (var reference in views) { var view = reference.Target as LaptopView; if (view != null) view.Stop(); }
        views.Clear();
    }
}

public sealed class LaptopView {
    readonly VisualElement root;
    readonly bool maker;
    readonly List<Action> refresh = new List<Action>();
    IVisualElementScheduledItem schedule;
    VisualElement body;
    Label notice;
    bool authorized;
    bool stopped;
    Func<bool> stale;
    public LaptopView(VisualElement root, bool maker) { this.root = root; this.maker = maker; }
    public bool Uses(VisualElement element) { return root == element; }
    public void Start() { Rebuild(); schedule = root.schedule.Execute(Tick).Every(250); }
    public void Stop() { stopped = true; schedule?.Pause(); root.Clear(); root.Add(new Label("HostBoss is disabled.")); }
    public bool CanUse { get { return !stopped && LaptopApps.Active && Hooks.IsHost(); } }
    public void Run(Action action, bool rebuild = false) {
        if (!CanUse || (stale != null && stale())) { Rebuild(); return; }
        try { action(); if (rebuild) Rebuild(); else Tick(); }
        catch (Exception e) { notice.text = e.Message; Debug.LogWarning("[HostBoss] Laptop: " + e.Message); }
    }
    public void Rebuild() {
        refresh.Clear(); stale = null; root.Clear(); authorized = CanUse;
        // Laptop apps can open before F1 initializes the shared widget palette.
        // Its unsampled colours are transparent and its row height is zero.
        if (UiTheme.RowHeight < 1) UiTheme.Sample(root);
        root.style.flexGrow = 1; root.style.minHeight = 0;
        if (!authorized) { root.Add(new Label(LaptopApps.HostRequired)); StyleLaptop(root); return; }
        Hooks.Init();
        notice = Widgets.Paragraph(root, "");
        var scroll = Widgets.Scroll(root); body = Widgets.Column(scroll);
        body.style.paddingLeft = 8; body.style.paddingRight = 8; body.style.paddingBottom = 12;
        try {
            if (maker) Hooks.Panel.BuildLaptopMaker(this, body);
            else Hooks.Panel.BuildLaptopControls(this, body);
        } catch (Exception e) {
            notice.text = "HostBoss app could not open: " + e.Message;
            Debug.LogError("[HostBoss] Laptop app: " + e);
        }
        StyleLaptop(root);
        Tick();
    }
    // Use explicit colours inside the OS's light windows. Shared menu widgets normally
    // inherit a dark overlay palette, which is not suitable for these app contents.
    public static void StyleLaptop(VisualElement element) {
        var ink = new Color(0.08f, 0.12f, 0.19f, 1f);
        element.style.color = ink;
        if (element is TextElement) { element.style.fontSize = 14; element.style.whiteSpace = WhiteSpace.Normal; }
        var button = element as Button;
        if (button != null) {
            button.style.height = 32; button.style.minHeight = 32; button.style.flexShrink = 0;
            button.style.backgroundColor = element.ClassListContains("hostboss-selected") ? new Color(.74f, .84f, .98f, 1) : new Color(.90f, .93f, .97f, 1);
            button.style.borderLeftWidth = button.style.borderRightWidth = button.style.borderTopWidth = button.style.borderBottomWidth = 1;
            button.style.borderLeftColor = button.style.borderRightColor = button.style.borderTopColor = button.style.borderBottomColor = new Color(.60f, .67f, .77f, 1);
        }
        var field = element as TextField;
        if (field != null) { field.style.flexShrink = 0; field.style.minHeight = field.multiline ? 80 : 32; }
        if (element.ClassListContains("unity-text-field__input")) {
            element.style.backgroundColor = Color.white;
            element.style.color = ink;
            element.style.paddingLeft = element.style.paddingRight = 6;
        }
        if (element is Toggle) { element.style.height = StyleKeyword.Auto; element.style.minHeight = 32; element.style.flexShrink = 0; }
        for (int i = 0; i < element.childCount; i++) StyleLaptop(element[i]);
    }
    void Tick() {
        if (stopped) return;
        if (authorized != CanUse) { Rebuild(); return; }
        if (!authorized) return;
        if (stale != null && stale()) { Rebuild(); return; }
        foreach (var update in refresh.ToArray()) update();
    }
    public void Watch(Action action) { refresh.Add(action); }
    public void RebuildWhen(Func<bool> condition) { stale = condition; }
    public Label Live(VisualElement parent, Func<string> text) {
        var label = Widgets.Paragraph(parent, text()); Watch(() => label.text = text()); return label;
    }
    public Button Button(VisualElement parent, string text, Action action, Func<bool> enabled = null, bool rebuild = false) {
        var button = Widgets.Button(parent, text, () => Run(() => { if (enabled == null || enabled()) action(); }, rebuild));
        if (enabled != null) { Watch(() => button.SetEnabled(CanUse && enabled())); button.SetEnabled(enabled()); }
        return button;
    }
    public TextField Field(VisualElement parent, string title, string text, Action<string> change, int max = 200, bool multiline = false) {
        var field = Widgets.TextField(parent, title, text, value => Run(() => change(value)), multiline);
        field.maxLength = max;
        if (multiline) field.style.minHeight = 72;
        return field;
    }
    public void Toggle(VisualElement parent, string title, Func<bool> value, Action<bool> change, bool rebuild = false, Func<bool> enabled = null) {
        var toggle = Widgets.Toggle(parent, title, value(), next => Run(() => { if (enabled == null || enabled()) change(next); }, rebuild));
        Watch(() => { toggle.SetValueWithoutNotify(value()); toggle.SetEnabled(enabled == null || enabled()); });
    }
    public void Choices(VisualElement parent, string title, string current, string[] values, Action<string> change) {
        Widgets.Heading(parent, title); var row = Widgets.WrapRow(parent);
        foreach (string value in values) {
            var button = Button(row, value, () => change(value), rebuild: true);
            Widgets.SetSelected(button, value == current);
            button.EnableInClassList("hostboss-selected", value == current);
        }
    }
}

public sealed partial class BossPanel {
    public void BuildLaptopMaker(LaptopView ui, VisualElement root) { maker.BuildLaptop(ui, root, this); }
    public void BuildLaptopControls(LaptopView ui, VisualElement root) {
        Widgets.Heading(root, "HOSTBOSS");
        ui.Live(root, () => Status);
        ui.Toggle(root, "Enable AI Boss", () => !ManualBossEnabled, SetAiBoss, enabled: () => !FiringCutscene);
        Func<ScammerGameManager> game = () => ScammerGameManager.Instance;
        Func<bool> manual = () => game() != null && ManualBossEnabled;
        Func<bool> review = () => manual() && game().IsReviewPhase;
        Func<bool> ready = () => review() && !preparing && !presentationStarted && nativeDeck != null && preparedDay == game().CurrentDay;
        Func<bool> presenting = () => review() && presentationStarted && !preparing && !FiringCutscene;
        ui.Live(root, () => game() == null ? "Waiting for game manager..." : "Day " + game().CurrentDay + " | Quota: $" + game().MoneyPool + " / $" + game().Quota);
        ui.Live(root, () => preparing ? "Preparing presentation..." : presentationStarted ? "Presentation started" : "Connecting — click Start Presentation when ready.");
        ui.Button(root, "Start Presentation", StartPresentation, ready);
        ui.Button(root, "Restore default presentation", RestoreDefaultTitles, () => ready() && defaultReviewDeck != null);
        var row = Widgets.WrapRow(root);
        ui.Button(row, "Previous slide", () => MoveSlide(-1), presenting);
        ui.Button(row, "Next slide", () => MoveSlide(1), presenting);
        ui.Live(root, () => Board == null ? "Slides loading..." : "Slide " + (Board.CurrentSlideIndex + 1) + " / " + Board.SlideCount);
        ui.Field(root, "Quota override", quotaText, value => quotaText = value, 9);
        ui.Button(root, "Set quota", () => {
            int value; if (!int.TryParse(quotaText, out value) || value < 1 || value > 100000000) { Status = "Enter a quota from 1 to 100,000,000."; return; }
            customQuota = value; ApplyQuota(game()); Status = "Quota set to $" + value + ".";
        }, manual);
        ui.Button(root, "Automatic quota next shift", () => { customQuota = -1; Status = "Automatic quotas resume at the next shift."; }, manual);
        Widgets.Heading(root, "Next day goals");
        for (int i = 0; i < nextDayGoals.Length; i++) {
            int index = i; var field = ui.Field(root, "Goal " + (i + 1), nextDayGoals[i], value => { if (ready() && !madeDeckActive) nextDayGoals[index] = value; });
            ui.Watch(() => { field.SetEnabled(ready() && !madeDeckActive); if (!field.focusController?.focusedElement?.Equals(field) ?? true) field.SetValueWithoutNotify(nextDayGoals[index]); });
        }
        ui.Button(root, "Show Next Day Goals", () => { int index = Array.IndexOf(nativeDeck.slides, goalsSlide); if (index >= 0) Hooks.Call(Board, "ServerPublishSlideIndex", index, false); }, () => presenting() && goalsSlide != null && nativeDeck != null);
        ui.Button(root, "Reset goals to defaults", ResetNextDayGoals, () => ready() && !madeDeckActive, true);
        Widgets.Heading(root, "Quotes and recordings");
        ui.Live(root, () => quotes.Count == 0 ? "No built-in recordings ready." : (quoteIndex + 1) + " / " + quotes.Count + " | " + quotes[quoteIndex].playerName + "\n" + quotes[quoteIndex].quote);
        ui.Button(root, "Refresh recordings", () => PrepareReview(Board, game().CurrentDay, true), () => review() && !preparing && !FiringCutscene && Board != null);
        row = Widgets.WrapRow(root);
        Func<bool> clips = () => presenting() && quotes.Count > 0;
        ui.Button(row, "Previous quote", () => quoteIndex = (quoteIndex + quotes.Count - 1) % quotes.Count, clips);
        ui.Button(row, "Show Quote", ShowQuote, clips);
        ui.Button(row, "Play Recording", PlayRecording, clips);
        ui.Button(row, "Next quote", () => quoteIndex = (quoteIndex + 1) % quotes.Count, clips);
        ui.Button(root, "Return to presentation", ReturnToPresentation, presenting);
        Widgets.Heading(root, "Employees");
        ui.Button(root, "Refresh employees", () => {}, rebuild: true);
        foreach (PlayerName player in PlayerName.ServerInstances) {
            if (player == null) continue;
            var p = player;
            ui.Live(root, () => p == null ? "Disconnected" : p.playerName + " | $" + (game() == null ? 0 : game().GetEarnedMoney(p)) + (IsFired(p.netId) ? " [FIRED]" : ""));
            ui.Button(root, "Fire / confirm fire: " + p.playerName, () => {
                if (selected == p.netId) Fire(p); else { selected = p.netId; Status = "Click again to confirm firing " + p.playerName + "."; }
            }, () => manual() && game().ScammerGameStarted && !FiringCutscene && p != null && !p.isLocalPlayer && !p.isHostPlayer && !IsFired(p.netId));
            ui.Button(root, "Rehire: " + p.playerName, () => {
                fired.Remove(p.netId); var tp = p.GetComponent<PlayerTeleporter>();
                if (tp != null) tp.ServerTeleportToPosition(PlayerModeManager.LocalPlayerInstance.transform.position, "Rehired by host", "HostBoss");
            }, () => manual() && p != null && IsFired(p.netId));
        }
        ui.Live(root, () => confirmFireAll ? "Click Fire All again to confirm the cutscene. Nobody will be fired." : "Fire All plays the cutscene only.");
        ui.Button(root, "Fire All", () => { if (confirmFireAll) FireAll(); else confirmFireAll = true; }, () => manual() && game().ScammerGameStarted && !FiringCutscene && !fireAllQueued);
        ui.Button(root, "Cancel Fire All", () => confirmFireAll = false);
        ui.Button(root, "DESTROY REPUTATION", DestroyReputation, () => review() && fireAllCompleted && !FiringCutscene && !reputationDestroyed);
        ui.Button(root, "END DESTROY REPUTATION / return everyone to lobby", EndDestroyReputation, () => manual() && (reputationDestroyed || chaosActive));
        ui.Button(root, "End shift / summon review", () => game().ServerEndCurrentDayEarly(), () => manual() && game().ScammerGameStarted && !game().IsReviewPhase && !game().DayZeroTutorialActive);
        ui.Button(root, "Finish review / approve next day", FinishReview, () => presenting() && !fireAllQueued);
    }
}

public sealed partial class SlideMaker {
    public void BuildLaptop(LaptopView ui, VisualElement root, BossPanel panel) {
        EnsureLoaded(); var doc = Document;
        var openedDeck = doc.Deck; int openedSelection = doc.Selected;
        var openedSlide = doc.Deck.slides[doc.Selected]; string openedSlideId = openedSlide.id;
        ui.RebuildWhen(() => Document != doc || doc.Deck != openedDeck || doc.Selected != openedSelection || doc.Deck.slides[doc.Selected] != openedSlide || openedSlide.id != openedSlideId);
        Widgets.Heading(root, "SLIDE MAKER");
        ui.Live(root, () => (Document.Dirty ? "Unsaved changes | " : "Saved / unchanged | ") + message);
        ui.Live(root, () => panel.Status);
        ui.Field(root, "Presentation filename", name, value => name = value, 69);
        ui.Button(root, "Save / confirm overwrite", () => {
            string path = Document.FilePath(name);
            if (File.Exists(path) && !string.Equals(currentFile, path, StringComparison.OrdinalIgnoreCase) && !Confirm("save:" + name, "This name exists. Click Save again to confirm overwrite.")) return;
            Document.Save(name); name = Path.GetFileNameWithoutExtension(path); currentFile = path;
            File.WriteAllText(lastFile, name); RefreshLibrary(); pendingAction = null; message = "SAVED: " + path;
        });
        ui.Button(root, "Refresh saved decks", RefreshLibrary, rebuild: true);
        RefreshLibrary();
        foreach (string savedName in saved) {
            string file = savedName;
            ui.Button(root, "Load: " + file, () => {
                if (Document.Dirty && !Confirm("load:" + file, "Click Load again to discard unsaved changes, or Save first.")) return;
                Document.Load(file); name = file; currentFile = Document.FilePath(file); File.WriteAllText(lastFile, file);
                pendingAction = null; message = "Loaded " + file;
            }, rebuild: true);
        }
        ui.Button(root, "New from default slides", () => {
            if (Document.Dirty && !Confirm("new", "Click New again to discard unsaved changes, or save first.")) return;
            Document.Set(defaults); name = "My Presentation"; currentFile = null; pendingAction = null;
        }, rebuild: true);
        ui.Button(root, "Cancel pending overwrite / load", () => { pendingAction = null; message = "Cancelled."; });
        ui.Button(root, "Use this deck for review", () => panel.ApplyMadeDeck(Document), () => panel.CanApplyMadeDeck);
        Widgets.Paragraph(root, "Edits and saves are available anytime. Apply while the manual review is connecting, then Start Presentation in HostBoss.");
        Action dirty = () => doc.Dirty = true;
        var options = doc.Options;
        ui.Toggle(root, "Announce new apps with built-in slides", () => options.AnnounceApps, value => { options.AnnounceApps = value; dirty(); });
        ui.Toggle(root, "Use the game's selected review slides", () => options.UseNativeSlides, value => { options.UseNativeSlides = value; dirty(); });
        ui.Toggle(root, "Keep original boss TTS on built-in slides", () => options.NativeTts, value => { options.NativeTts = value; dirty(); });
        ui.Field(root, "Presentation title", doc.Deck.title, value => { doc.Deck.title = value; dirty(); }, 160);
        ui.Field(root, "Presenter", doc.Deck.presenterName, value => { doc.Deck.presenterName = value; dirty(); }, 160);
        Widgets.Heading(root, "Slides");
        var row = Widgets.WrapRow(root);
        for (int i = 0; i < doc.Deck.slides.Length; i++) {
            int index = i; var button = ui.Button(row, (i + 1) + ". " + doc.Deck.slides[i].title, () => { doc.Selected = index; pendingAction = null; }, rebuild: true);
            Widgets.SetSelected(button, index == doc.Selected);
            button.EnableInClassList("hostboss-selected", index == doc.Selected);
        }
        row = Widgets.WrapRow(root);
        ui.Button(row, "Move up", () => doc.Move(-1), rebuild: true);
        ui.Button(row, "Move down", () => doc.Move(1), rebuild: true);
        ui.Button(row, "Duplicate", () => doc.Add(doc.Deck.slides[doc.Selected]), rebuild: true);
        ui.Button(row, "Delete", doc.Remove, rebuild: true);
        Widgets.Heading(root, "Default templates");
        foreach (var item in templates) {
            var template = item; row = Widgets.WrapRow(root);
            ui.Button(row, "Add: " + item.title, () => doc.Add(template), rebuild: true);
            ui.Button(row, "Replace selected", () => { if (Confirm("replace:" + doc.Selected + ":" + template.id, "Click Replace again to reset the selected slide.")) doc.Replace(template); }, rebuild: true);
        }
        var slide = doc.Deck.slides[doc.Selected]; var slideOptions = options.For(slide.id);
        Widgets.Heading(root, "Slide " + (doc.Selected + 1) + " | " + slide.layout);
        ui.Choices(root, "Boss cam position", slideOptions.Camera, new[] { "auto", "top-left", "top-right", "bottom-left", "bottom-right" }, value => { slideOptions.Camera = value; dirty(); });
        if (slide.layout == "checklist") ui.Toggle(root, "Use real What the Boss Heard and chosen recording slides", () => slideOptions.NativeHeard, value => { slideOptions.NativeHeard = value; dirty(); });
        ui.Field(root, "Title", slide.title, value => { slide.title = value; dirty(); }, 160);
        ui.Field(root, "Heading label", slide.kicker, value => { slide.kicker = value; dirty(); }, 160);
        ui.Field(root, "Subtitle", slide.subtitle, value => { slide.subtitle = value; dirty(); }, 400);
        ui.Field(root, "Quote / callout", slide.quote, value => { slide.quote = value; dirty(); }, 1200, true);
        ui.Toggle(root, "Boss TTS when shown", () => !string.IsNullOrWhiteSpace(slide.narration), value => { slide.narration = value ? SlideDocument.TextForSpeech(slide) : ""; dirty(); }, true);
        ui.Field(root, "Boss narration (blank = silent)", slide.narration, value => { slide.narration = value; dirty(); }, 1200, true);
        ui.Button(root, "Use title and quote as narration", () => { slide.narration = SlideDocument.TextForSpeech(slide); dirty(); }, rebuild: true);
        ui.Field(root, "Footer", slide.footer, value => { slide.footer = value; dirty(); }, 400);
        ui.Choices(root, "Theme", slide.theme, new[] { "white", "blue", "red" }, value => { slide.theme = value; dirty(); });
        if (slide.metrics != null) foreach (var m in slide.metrics) {
            var metric = m;
            ui.Field(root, "Metric label", metric.label, value => { metric.label = value; dirty(); }, 80);
            ui.Field(root, "Metric value", metric.value, value => { metric.value = value; dirty(); }, 120);
            ui.Field(root, "Metric detail", metric.detail, value => { metric.detail = value; dirty(); }, 200);
        }
        if (slide.bullets != null || slide.layout == "checklist") {
            string lines = string.Join("\n", (slide.bullets ?? new ReviewBulletDefinition[0]).Select(b => b.text).ToArray());
            ui.Field(root, "Checklist (one item per line)", lines, value => {
                slide.bullets = value.Replace("\r", "").Split('\n').Take(8).Select(line => new ReviewBulletDefinition { text = line, tone = "neutral", iconSvgPath = "kolkata-review/svg/check.svg" }).ToArray(); dirty();
            }, 1600, true);
        }
        if (slide.layout == "ranking") ui.Toggle(root, "Use built-in scammer rankings", () => SlideDocument.UsesBuiltInRankings(slide), value => {
            string oldId = slide.id; SlideDocument.SetBuiltInRankings(slide, value); options.Slides.Remove(oldId); options.Slides[slide.id] = slideOptions; dirty();
        }, true);
        if (slide.table != null && !SlideDocument.UsesBuiltInRankings(slide)) {
            ui.Field(root, "Column headings (separate with |)", string.Join(" | ", slide.table.headers ?? new string[0]), value => { slide.table.headers = Cells(value); dirty(); }, 400);
            ui.Field(root, "Table rows (one per line; cells with |)", string.Join("\n", (slide.table.rows ?? new string[0][]).Select(cells => string.Join(" | ", cells)).ToArray()), value => {
                slide.table.rows = value.Replace("\r", "").Split('\n').Take(12).Select(Cells).ToArray(); slide.table.tiers = null; dirty();
            }, 2000, true);
        }
        ui.Field(root, "PNG file path", imageFile, value => imageFile = value, 2048);
        ui.Button(root, "Browse PNGs", () => { if (browseDirectory == null) Browse(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)); browseImages = !browseImages; }, rebuild: true);
        if (browseImages) {
            Widgets.Paragraph(root, browseDirectory);
            ui.Button(root, "Up one folder", () => { var parent = Directory.GetParent(browseDirectory); if (parent != null) Browse(parent.FullName); }, rebuild: true);
            foreach (string path in imageFolders.Take(100)) { string folder = path; ui.Button(root, "[Folder] " + Path.GetFileName(folder), () => Browse(folder), rebuild: true); }
            foreach (string path in imageFiles.Take(100)) { string file = path; ui.Button(root, Path.GetFileName(file), () => { imageFile = file; browseImages = false; }, rebuild: true); }
        }
        ui.Button(root, "Import PNG", () => {
            string image = SlideImages.Import(imageFile), old = slide.imagePath; slide.imagePath = image;
            try { SlideDocument.Validate(doc.Deck, false); } catch { slide.imagePath = old; throw; }
            slide.svgPath = ""; slide.iconResource = ""; slide.imageLabel = "Custom PNG"; dirty();
        }, rebuild: true);
        ui.Button(root, "Remove image", () => { slide.imagePath = ""; slide.svgPath = ""; slide.iconResource = ""; dirty(); }, rebuild: true);
        Widgets.Heading(root, "Content preview");
        ui.Live(root, () => slide.kicker + "\n" + slide.title + "\n" + slide.subtitle + "\n" + slide.quote + "\n" + slide.footer);
        if (slide.imagePath != null && slide.imagePath.StartsWith(SlideDocument.PngPrefix, StringComparison.Ordinal)) {
            var image = new Image { image = SlideImages.Get(slide.imagePath), scaleMode = ScaleMode.ScaleToFit }; image.style.height = 150; root.Add(image);
        }
        ui.Live(root, () => "Sharing: " + SlideDocument.NetworkBytes(doc.Deck) + " / " + SlideDocument.MaxDeckBytes + " bytes. Saves: " + doc.DirectoryPath);
    }
}
}
