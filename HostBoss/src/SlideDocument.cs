using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kolkata.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HostBoss {
public sealed class SlideOptions {
    public bool NativeHeard;
    public string Camera = "auto";
}
public sealed class PresentationOptions {
    public bool AnnounceApps;
    public bool UseNativeSlides;
    public bool NativeTts = true;
    public Dictionary<string, SlideOptions> Slides = new Dictionary<string, SlideOptions>();
    public SlideOptions For(string id) { SlideOptions value; if (!Slides.TryGetValue(id, out value)) { value = new SlideOptions(); Slides[id] = value; } return value; }
    public bool NeedsAi { get { if (UseNativeSlides) return true; foreach (var item in Slides.Values) if (item.NativeHeard) return true; return false; } }
}
public sealed class SavedPresentation {
    public int schemaVersion = 2;
    public ReviewSlideDeckDefinition deck;
    public PresentationOptions options;
}
// No Unity lifecycle dependencies: saved drafts can be edited outside a running review.
public sealed class SlideDocument {
    public const int MaxSlides = 32;
    // Mirror strings allow 65,534 UTF-8 bytes. Leave room for runtime replay/verdict slides.
    public const int MaxDeckBytes = 48000;
    public const string PngPrefix = "hostboss-png:";
    public const int MaxPngBytes = 16384;
    const string BuiltInRankingsPrefix = "hostboss-live-rankings-";
    public ReviewSlideDeckDefinition Deck;
    public PresentationOptions Options = new PresentationOptions();
    public readonly string DirectoryPath;
    public int Selected;
    public bool Dirty;
    static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 };

    public SlideDocument(string directory, ReviewSlideDeckDefinition defaults) {
        DirectoryPath = Path.GetFullPath(directory);
        Set(defaults);
        Dirty = false;
    }
    public static T Clone<T>(T value) { return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value), JsonSettings); }
    public static int NetworkBytes(ReviewSlideDeckDefinition deck) { return Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(deck)); }
    public void Set(ReviewSlideDeckDefinition deck) {
        var next = Clone(deck);
        Validate(next, false);
        Deck = next; Options = new PresentationOptions(); Selected = 0; Dirty = true;
    }
    public void Add(ReviewSlideDefinition template) {
        if (Deck.slides.Length >= MaxSlides) throw new InvalidOperationException("A presentation can contain up to 32 slides.");
        var slide = Clone(template);
        slide.id = (UsesBuiltInRankings(template) ? BuiltInRankingsPrefix : "hostboss-made-") + Guid.NewGuid().ToString("N");
        slide.replayMoments = null;
        var slides = new List<ReviewSlideDefinition>(Deck.slides);
        int index = Math.Min(Selected + 1, slides.Count);
        slides.Insert(index, slide); Deck.slides = slides.ToArray(); Selected = index; Dirty = true;
        SlideOptions original;
        if (Options.Slides.TryGetValue(template.id, out original)) Options.Slides[slide.id] = Clone(original);
    }
    public void Remove() {
        if (Deck.slides.Length == 1) throw new InvalidOperationException("Keep at least one slide.");
        var slides = new List<ReviewSlideDefinition>(Deck.slides);
        Options.Slides.Remove(slides[Selected].id);
        slides.RemoveAt(Selected); Deck.slides = slides.ToArray(); Selected = Math.Min(Selected, slides.Count - 1); Dirty = true;
    }
    public void Move(int direction) {
        int next = Math.Max(0, Math.Min(Deck.slides.Length - 1, Selected + direction));
        var slide = Deck.slides[Selected]; Deck.slides[Selected] = Deck.slides[next]; Deck.slides[next] = slide;
        Selected = next; Dirty = true;
    }
    public void Replace(ReviewSlideDefinition template) {
        var slide = Clone(template); slide.id = Deck.slides[Selected].id;
        SetBuiltInRankings(slide, UsesBuiltInRankings(template)); slide.replayMoments = null;
        Options.Slides.Remove(Deck.slides[Selected].id);
        Deck.slides[Selected] = slide; Dirty = true;
    }
    // The marker stays inside the native slide schema, so old saved decks still load unchanged.
    public static bool UsesBuiltInRankings(ReviewSlideDefinition slide) {
        return slide != null && slide.layout == "ranking" && slide.id != null && slide.id.StartsWith(BuiltInRankingsPrefix, StringComparison.Ordinal);
    }
    public static string TextForSpeech(ReviewSlideDefinition slide) {
        string text = string.Join(". ", new[] { slide.title ?? "", slide.quote ?? "" }).Trim(' ', '.');
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
        return text.Length > 1200 ? text.Substring(0, 1200) : text;
    }
    public static void SetBuiltInRankings(ReviewSlideDefinition slide, bool enabled) {
        if (enabled && slide.layout != "ranking") throw new InvalidOperationException("Built-in rankings are only available on ranking slides.");
        bool marked = slide.id != null && slide.id.StartsWith(BuiltInRankingsPrefix, StringComparison.Ordinal);
        if (enabled && !marked) slide.id = BuiltInRankingsPrefix + slide.id;
        if (!enabled && marked) slide.id = slide.id.Substring(BuiltInRankingsPrefix.Length);
    }
    public static ReviewSlideDeckDefinition ResolveRankings(ReviewSlideDeckDefinition draft, ReviewSlideDeckDefinition currentReview) {
        Validate(draft);
        var result = Clone(draft);
        ReviewTableDefinition rankings = null;
        if (currentReview != null && currentReview.slides != null) foreach (var native in currentReview.slides) {
            if (native != null && native.layout == "ranking" && native.table != null) { rankings = native.table; break; }
        }
        foreach (var slide in result.slides) if (UsesBuiltInRankings(slide)) {
            if (rankings == null) throw new InvalidOperationException("The game's rankings are not ready. Prepare the review before applying this deck.");
            slide.table = Clone(rankings);
        }
        Validate(result);
        return result;
    }
    public static bool IsBossHeardSlide(ReviewSlideDefinition slide) {
        if (slide == null) return false;
        // 0.1.81 splits the native notes into one slide per selected moment.
        // Text-only moments have no replay array, but are still real boss notes.
        return slide.id == "runtime-ai-boss-notes" ||
            (slide.id != null && slide.id.StartsWith("runtime-ai-boss-notes-", StringComparison.Ordinal)) ||
            (slide.replayMoments != null && slide.replayMoments.Length > 0);
    }
    public static ReviewSlideDeckDefinition ResolveNativeContent(ReviewSlideDeckDefinition draft, ReviewSlideDeckDefinition native, PresentationOptions options) {
        var result = ResolveRankings(draft, native);
        var slides = new List<ReviewSlideDefinition>();
        var used = new HashSet<string>();
        Action<ReviewSlideDefinition, SlideOptions> addNative = (source, inherited) => {
            if (source == null || source.layout == "fired") return;
            var copy = Clone(source);
            if (!options.NativeTts) {
                copy.narration = "";
                if (copy.replayMoments != null) foreach (var moment in copy.replayMoments) if (moment != null) { moment.introduction = ""; moment.fallback = ""; moment.reaction = ""; }
            }
            if (!used.Add(copy.id)) { copy.id = "hostboss-native-" + Guid.NewGuid().ToString("N"); used.Add(copy.id); }
            if (inherited != null) options.Slides[copy.id] = Clone(inherited);
            slides.Add(copy);
        };
        if (options.UseNativeSlides) {
            if (native == null || native.slides == null) throw new InvalidOperationException("The game's selected slides are not ready.");
            foreach (var slide in native.slides) if (options.AnnounceApps || slide.layout != "unlock") addNative(slide, null);
        } else {
            foreach (var slide in result.slides) {
                SlideOptions setting;
                if (options.Slides.TryGetValue(slide.id, out setting) && setting.NativeHeard) {
                    int before = slides.Count;
                    if (native != null && native.slides != null) foreach (var source in native.slides) {
                        if (IsBossHeardSlide(source)) addNative(source, setting);
                    }
                    if (slides.Count == before) {
                        // Missing optional evidence must not discard the rest of the custom deck.
                        // Keep its title/image, but explicitly report that no real evidence exists.
                        slide.quote = "No boss notes or recordings are available for this day.";
                        slide.bullets = null; slide.replayMoments = null; slide.narration = "";
                        slides.Add(slide); used.Add(slide.id);
                    }
                } else { slide.replayMoments = null; slides.Add(slide); used.Add(slide.id); }
            }
            if (options.AnnounceApps && native != null && native.slides != null) foreach (var slide in native.slides) if (slide.layout == "unlock" && !used.Contains(slide.id)) addNative(slide, null);
        }
        result.slides = slides.ToArray();
        if (slides.Count == 0 || slides.Count > 64 || NetworkBytes(result) > 58000) throw new InvalidOperationException("The expanded native presentation is too large to share. Use fewer custom slides or images.");
        return result;
    }
    public string FilePath(string name) {
        name = (name ?? "").Trim();
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 5).Trim();
        if (name.Length == 0 || name.Length > 64 || name == "." || name == "..") throw new InvalidOperationException("Use a presentation name of 1–64 letters, numbers, spaces, underscores or dashes.");
        foreach (char ch in name) if (ch < 32 || "<>:\"/\\|?*".IndexOf(ch) >= 0) throw new InvalidOperationException("The presentation name contains an invalid filename character.");
        if (name.EndsWith(".") || System.Text.RegularExpressions.Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\.)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new InvalidOperationException("Choose a different presentation filename.");
        string path = Path.GetFullPath(Path.Combine(DirectoryPath, name + ".json"));
        if (!path.StartsWith(DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid presentation path.");
        return path;
    }
    public void Save(string name) {
        Validate(Deck, false);
        Directory.CreateDirectory(DirectoryPath);
        string path = FilePath(name), pending = path + ".pending";
        string json = JsonConvert.SerializeObject(new SavedPresentation { deck = Deck, options = Options }, Formatting.Indented);
        if (Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024) throw new InvalidOperationException("Saved presentation exceeds the 2 MB file limit.");
        File.WriteAllText(pending, json, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(pending, path, path + ".bak");
        else File.Move(pending, path);
        Dirty = false;
    }
    public void Load(string name) {
        string path = FilePath(name);
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidOperationException("Presentation file is too large.");
        var root = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(path), JsonSettings);
        var next = root["deck"] != null ? root["deck"].ToObject<ReviewSlideDeckDefinition>() : root.ToObject<ReviewSlideDeckDefinition>();
        var options = root["options"] != null ? root["options"].ToObject<PresentationOptions>() : new PresentationOptions();
        if (options == null) options = new PresentationOptions();
        if (options.Slides == null) options.Slides = new Dictionary<string, SlideOptions>();
        foreach (var value in options.Slides.Values) if (value == null || !IsCamera(value.Camera)) throw new InvalidOperationException("Invalid saved camera position.");
        Set(next); Options = options;
        Dirty = false;
    }
    public string[] SavedNames() {
        if (!Directory.Exists(DirectoryPath)) return new string[0];
        string[] files = Directory.GetFiles(DirectoryPath, "*.json");
        for (int i = 0; i < files.Length; i++) files[i] = Path.GetFileNameWithoutExtension(files[i]);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase); return files;
    }
    static void Text(string text, int max) { if (text != null && text.Length > max) throw new InvalidOperationException("A slide text field is too long."); }
    public static bool IsCamera(string value) { return value == "auto" || value == "top-left" || value == "top-right" || value == "bottom-left" || value == "bottom-right"; }
    public static string CameraClass(PresentationOptions options, string id) {
        SlideOptions setting;
        if (options == null || options.Slides == null || !options.Slides.TryGetValue(id, out setting) || setting == null || setting.Camera == "auto" || !IsCamera(setting.Camera)) return null;
        return "boss-webcam--" + setting.Camera;
    }
    public static void Validate(ReviewSlideDeckDefinition deck, bool forNetwork = true) {
        if (deck == null || deck.slides == null || deck.slides.Length == 0 || deck.slides.Length > MaxSlides) throw new InvalidOperationException("A presentation needs 1–32 slides.");
        Text(deck.title, 160); Text(deck.presenterName, 160); Text(deck.presenterTitle, 160);
        var ids = new HashSet<string>();
        foreach (var slide in deck.slides) {
            if (slide == null || string.IsNullOrWhiteSpace(slide.id) || !ids.Add(slide.id)) throw new InvalidOperationException("Slides need unique identifiers.");
            Text(slide.id, 160); Text(slide.title, 160); Text(slide.subtitle, 400); Text(slide.quote, 1200); Text(slide.footer, 400); Text(slide.kicker, 160); Text(slide.narration, 1200);
            if (slide.imagePath != null && slide.imagePath.StartsWith(PngPrefix, StringComparison.Ordinal)) {
                string encoded = slide.imagePath.Substring(PngPrefix.Length);
                if (encoded.Length > (MaxPngBytes + 2) / 3 * 4) throw new InvalidOperationException("Embedded PNG is too large.");
                ValidatePng(Convert.FromBase64String(encoded), 512, MaxPngBytes);
            }
        }
        if (forNetwork && NetworkBytes(deck) > MaxDeckBytes) throw new InvalidOperationException("This deck is too large to share. Remove images or slides (48 KB deck limit). You can still save it locally.");
    }
    public static void ValidatePng(byte[] bytes, int maxSide, int maxBytes) {
        byte[] signature = {137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82};
        if (bytes == null || bytes.Length < 33 || bytes.Length > maxBytes) throw new InvalidOperationException("Invalid PNG size.");
        for (int i = 0; i < signature.Length; i++) if (bytes[i] != signature[i]) throw new InvalidOperationException("Choose a PNG image.");
        for (int offset = 16; offset <= 20; offset += 4) {
            uint side = ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
            if (side == 0 || side > maxSide) throw new InvalidOperationException("PNG dimensions are too large.");
        }
    }
}
}
