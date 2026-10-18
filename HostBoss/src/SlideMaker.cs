using System;
using System.Collections.Generic;
using System.IO;
using Kolkata.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HostBoss {
public sealed partial class SlideMaker {
    public SlideDocument Document;
    ReviewSlideDeckDefinition defaults;
    ReviewSlideDefinition[] templates;
    int templateIndex;
    string name = "My Presentation", imageFile = "", message = "";
    string[] saved = new string[0];
    bool showLibrary;
    string pendingAction;
    int savedIndex;
    Vector2 slideScroll;
    Vector2 fileScroll;
    bool browseImages;
    string browseDirectory;
    string[] imageFolders = new string[0], imageFiles = new string[0];
    ReviewSlideDefinition editingSlide;
    string tableHeaders, tableRows;
    bool editNarration;
    readonly string directory;
    string currentFile;
    string lastFile { get { return Path.Combine(Document.DirectoryPath, "last-opened.txt"); } }
    public SlideMaker(string directory) { this.directory = directory; }
    public void EnsureLoaded() {
        if (Document != null) return;
        string file = Path.Combine(Application.streamingAssetsPath, "kolkata-review/review-slides.json");
        defaults = JsonConvert.DeserializeObject<ReviewSlideDeckDefinition>(File.ReadAllText(file));
        foreach (var slide in defaults.slides) slide.replayMoments = null;
        var availableTemplates = new List<ReviewSlideDefinition>(SlideDocument.Clone(defaults.slides));
        var decisionFactory = typeof(ReviewDaySummaryTracker).GetMethod("BuildFiredBuildupSlide", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (decisionFactory == null) throw new MissingMethodException("The game's boss decision template is unavailable.");
        var decision = (ReviewSlideDefinition)decisionFactory.Invoke(null, null);
        decision.id = "hostboss-template-decision";
        decision.replayMoments = null;
        availableTemplates.Add(decision);
        templates = availableTemplates.ToArray();
        Document = new SlideDocument(directory, defaults);
        string legacy = Path.Combine(BepInEx.Paths.ConfigPath, "HostBoss", "Presentations");
        if (Directory.Exists(legacy) && legacy != directory) {
            Directory.CreateDirectory(directory);
            foreach (string savedFile in Directory.GetFiles(legacy, "*.json")) { string target = Path.Combine(directory, Path.GetFileName(savedFile)); if (!File.Exists(target)) File.Copy(savedFile, target); }
        }
        RefreshLibrary();
        if (File.Exists(lastFile)) Try(() => { string savedName = File.ReadAllText(lastFile).Trim(); Document.Load(savedName); name = savedName; currentFile = Document.FilePath(name); savedIndex = Math.Max(0, Array.IndexOf(saved, name)); message = "Reopened " + name + "."; });
    }
    void RefreshLibrary() { saved = Document.SavedNames(); savedIndex = Math.Min(savedIndex, Math.Max(0, saved.Length - 1)); }
    void Try(Action action) { try { action(); } catch (Exception e) { message = e.Message; Debug.LogWarning("[HostBoss] Slide maker: " + e.Message); } }
    bool Confirm(string action, string prompt) {
        if (pendingAction == action) { pendingAction = null; return true; }
        pendingAction = action; message = prompt; return false;
    }
    // This game's stripped IMGUI retains DoTextField, but removes the public TextArea wrapper.
    static readonly System.Reflection.MethodInfo textArea = typeof(GUILayout).GetMethod("DoTextField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    static string Multiline(string text, int max) {
        return (string)textArea.Invoke(null, new object[] { text, max, true, GUI.skin.textArea, new[] { GUILayout.Height(70) } });
    }
    static int Choices(int selected, string[] names) {
        for (int i = 0; i < names.Length; i++) if (GUILayout.Toggle(i == selected, names[i])) selected = i;
        return selected;
    }
    void Browse(string folder) {
        var folders = Directory.GetDirectories(folder);
        var files = Directory.GetFiles(folder, "*.png");
        Array.Sort(folders, StringComparer.OrdinalIgnoreCase); Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        browseDirectory = folder; imageFolders = folders; imageFiles = files;
    }
    static string[] Cells(string line) {
        string[] cells = line.Split('|');
        if (cells.Length > 8) Array.Resize(ref cells, 8);
        for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
        return cells;
    }
    string Field(string label, string value, int max, bool multiline = false) {
        GUILayout.Label(label);
        string edited = multiline ? Multiline(value ?? "", max) : GUILayout.TextField(value ?? "");
        if (edited.Length > max) edited = edited.Substring(0, max);
        if (edited != (value ?? "")) Document.Dirty = true;
        return edited;
    }
    public void Draw(BossPanel panel) {
        try { EnsureLoaded(); } catch (Exception e) { GUILayout.Label("Slide Maker could not open: " + e.Message); return; }
        GUILayout.Label("SLIDE MAKER  |  " + (Document.Dirty ? "Unsaved changes" : "Saved / unchanged"));
        if (!string.IsNullOrWhiteSpace(message)) GUILayout.Label(message, new GUIStyle(GUI.skin.box) { wordWrap = true });
        GUILayout.Label("Edit and save anytime. Apply during a waiting manual review, then Start Presentation.");
        GUILayout.Label("Presentation filename (.json is optional)");
        GUILayout.BeginHorizontal();
        name = GUILayout.TextField(name, GUILayout.Width(180));
        if (GUILayout.Button(pendingAction == "save:" + name ? "Confirm overwrite" : "Save")) {
            Try(() => {
                string path = Document.FilePath(name);
                if (File.Exists(path) && !string.Equals(currentFile, path, StringComparison.OrdinalIgnoreCase) && !Confirm("save:" + name, "This name exists. Click Confirm overwrite or choose another name. A backup is kept.")) return;
                Document.Save(name); name = Path.GetFileNameWithoutExtension(path); currentFile = path;
                File.WriteAllText(lastFile, name);
                RefreshLibrary(); savedIndex = Math.Max(0, Array.IndexOf(saved, name)); pendingAction = null; message = "SAVED: " + path;
            });
        }
        if (GUILayout.Button("Saved decks")) { showLibrary = !showLibrary; Try(RefreshLibrary); }
        GUILayout.EndHorizontal();
        if (pendingAction != null && GUILayout.Button("Cancel pending overwrite / load")) pendingAction = null;
        if (showLibrary) {
            if (saved.Length == 0) GUILayout.Label("No saved presentations yet.");
            else {
                savedIndex = Choices(savedIndex, saved);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(pendingAction == "load:" + saved[savedIndex] ? "Confirm load (discard draft)" : "Load selected deck")) Try(() => {
                    if (Document.Dirty && !Confirm("load:" + saved[savedIndex], "Load again to discard unsaved changes, or Save under a new name first.")) return;
                    Document.Load(saved[savedIndex]); name = saved[savedIndex]; currentFile = Document.FilePath(name); File.WriteAllText(lastFile, name); imageFile = ""; pendingAction = null; message = "Loaded " + name + ".";
                });
                if (GUILayout.Button("Cancel replace / load")) pendingAction = null;
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("New from default slides")) Try(() => {
            if (Document.Dirty && !Confirm("new", "Click New again to discard unsaved changes, or save first.")) return;
            Document.Set(defaults); name = "My Presentation"; currentFile = null; imageFile = ""; pendingAction = null; message = "Started a fresh presentation using the game's default slides.";
        });
        bool canApply = panel.CanApplyMadeDeck;
        GUI.enabled = canApply;
        if (GUILayout.Button("Use this deck for review", GUILayout.Height(30))) Try(() => { panel.ApplyMadeDeck(Document); message = "Preparing presentation. Check Host Controls for status, then Start Presentation."; });
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        if (!canApply) GUILayout.Label("To apply: host a manual review, wait for preparation, and leave Start Presentation unclicked.");
        GUILayout.Label(panel.Status, new GUIStyle(GUI.skin.label) { wordWrap = true });
        var options = Document.Options;
        bool apps = GUILayout.Toggle(options.AnnounceApps, "Announce newly unlocked apps using the built-in slides");
        bool native = GUILayout.Toggle(options.UseNativeSlides, "Use the game's selected review slides instead of my custom slides");
        bool tts = GUILayout.Toggle(options.NativeTts, "Keep original boss TTS on built-in slides");
        if (apps != options.AnnounceApps || native != options.UseNativeSlides || tts != options.NativeTts) Document.Dirty = true;
        options.AnnounceApps = apps; options.UseNativeSlides = native; options.NativeTts = tts;
        if (options.NeedsAi) GUILayout.Label("Uses the game's review AI and chosen recordings for this day. It prepares while the presentation stays connecting.");
        Document.Deck.title = Field("Presentation title", Document.Deck.title, 160);
        Document.Deck.presenterName = Field("Presenter name", Document.Deck.presenterName, 160);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(190));
        GUILayout.Label("SLIDES");
        slideScroll = GUILayout.BeginScrollView(slideScroll, GUILayout.Height(300));
        for (int i = 0; i < Document.Deck.slides.Length; i++) {
            if (GUILayout.Toggle(Document.Selected == i, (i + 1) + ". " + Document.Deck.slides[i].title)) {
                if (Document.Selected != i) { Document.Selected = i; imageFile = ""; pendingAction = null; }
            }
        }
        GUILayout.EndScrollView();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Up")) Document.Move(-1);
        if (GUILayout.Button("Down")) Document.Move(1);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Duplicate slide")) Try(() => Document.Add(Document.Deck.slides[Document.Selected]));
        if (GUILayout.Button("Delete slide")) Try(Document.Remove);
        GUILayout.Label("DEFAULT TEMPLATES");
        string[] names = new string[templates.Length];
        for (int i = 0; i < names.Length; i++) names[i] = templates[i].title;
        templateIndex = Choices(templateIndex, names);
        if (GUILayout.Button("Add from template")) Try(() => Document.Add(templates[templateIndex]));
        if (GUILayout.Button("Replace selected")) {
            if (Confirm("replace:" + Document.Selected + ":" + templateIndex, "Click Replace selected again to reset this slide to the template.")) { Document.Replace(templates[templateIndex]); imageFile = ""; }
        }
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        var slide = Document.Deck.slides[Document.Selected];
        var slideOptions = options.For(slide.id);
        if (!ReferenceEquals(editingSlide, slide)) {
            editingSlide = slide; tableHeaders = null; tableRows = null; editNarration = !string.IsNullOrWhiteSpace(slide.narration);
        }
        GUILayout.Label("SLIDE " + (Document.Selected + 1) + "  |  " + slide.layout);
        GUILayout.Label("BOSS CAM POSITION");
        GUILayout.BeginHorizontal();
        foreach (string corner in new[] { "auto", "top-left", "top-right", "bottom-left", "bottom-right" }) {
            if (GUILayout.Toggle(slideOptions.Camera == corner, corner) && slideOptions.Camera != corner) { slideOptions.Camera = corner; Document.Dirty = true; }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Auto restores the game's placement. Custom positions require updated HostBoss on viewers.");
        if (slide.layout == "checklist") {
            bool heard = GUILayout.Toggle(slideOptions.NativeHeard, "Use real What the Boss Heard + the game's selected recording slides");
            if (heard != slideOptions.NativeHeard) { slideOptions.NativeHeard = heard; Document.Dirty = true; }
            if (heard) GUILayout.Label("This placeholder expands into this day's real boss notes and selected clips. Original TTS follows the option above.");
        }
        slide.title = Field("Title", slide.title, 160);
        slide.kicker = Field("Heading label", slide.kicker, 160);
        slide.subtitle = Field("Subtitle", slide.subtitle, 400);
        slide.quote = Field("Quote / callout", slide.quote, 1200, true);
        bool narrate = GUILayout.Toggle(editNarration, "Boss TTS when this slide is shown");
        if (narrate != editNarration) {
            editNarration = narrate;
            slide.narration = narrate ? SlideDocument.TextForSpeech(slide) : "";
            Document.Dirty = true;
        }
        if (editNarration) {
            slide.narration = Field("Boss narration (blank = silent)", slide.narration, 1200, true);
            if (GUILayout.Button("Use this slide's title and quote as narration")) { slide.narration = SlideDocument.TextForSpeech(slide); Document.Dirty = true; }
            GUILayout.Label("Plays through the game's boss voice when this slide appears. Changing slides stops the previous line. Requires the game's TTS engine to be available.");
        }
        slide.footer = Field("Footer", slide.footer, 400);
        GUILayout.BeginHorizontal();
        foreach (string theme in new[] { "white", "blue", "red" }) if (GUILayout.Toggle(slide.theme == theme, theme) && slide.theme != theme) { slide.theme = theme; Document.Dirty = true; }
        GUILayout.EndHorizontal();
        if (slide.metrics != null) foreach (var metric in slide.metrics) {
            metric.label = Field("Metric label", metric.label, 80);
            metric.value = Field("Metric value", metric.value, 120);
            metric.detail = Field("Metric detail", metric.detail, 200);
        }
        if (slide.bullets != null || slide.layout == "checklist") {
            var lines = new List<string>();
            if (slide.bullets != null) foreach (var bullet in slide.bullets) lines.Add(bullet.text);
            string previous = string.Join("\n", lines.ToArray());
            string edited = Field("Checklist (one item per line, up to 8)", previous, 1600, true);
            if (edited != previous) {
                var bullets = new List<ReviewBulletDefinition>();
                foreach (string line in edited.Replace("\r", "").Split('\n')) if (bullets.Count < 8) bullets.Add(new ReviewBulletDefinition { text = line, tone = "neutral", iconSvgPath = "kolkata-review/svg/check.svg" });
                slide.bullets = bullets.ToArray();
            }
        }
        bool builtInRankings = SlideDocument.UsesBuiltInRankings(slide);
        if (slide.layout == "ranking") {
            bool enabled = GUILayout.Toggle(builtInRankings, "Use built-in scammer rankings");
            if (enabled != builtInRankings) { string oldId = slide.id; SlideDocument.SetBuiltInRankings(slide, enabled); options.Slides.Remove(oldId); options.Slides[slide.id] = slideOptions; Document.Dirty = true; builtInRankings = enabled; }
            if (builtInRankings) GUILayout.Label("Player order, earnings and grades come from the game's current-day rankings when you apply this deck. Custom titles and images stay yours.");
        }
        if (slide.table != null && !builtInRankings) {
            if (tableHeaders == null) tableHeaders = string.Join(" | ", slide.table.headers ?? new string[0]);
            string nextHeaders = Field("Column headings (separate with |)", tableHeaders, 400);
            if (nextHeaders != tableHeaders) { slide.table.headers = Cells(nextHeaders); tableHeaders = nextHeaders; }
            var rows = new List<string>();
            if (tableRows == null) {
                if (slide.table.rows != null) foreach (var row in slide.table.rows) rows.Add(string.Join(" | ", row));
                tableRows = string.Join("\n", rows.ToArray());
            }
            string nextRows = Field("Table rows (one per line; cells separated with |)", tableRows, 2000, true);
            if (nextRows != tableRows) {
                rows.Clear(); rows.AddRange(nextRows.Replace("\r", "").Split('\n'));
                var cells = new List<string[]>(); foreach (var row in rows) if (cells.Count < 12) cells.Add(Cells(row));
                slide.table.rows = cells.ToArray(); slide.table.tiers = null; tableRows = nextRows;
            }
        }
        GUILayout.Label("PNG image: paste a local .png path");
        imageFile = GUILayout.TextField(imageFile);
        if (GUILayout.Button(browseImages ? "Close PNG browser" : "Browse PNGs")) Try(() => {
            if (!browseImages && browseDirectory == null) Browse(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            browseImages = !browseImages;
        });
        if (browseImages) {
            GUILayout.Label(browseDirectory, new GUIStyle(GUI.skin.label) { wordWrap = true });
            if (GUILayout.Button("Up one folder")) Try(() => { var parent = Directory.GetParent(browseDirectory); if (parent != null) Browse(parent.FullName); });
            fileScroll = GUILayout.BeginScrollView(fileScroll, GUILayout.Height(160));
            for (int i = 0; i < Math.Min(100, imageFolders.Length); i++) {
                string folder = imageFolders[i];
                if (GUILayout.Button("[Folder] " + Path.GetFileName(folder))) { Try(() => Browse(folder)); break; }
            }
            for (int i = 0; i < Math.Min(100, imageFiles.Length); i++) {
                string file = imageFiles[i];
                if (GUILayout.Button(Path.GetFileName(file))) { imageFile = file; browseImages = false; }
            }
            GUILayout.EndScrollView();
            GUILayout.Label("Select a file, then click Import PNG. You can also paste any full path above.");
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Import PNG")) Try(() => {
            string image = SlideImages.Import(imageFile);
            string old = slide.imagePath;
            slide.imagePath = image;
            try { SlideDocument.Validate(Document.Deck, false); } catch { slide.imagePath = old; throw; }
            slide.svgPath = ""; slide.iconResource = ""; slide.imageLabel = "Custom PNG"; Document.Dirty = true;
            message = "PNG embedded. It may be resized to fit multiplayer. Everyone needs HostBoss 1.6.0 for custom PNGs.";
        });
        if (GUILayout.Button("Remove image")) { slide.imagePath = ""; slide.svgPath = ""; slide.iconResource = ""; Document.Dirty = true; }
        GUILayout.EndHorizontal();
        GUILayout.Label("Custom PNGs require HostBoss 1.6.0 on viewers. Images are embedded in saves; no shared file paths needed.");
        DrawPreview(slide);
        GUILayout.EndVertical(); GUILayout.EndHorizontal();
        GUILayout.Label(message, new GUIStyle(GUI.skin.label) { wordWrap = true });
        int bytes = SlideDocument.NetworkBytes(Document.Deck);
        GUILayout.Label("Sharing size: " + bytes + " / " + SlideDocument.MaxDeckBytes + " bytes. Saved in: " + Document.DirectoryPath);
    }
    static void DrawPreview(ReviewSlideDefinition slide) {
        GUILayout.Space(8); GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("CONTENT PREVIEW  |  " + slide.layout);
        GUILayout.Label(slide.kicker ?? "");
        GUILayout.Label(slide.title ?? "", new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true });
        GUILayout.Label(slide.subtitle ?? "", new GUIStyle(GUI.skin.label) { wordWrap = true });
        GUILayout.Label(slide.quote ?? "", new GUIStyle(GUI.skin.label) { wordWrap = true });
        if (slide.imagePath != null && slide.imagePath.StartsWith(SlideDocument.PngPrefix, StringComparison.Ordinal)) {
            Texture2D texture = SlideImages.Get(slide.imagePath);
            if (texture != null) GUILayout.Label(texture, GUILayout.Height(140), GUILayout.Width(300));
        }
        if (slide.bullets != null) foreach (var bullet in slide.bullets) GUILayout.Label("• " + bullet.text);
        if (slide.metrics != null) foreach (var metric in slide.metrics) GUILayout.Label(metric.label + ": " + metric.value + "  " + metric.detail);
        if (SlideDocument.UsesBuiltInRankings(slide)) GUILayout.Label("[The game's current-day player rankings appear here when applied]");
        else if (slide.table != null && slide.table.rows != null) foreach (var row in slide.table.rows) GUILayout.Label(string.Join("  |  ", row));
        GUILayout.Label(slide.footer ?? ""); GUILayout.EndVertical();
    }
}
}
