using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Kolkata.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HostBoss;

public sealed class SlideDocument
{
	public const int MaxSlides = 32;

	public const int MaxDeckBytes = 48000;

	public const string PngPrefix = "hostboss-png:";

	public const int MaxPngBytes = 16384;

	private const string BuiltInRankingsPrefix = "hostboss-live-rankings-";

	public ReviewSlideDeckDefinition Deck;

	public PresentationOptions Options = new PresentationOptions();

	public readonly string DirectoryPath;

	public int Selected;

	public bool Dirty;

	private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
	{
		TypeNameHandling = TypeNameHandling.None,
		MaxDepth = 32
	};

	public SlideDocument(string directory, ReviewSlideDeckDefinition defaults)
	{
		DirectoryPath = Path.GetFullPath(directory);
		Set(defaults);
		Dirty = false;
	}

	public static T Clone<T>(T value)
	{
		return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value), JsonSettings);
	}

	public static int NetworkBytes(ReviewSlideDeckDefinition deck)
	{
		return Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(deck));
	}

	public void Set(ReviewSlideDeckDefinition deck)
	{
		ReviewSlideDeckDefinition deck2 = Clone(deck);
		Validate(deck2, forNetwork: false);
		Deck = deck2;
		Options = new PresentationOptions();
		Selected = 0;
		Dirty = true;
	}

	public void Add(ReviewSlideDefinition template)
	{
		if (Deck.slides.Length >= 32)
		{
			throw new InvalidOperationException("A presentation can contain up to 32 slides.");
		}
		ReviewSlideDefinition reviewSlideDefinition = Clone(template);
		reviewSlideDefinition.id = (UsesBuiltInRankings(template) ? "hostboss-live-rankings-" : "hostboss-made-") + Guid.NewGuid().ToString("N");
		reviewSlideDefinition.replayMoments = null;
		List<ReviewSlideDefinition> list = new List<ReviewSlideDefinition>(Deck.slides);
		int num = Math.Min(Selected + 1, list.Count);
		list.Insert(num, reviewSlideDefinition);
		Deck.slides = list.ToArray();
		Selected = num;
		Dirty = true;
		if (Options.Slides.TryGetValue(template.id, out var value))
		{
			Options.Slides[reviewSlideDefinition.id] = Clone(value);
		}
	}

	public void Remove()
	{
		if (Deck.slides.Length == 1)
		{
			throw new InvalidOperationException("Keep at least one slide.");
		}
		List<ReviewSlideDefinition> list = new List<ReviewSlideDefinition>(Deck.slides);
		Options.Slides.Remove(list[Selected].id);
		list.RemoveAt(Selected);
		Deck.slides = list.ToArray();
		Selected = Math.Min(Selected, list.Count - 1);
		Dirty = true;
	}

	public void Move(int direction)
	{
		int num = Math.Max(0, Math.Min(Deck.slides.Length - 1, Selected + direction));
		ReviewSlideDefinition reviewSlideDefinition = Deck.slides[Selected];
		Deck.slides[Selected] = Deck.slides[num];
		Deck.slides[num] = reviewSlideDefinition;
		Selected = num;
		Dirty = true;
	}

	public void Replace(ReviewSlideDefinition template)
	{
		ReviewSlideDefinition reviewSlideDefinition = Clone(template);
		reviewSlideDefinition.id = Deck.slides[Selected].id;
		SetBuiltInRankings(reviewSlideDefinition, UsesBuiltInRankings(template));
		reviewSlideDefinition.replayMoments = null;
		Options.Slides.Remove(Deck.slides[Selected].id);
		Deck.slides[Selected] = reviewSlideDefinition;
		Dirty = true;
	}

	public static bool UsesBuiltInRankings(ReviewSlideDefinition slide)
	{
		if (slide != null && slide.layout == "ranking" && slide.id != null)
		{
			return slide.id.StartsWith("hostboss-live-rankings-", StringComparison.Ordinal);
		}
		return false;
	}

	public static string TextForSpeech(ReviewSlideDefinition slide)
	{
		string input = string.Join(". ", slide.title ?? "", slide.quote ?? "").Trim(' ', '.');
		input = Regex.Replace(input, "<[^>]+>", "");
		if (input.Length <= 1200)
		{
			return input;
		}
		return input.Substring(0, 1200);
	}

	public static void SetBuiltInRankings(ReviewSlideDefinition slide, bool enabled)
	{
		if (enabled && slide.layout != "ranking")
		{
			throw new InvalidOperationException("Built-in rankings are only available on ranking slides.");
		}
		bool flag = slide.id != null && slide.id.StartsWith("hostboss-live-rankings-", StringComparison.Ordinal);
		if (enabled && !flag)
		{
			slide.id = "hostboss-live-rankings-" + slide.id;
		}
		if (!enabled && flag)
		{
			slide.id = slide.id.Substring("hostboss-live-rankings-".Length);
		}
	}

	public static ReviewSlideDeckDefinition ResolveRankings(ReviewSlideDeckDefinition draft, ReviewSlideDeckDefinition currentReview)
	{
		Validate(draft);
		ReviewSlideDeckDefinition reviewSlideDeckDefinition = Clone(draft);
		ReviewTableDefinition reviewTableDefinition = null;
		ReviewSlideDefinition[] slides;
		if (currentReview != null && currentReview.slides != null)
		{
			slides = currentReview.slides;
			foreach (ReviewSlideDefinition reviewSlideDefinition in slides)
			{
				if (reviewSlideDefinition != null && reviewSlideDefinition.layout == "ranking" && reviewSlideDefinition.table != null)
				{
					reviewTableDefinition = reviewSlideDefinition.table;
					break;
				}
			}
		}
		slides = reviewSlideDeckDefinition.slides;
		foreach (ReviewSlideDefinition reviewSlideDefinition2 in slides)
		{
			if (UsesBuiltInRankings(reviewSlideDefinition2))
			{
				if (reviewTableDefinition == null)
				{
					throw new InvalidOperationException("The game's rankings are not ready. Prepare the review before applying this deck.");
				}
				reviewSlideDefinition2.table = Clone(reviewTableDefinition);
			}
		}
		Validate(reviewSlideDeckDefinition);
		return reviewSlideDeckDefinition;
	}

	public static bool IsBossHeardSlide(ReviewSlideDefinition slide)
	{
		if (slide == null)
		{
			return false;
		}
		if (!(slide.id == "runtime-ai-boss-notes") && (slide.id == null || !slide.id.StartsWith("runtime-ai-boss-notes-", StringComparison.Ordinal)))
		{
			if (slide.replayMoments != null)
			{
				return slide.replayMoments.Length != 0;
			}
			return false;
		}
		return true;
	}

	public static ReviewSlideDeckDefinition ResolveNativeContent(ReviewSlideDeckDefinition draft, ReviewSlideDeckDefinition native, PresentationOptions options)
	{
		ReviewSlideDeckDefinition reviewSlideDeckDefinition = ResolveRankings(draft, native);
		List<ReviewSlideDefinition> slides = new List<ReviewSlideDefinition>();
		HashSet<string> used = new HashSet<string>();
		Action<ReviewSlideDefinition, SlideOptions> action = delegate(ReviewSlideDefinition source, SlideOptions inherited)
		{
			if (source != null && !(source.layout == "fired"))
			{
				ReviewSlideDefinition reviewSlideDefinition5 = Clone(source);
				if (!options.NativeTts)
				{
					reviewSlideDefinition5.narration = "";
					if (reviewSlideDefinition5.replayMoments != null)
					{
						ReviewReplayMoment[] replayMoments = reviewSlideDefinition5.replayMoments;
						foreach (ReviewReplayMoment reviewReplayMoment in replayMoments)
						{
							if (reviewReplayMoment != null)
							{
								reviewReplayMoment.introduction = "";
								reviewReplayMoment.fallback = "";
								reviewReplayMoment.reaction = "";
							}
						}
					}
				}
				if (!used.Add(reviewSlideDefinition5.id))
				{
					reviewSlideDefinition5.id = "hostboss-native-" + Guid.NewGuid().ToString("N");
					used.Add(reviewSlideDefinition5.id);
				}
				if (inherited != null)
				{
					options.Slides[reviewSlideDefinition5.id] = Clone(inherited);
				}
				slides.Add(reviewSlideDefinition5);
			}
		};
		if (options.UseNativeSlides)
		{
			if (native == null || native.slides == null)
			{
				throw new InvalidOperationException("The game's selected slides are not ready.");
			}
			ReviewSlideDefinition[] slides2 = native.slides;
			foreach (ReviewSlideDefinition reviewSlideDefinition in slides2)
			{
				if (options.AnnounceApps || reviewSlideDefinition.layout != "unlock")
				{
					action(reviewSlideDefinition, null);
				}
			}
		}
		else
		{
			ReviewSlideDefinition[] slides2 = reviewSlideDeckDefinition.slides;
			foreach (ReviewSlideDefinition reviewSlideDefinition2 in slides2)
			{
				if (options.Slides.TryGetValue(reviewSlideDefinition2.id, out var value) && value.NativeHeard)
				{
					int count = slides.Count;
					if (native != null && native.slides != null)
					{
						ReviewSlideDefinition[] slides3 = native.slides;
						foreach (ReviewSlideDefinition reviewSlideDefinition3 in slides3)
						{
							if (IsBossHeardSlide(reviewSlideDefinition3))
							{
								action(reviewSlideDefinition3, value);
							}
						}
					}
					if (slides.Count == count)
					{
						reviewSlideDefinition2.quote = "No boss notes or recordings are available for this day.";
						reviewSlideDefinition2.bullets = null;
						reviewSlideDefinition2.replayMoments = null;
						reviewSlideDefinition2.narration = "";
						slides.Add(reviewSlideDefinition2);
						used.Add(reviewSlideDefinition2.id);
					}
				}
				else
				{
					reviewSlideDefinition2.replayMoments = null;
					slides.Add(reviewSlideDefinition2);
					used.Add(reviewSlideDefinition2.id);
				}
			}
			if (options.AnnounceApps && native != null && native.slides != null)
			{
				slides2 = native.slides;
				foreach (ReviewSlideDefinition reviewSlideDefinition4 in slides2)
				{
					if (reviewSlideDefinition4.layout == "unlock" && !used.Contains(reviewSlideDefinition4.id))
					{
						action(reviewSlideDefinition4, null);
					}
				}
			}
		}
		reviewSlideDeckDefinition.slides = slides.ToArray();
		if (slides.Count == 0 || slides.Count > 64 || NetworkBytes(reviewSlideDeckDefinition) > 58000)
		{
			throw new InvalidOperationException("The expanded native presentation is too large to share. Use fewer custom slides or images.");
		}
		return reviewSlideDeckDefinition;
	}

	public string FilePath(string name)
	{
		name = (name ?? "").Trim();
		if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
		{
			name = name.Substring(0, name.Length - 5).Trim();
		}
		if (name.Length == 0 || name.Length > 64 || name == "." || name == "..")
		{
			throw new InvalidOperationException("Use a presentation name of 1–64 letters, numbers, spaces, underscores or dashes.");
		}
		string text = name;
		foreach (char c in text)
		{
			if (c < ' ' || "<>:\"/\\|?*".IndexOf(c) >= 0)
			{
				throw new InvalidOperationException("The presentation name contains an invalid filename character.");
			}
		}
		if (name.EndsWith(".") || Regex.IsMatch(name, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\\.)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
		{
			throw new InvalidOperationException("Choose a different presentation filename.");
		}
		string fullPath = Path.GetFullPath(Path.Combine(DirectoryPath, name + ".json"));
		if (!fullPath.StartsWith(DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Invalid presentation path.");
		}
		return fullPath;
	}

	public void Save(string name)
	{
		Validate(Deck, forNetwork: false);
		Directory.CreateDirectory(DirectoryPath);
		string text = FilePath(name);
		string text2 = text + ".pending";
		string text3 = JsonConvert.SerializeObject(new SavedPresentation
		{
			deck = Deck,
			options = Options
		}, Formatting.Indented);
		if (Encoding.UTF8.GetByteCount(text3) > 2097152)
		{
			throw new InvalidOperationException("Saved presentation exceeds the 2 MB file limit.");
		}
		File.WriteAllText(text2, text3, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		if (File.Exists(text))
		{
			File.Replace(text2, text, text + ".bak");
		}
		else
		{
			File.Move(text2, text);
		}
		Dirty = false;
	}

	public void Load(string name)
	{
		string text = FilePath(name);
		if (new FileInfo(text).Length > 2097152)
		{
			throw new InvalidOperationException("Presentation file is too large.");
		}
		JObject jObject = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(text), JsonSettings);
		ReviewSlideDeckDefinition deck = ((jObject["deck"] != null) ? jObject["deck"].ToObject<ReviewSlideDeckDefinition>() : jObject.ToObject<ReviewSlideDeckDefinition>());
		PresentationOptions presentationOptions = ((jObject["options"] != null) ? jObject["options"].ToObject<PresentationOptions>() : new PresentationOptions());
		if (presentationOptions == null)
		{
			presentationOptions = new PresentationOptions();
		}
		if (presentationOptions.Slides == null)
		{
			presentationOptions.Slides = new Dictionary<string, SlideOptions>();
		}
		foreach (SlideOptions value in presentationOptions.Slides.Values)
		{
			if (value == null || !IsCamera(value.Camera))
			{
				throw new InvalidOperationException("Invalid saved camera position.");
			}
		}
		Set(deck);
		Options = presentationOptions;
		Dirty = false;
	}

	public string[] SavedNames()
	{
		if (!Directory.Exists(DirectoryPath))
		{
			return new string[0];
		}
		string[] files = Directory.GetFiles(DirectoryPath, "*.json");
		for (int i = 0; i < files.Length; i++)
		{
			files[i] = Path.GetFileNameWithoutExtension(files[i]);
		}
		Array.Sort(files, StringComparer.OrdinalIgnoreCase);
		return files;
	}

	private static void Text(string text, int max)
	{
		if (text != null && text.Length > max)
		{
			throw new InvalidOperationException("A slide text field is too long.");
		}
	}

	public static bool IsCamera(string value)
	{
		switch (value)
		{
		default:
			return value == "bottom-right";
		case "auto":
		case "top-left":
		case "top-right":
		case "bottom-left":
			return true;
		}
	}

	public static string CameraClass(PresentationOptions options, string id)
	{
		if (options == null || options.Slides == null || !options.Slides.TryGetValue(id, out var value) || value == null || value.Camera == "auto" || !IsCamera(value.Camera))
		{
			return null;
		}
		return "boss-webcam--" + value.Camera;
	}

	public static void Validate(ReviewSlideDeckDefinition deck, bool forNetwork = true)
	{
		if (deck == null || deck.slides == null || deck.slides.Length == 0 || deck.slides.Length > 32)
		{
			throw new InvalidOperationException("A presentation needs 1–32 slides.");
		}
		Text(deck.title, 160);
		Text(deck.presenterName, 160);
		Text(deck.presenterTitle, 160);
		HashSet<string> hashSet = new HashSet<string>();
		ReviewSlideDefinition[] slides = deck.slides;
		foreach (ReviewSlideDefinition reviewSlideDefinition in slides)
		{
			if (reviewSlideDefinition == null || string.IsNullOrWhiteSpace(reviewSlideDefinition.id) || !hashSet.Add(reviewSlideDefinition.id))
			{
				throw new InvalidOperationException("Slides need unique identifiers.");
			}
			Text(reviewSlideDefinition.id, 160);
			Text(reviewSlideDefinition.title, 160);
			Text(reviewSlideDefinition.subtitle, 400);
			Text(reviewSlideDefinition.quote, 1200);
			Text(reviewSlideDefinition.footer, 400);
			Text(reviewSlideDefinition.kicker, 160);
			Text(reviewSlideDefinition.narration, 1200);
			if (reviewSlideDefinition.imagePath != null && reviewSlideDefinition.imagePath.StartsWith("hostboss-png:", StringComparison.Ordinal))
			{
				string text = reviewSlideDefinition.imagePath.Substring("hostboss-png:".Length);
				if (text.Length > 21848)
				{
					throw new InvalidOperationException("Embedded PNG is too large.");
				}
				ValidatePng(Convert.FromBase64String(text), 512, 16384);
			}
		}
		if (forNetwork && NetworkBytes(deck) > 48000)
		{
			throw new InvalidOperationException("This deck is too large to share. Remove images or slides (48 KB deck limit). You can still save it locally.");
		}
	}

	public static void ValidatePng(byte[] bytes, int maxSide, int maxBytes)
	{
		byte[] array = new byte[16]
		{
			137, 80, 78, 71, 13, 10, 26, 10, 0, 0,
			0, 13, 73, 72, 68, 82
		};
		if (bytes == null || bytes.Length < 33 || bytes.Length > maxBytes)
		{
			throw new InvalidOperationException("Invalid PNG size.");
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (bytes[i] != array[i])
			{
				throw new InvalidOperationException("Choose a PNG image.");
			}
		}
		for (int j = 16; j <= 20; j += 4)
		{
			uint num = (uint)((bytes[j] << 24) | (bytes[j + 1] << 16) | (bytes[j + 2] << 8) | bytes[j + 3]);
			if (num == 0 || num > maxSide)
			{
				throw new InvalidOperationException("PNG dimensions are too large.");
			}
		}
	}
}
