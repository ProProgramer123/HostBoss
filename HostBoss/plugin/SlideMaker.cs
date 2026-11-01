using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using Kolkata.UI;
using Newtonsoft.Json;
using ScamWYF.Modding.Core.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace HostBoss;

public sealed class SlideMaker
{
	public SlideDocument Document;

	private ReviewSlideDeckDefinition defaults;

	private ReviewSlideDefinition[] templates;

	private int templateIndex;

	private string name = "My Presentation";

	private string imageFile = "";

	private string message = "";

	private string[] saved = new string[0];

	private bool showLibrary;

	private string pendingAction;

	private int savedIndex;

	private Vector2 slideScroll;

	private Vector2 fileScroll;

	private bool browseImages;

	private string browseDirectory;

	private string[] imageFolders = new string[0];

	private string[] imageFiles = new string[0];

	private ReviewSlideDefinition editingSlide;

	private string tableHeaders;

	private string tableRows;

	private bool editNarration;

	private readonly string directory;

	private string currentFile;

	private static readonly MethodInfo textArea = typeof(GUILayout).GetMethod("DoTextField", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

	private string lastFile => Path.Combine(Document.DirectoryPath, "last-opened.txt");

	public SlideMaker(string directory)
	{
		this.directory = directory;
	}

	public void EnsureLoaded()
	{
		if (Document != null)
		{
			return;
		}
		string path = Path.Combine(Application.streamingAssetsPath, "kolkata-review/review-slides.json");
		defaults = JsonConvert.DeserializeObject<ReviewSlideDeckDefinition>(File.ReadAllText(path));
		ReviewSlideDefinition[] slides = defaults.slides;
		for (int i = 0; i < slides.Length; i++)
		{
			slides[i].replayMoments = null;
		}
		List<ReviewSlideDefinition> list = new List<ReviewSlideDefinition>(SlideDocument.Clone(defaults.slides));
		MethodInfo method = typeof(ReviewDaySummaryTracker).GetMethod("BuildFiredBuildupSlide", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (method == null)
		{
			throw new MissingMethodException("The game's boss decision template is unavailable.");
		}
		ReviewSlideDefinition reviewSlideDefinition = (ReviewSlideDefinition)method.Invoke(null, null);
		reviewSlideDefinition.id = "hostboss-template-decision";
		reviewSlideDefinition.replayMoments = null;
		list.Add(reviewSlideDefinition);
		templates = list.ToArray();
		Document = new SlideDocument(directory, defaults);
		string text = Path.Combine(Paths.ConfigPath, "HostBoss", "Presentations");
		if (Directory.Exists(text) && text != directory)
		{
			Directory.CreateDirectory(directory);
			string[] files = Directory.GetFiles(text, "*.json");
			foreach (string text2 in files)
			{
				string text3 = Path.Combine(directory, Path.GetFileName(text2));
				if (!File.Exists(text3))
				{
					File.Copy(text2, text3);
				}
			}
		}
		RefreshLibrary();
		if (File.Exists(lastFile))
		{
			Try(delegate
			{
				string text4 = File.ReadAllText(lastFile).Trim();
				Document.Load(text4);
				name = text4;
				currentFile = Document.FilePath(name);
				savedIndex = Math.Max(0, Array.IndexOf(saved, name));
				message = "Reopened " + name + ".";
			});
		}
	}

	private void RefreshLibrary()
	{
		saved = Document.SavedNames();
		savedIndex = Math.Min(savedIndex, Math.Max(0, saved.Length - 1));
	}

	private void Try(Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			message = ex.Message;
			Debug.LogWarning("[HostBoss] Slide maker: " + ex.Message);
		}
	}

	private bool Confirm(string action, string prompt)
	{
		if (pendingAction == action)
		{
			pendingAction = null;
			return true;
		}
		pendingAction = action;
		message = prompt;
		return false;
	}

	private static string Multiline(string text, int max)
	{
		return (string)textArea.Invoke(null, new object[5]
		{
			text,
			max,
			true,
			GUI.skin.textArea,
			new GUILayoutOption[1] { GUILayout.Height(70f) }
		});
	}

	private static int Choices(int selected, string[] names)
	{
		for (int i = 0; i < names.Length; i++)
		{
			if (GUILayout.Toggle(i == selected, names[i]))
			{
				selected = i;
			}
		}
		return selected;
	}

	private void Browse(string folder)
	{
		string[] directories = Directory.GetDirectories(folder);
		string[] files = Directory.GetFiles(folder, "*.png");
		Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
		Array.Sort(files, StringComparer.OrdinalIgnoreCase);
		browseDirectory = folder;
		imageFolders = directories;
		imageFiles = files;
	}

	private static string[] Cells(string line)
	{
		string[] array = line.Split('|');
		if (array.Length > 8)
		{
			Array.Resize(ref array, 8);
		}
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].Trim();
		}
		return array;
	}

	private string Field(string label, string value, int max, bool multiline = false)
	{
		GUILayout.Label(label);
		string text = (multiline ? Multiline(value ?? "", max) : GUILayout.TextField(value ?? ""));
		if (text.Length > max)
		{
			text = text.Substring(0, max);
		}
		if (text != (value ?? ""))
		{
			Document.Dirty = true;
		}
		return text;
	}

	public void Draw(BossPanel panel)
	{
		try
		{
			EnsureLoaded();
		}
		catch (Exception ex)
		{
			GUILayout.Label("Slide Maker could not open: " + ex.Message);
			return;
		}
		GUILayout.Label("SLIDE MAKER  |  " + (Document.Dirty ? "Unsaved changes" : "Saved / unchanged"));
		if (!string.IsNullOrWhiteSpace(message))
		{
			GUILayout.Label(message, new GUIStyle(GUI.skin.box)
			{
				wordWrap = true
			});
		}
		GUILayout.Label("Edit and save anytime. Apply during a waiting manual review, then Start Presentation.");
		GUILayout.Label("Presentation filename (.json is optional)");
		GUILayout.BeginHorizontal();
		name = GUILayout.TextField(name, GUILayout.Width(180f));
		if (GUILayout.Button((pendingAction == "save:" + name) ? "Confirm overwrite" : "Save"))
		{
			Try(delegate
			{
				string text8 = Document.FilePath(name);
				if (!File.Exists(text8) || string.Equals(currentFile, text8, StringComparison.OrdinalIgnoreCase) || Confirm("save:" + name, "This name exists. Click Confirm overwrite or choose another name. A backup is kept."))
				{
					Document.Save(name);
					name = Path.GetFileNameWithoutExtension(text8);
					currentFile = text8;
					File.WriteAllText(lastFile, name);
					RefreshLibrary();
					savedIndex = Math.Max(0, Array.IndexOf(saved, name));
					pendingAction = null;
					message = "SAVED: " + text8;
				}
			});
		}
		if (GUILayout.Button("Saved decks"))
		{
			showLibrary = !showLibrary;
			Try(RefreshLibrary);
		}
		GUILayout.EndHorizontal();
		if (pendingAction != null && GUILayout.Button("Cancel pending overwrite / load"))
		{
			pendingAction = null;
		}
		if (showLibrary)
		{
			if (saved.Length == 0)
			{
				GUILayout.Label("No saved presentations yet.");
			}
			else
			{
				savedIndex = Choices(savedIndex, saved);
				GUILayout.BeginHorizontal();
				if (GUILayout.Button((pendingAction == "load:" + saved[savedIndex]) ? "Confirm load (discard draft)" : "Load selected deck"))
				{
					Try(delegate
					{
						if (!Document.Dirty || Confirm("load:" + saved[savedIndex], "Load again to discard unsaved changes, or Save under a new name first."))
						{
							Document.Load(saved[savedIndex]);
							name = saved[savedIndex];
							currentFile = Document.FilePath(name);
							File.WriteAllText(lastFile, name);
							imageFile = "";
							pendingAction = null;
							message = "Loaded " + name + ".";
						}
					});
				}
				if (GUILayout.Button("Cancel replace / load"))
				{
					pendingAction = null;
				}
				GUILayout.EndHorizontal();
			}
		}
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("New from default slides"))
		{
			Try(delegate
			{
				if (!Document.Dirty || Confirm("new", "Click New again to discard unsaved changes, or save first."))
				{
					Document.Set(defaults);
					name = "My Presentation";
					currentFile = null;
					imageFile = "";
					pendingAction = null;
					message = "Started a fresh presentation using the game's default slides.";
				}
			});
		}
		bool flag = (GUI.enabled = panel.CanApplyMadeDeck);
		if (GUILayout.Button("Use this deck for review", GUILayout.Height(30f)))
		{
			Try(delegate
			{
				panel.ApplyMadeDeck(Document);
				message = "Preparing presentation. Check Host Controls for status, then Start Presentation.";
			});
		}
		GUI.enabled = true;
		GUILayout.EndHorizontal();
		if (!flag)
		{
			GUILayout.Label("To apply: host a manual review, wait for preparation, and leave Start Presentation unclicked.");
		}
		GUILayout.Label(panel.Status, new GUIStyle(GUI.skin.label)
		{
			wordWrap = true
		});
		PresentationOptions options = Document.Options;
		bool flag2 = GUILayout.Toggle(options.AnnounceApps, "Announce newly unlocked apps using the built-in slides");
		bool flag3 = GUILayout.Toggle(options.UseNativeSlides, "Use the game's selected review slides instead of my custom slides");
		bool flag4 = GUILayout.Toggle(options.NativeTts, "Keep original boss TTS on built-in slides");
		if (flag2 != options.AnnounceApps || flag3 != options.UseNativeSlides || flag4 != options.NativeTts)
		{
			Document.Dirty = true;
		}
		options.AnnounceApps = flag2;
		options.UseNativeSlides = flag3;
		options.NativeTts = flag4;
		if (options.NeedsAi)
		{
			GUILayout.Label("Uses the game's review AI and chosen recordings for this day. It prepares while the presentation stays connecting.");
		}
		Document.Deck.title = Field("Presentation title", Document.Deck.title, 160);
		Document.Deck.presenterName = Field("Presenter name", Document.Deck.presenterName, 160);
		GUILayout.BeginHorizontal();
		GUILayout.BeginVertical(GUILayout.Width(190f));
		GUILayout.Label("SLIDES");
		slideScroll = GUILayout.BeginScrollView(slideScroll, GUILayout.Height(300f));
		for (int num = 0; num < Document.Deck.slides.Length; num++)
		{
			if (GUILayout.Toggle(Document.Selected == num, num + 1 + ". " + Document.Deck.slides[num].title) && Document.Selected != num)
			{
				Document.Selected = num;
				imageFile = "";
				pendingAction = null;
			}
		}
		GUILayout.EndScrollView();
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("Up"))
		{
			Document.Move(-1);
		}
		if (GUILayout.Button("Down"))
		{
			Document.Move(1);
		}
		GUILayout.EndHorizontal();
		if (GUILayout.Button("Duplicate slide"))
		{
			Try(delegate
			{
				Document.Add(Document.Deck.slides[Document.Selected]);
			});
		}
		if (GUILayout.Button("Delete slide"))
		{
			Try(Document.Remove);
		}
		GUILayout.Label("DEFAULT TEMPLATES");
		string[] array = new string[templates.Length];
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			array[num2] = templates[num2].title;
		}
		templateIndex = Choices(templateIndex, array);
		if (GUILayout.Button("Add from template"))
		{
			Try(delegate
			{
				Document.Add(templates[templateIndex]);
			});
		}
		if (GUILayout.Button("Replace selected") && Confirm("replace:" + Document.Selected + ":" + templateIndex, "Click Replace selected again to reset this slide to the template."))
		{
			Document.Replace(templates[templateIndex]);
			imageFile = "";
		}
		GUILayout.EndVertical();
		GUILayout.BeginVertical();
		ReviewSlideDefinition slide = Document.Deck.slides[Document.Selected];
		SlideOptions slideOptions = options.For(slide.id);
		if (editingSlide != slide)
		{
			editingSlide = slide;
			tableHeaders = null;
			tableRows = null;
			editNarration = !string.IsNullOrWhiteSpace(slide.narration);
		}
		GUILayout.Label("SLIDE " + (Document.Selected + 1) + "  |  " + slide.layout);
		GUILayout.Label("BOSS CAM POSITION");
		GUILayout.BeginHorizontal();
		string[] array2 = new string[5] { "auto", "top-left", "top-right", "bottom-left", "bottom-right" };
		foreach (string text in array2)
		{
			if (GUILayout.Toggle(slideOptions.Camera == text, text) && slideOptions.Camera != text)
			{
				slideOptions.Camera = text;
				Document.Dirty = true;
			}
		}
		GUILayout.EndHorizontal();
		GUILayout.Label("Auto restores the game's placement. Custom positions require updated HostBoss on viewers.");
		if (slide.layout == "checklist")
		{
			bool flag5 = GUILayout.Toggle(slideOptions.NativeHeard, "Use real What the Boss Heard + the game's selected recording slides");
			if (flag5 != slideOptions.NativeHeard)
			{
				slideOptions.NativeHeard = flag5;
				Document.Dirty = true;
			}
			if (flag5)
			{
				GUILayout.Label("This placeholder expands into this day's real boss notes and selected clips. Original TTS follows the option above.");
			}
		}
		slide.title = Field("Title", slide.title, 160);
		slide.kicker = Field("Heading label", slide.kicker, 160);
		slide.subtitle = Field("Subtitle", slide.subtitle, 400);
		slide.quote = Field("Quote / callout", slide.quote, 1200, multiline: true);
		bool flag6 = GUILayout.Toggle(editNarration, "Boss TTS when this slide is shown");
		if (flag6 != editNarration)
		{
			editNarration = flag6;
			slide.narration = (flag6 ? SlideDocument.TextForSpeech(slide) : "");
			Document.Dirty = true;
		}
		if (editNarration)
		{
			slide.narration = Field("Boss narration (blank = silent)", slide.narration, 1200, multiline: true);
			if (GUILayout.Button("Use this slide's title and quote as narration"))
			{
				slide.narration = SlideDocument.TextForSpeech(slide);
				Document.Dirty = true;
			}
			GUILayout.Label("Plays through the game's boss voice when this slide appears. Changing slides stops the previous line. Requires the game's TTS engine to be available.");
		}
		slide.footer = Field("Footer", slide.footer, 400);
		GUILayout.BeginHorizontal();
		array2 = new string[3] { "white", "blue", "red" };
		foreach (string text2 in array2)
		{
			if (GUILayout.Toggle(slide.theme == text2, text2) && slide.theme != text2)
			{
				slide.theme = text2;
				Document.Dirty = true;
			}
		}
		GUILayout.EndHorizontal();
		if (slide.metrics != null)
		{
			ReviewMetricDefinition[] metrics = slide.metrics;
			foreach (ReviewMetricDefinition reviewMetricDefinition in metrics)
			{
				reviewMetricDefinition.label = Field("Metric label", reviewMetricDefinition.label, 80);
				reviewMetricDefinition.value = Field("Metric value", reviewMetricDefinition.value, 120);
				reviewMetricDefinition.detail = Field("Metric detail", reviewMetricDefinition.detail, 200);
			}
		}
		if (slide.bullets != null || slide.layout == "checklist")
		{
			List<string> list = new List<string>();
			if (slide.bullets != null)
			{
				ReviewBulletDefinition[] bullets = slide.bullets;
				foreach (ReviewBulletDefinition reviewBulletDefinition in bullets)
				{
					list.Add(reviewBulletDefinition.text);
				}
			}
			string text3 = string.Join("\n", list.ToArray());
			string text4 = Field("Checklist (one item per line, up to 8)", text3, 1600, multiline: true);
			if (text4 != text3)
			{
				List<ReviewBulletDefinition> list2 = new List<ReviewBulletDefinition>();
				array2 = text4.Replace("\r", "").Split('\n');
				foreach (string text5 in array2)
				{
					if (list2.Count < 8)
					{
						list2.Add(new ReviewBulletDefinition
						{
							text = text5,
							tone = "neutral",
							iconSvgPath = "kolkata-review/svg/check.svg"
						});
					}
				}
				slide.bullets = list2.ToArray();
			}
		}
		bool flag7 = SlideDocument.UsesBuiltInRankings(slide);
		if (slide.layout == "ranking")
		{
			bool flag8 = GUILayout.Toggle(flag7, "Use built-in scammer rankings");
			if (flag8 != flag7)
			{
				string id = slide.id;
				SlideDocument.SetBuiltInRankings(slide, flag8);
				options.Slides.Remove(id);
				options.Slides[slide.id] = slideOptions;
				Document.Dirty = true;
				flag7 = flag8;
			}
			if (flag7)
			{
				GUILayout.Label("Player order, earnings and grades come from the game's current-day rankings when you apply this deck. Custom titles and images stay yours.");
			}
		}
		if (slide.table != null && !flag7)
		{
			if (tableHeaders == null)
			{
				tableHeaders = string.Join(" | ", slide.table.headers ?? new string[0]);
			}
			string text6 = Field("Column headings (separate with |)", tableHeaders, 400);
			if (text6 != tableHeaders)
			{
				slide.table.headers = Cells(text6);
				tableHeaders = text6;
			}
			List<string> list3 = new List<string>();
			if (tableRows == null)
			{
				if (slide.table.rows != null)
				{
					string[][] rows = slide.table.rows;
					foreach (string[] value in rows)
					{
						list3.Add(string.Join(" | ", value));
					}
				}
				tableRows = string.Join("\n", list3.ToArray());
			}
			string text7 = Field("Table rows (one per line; cells separated with |)", tableRows, 2000, multiline: true);
			if (text7 != tableRows)
			{
				list3.Clear();
				list3.AddRange(text7.Replace("\r", "").Split('\n'));
				List<string[]> list4 = new List<string[]>();
				foreach (string item in list3)
				{
					if (list4.Count < 12)
					{
						list4.Add(Cells(item));
					}
				}
				slide.table.rows = list4.ToArray();
				slide.table.tiers = null;
				tableRows = text7;
			}
		}
		GUILayout.Label("PNG image: paste a local .png path");
		imageFile = GUILayout.TextField(imageFile);
		if (GUILayout.Button(browseImages ? "Close PNG browser" : "Browse PNGs"))
		{
			Try(delegate
			{
				if (!browseImages && browseDirectory == null)
				{
					Browse(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
				}
				browseImages = !browseImages;
			});
		}
		if (browseImages)
		{
			GUILayout.Label(browseDirectory, new GUIStyle(GUI.skin.label)
			{
				wordWrap = true
			});
			if (GUILayout.Button("Up one folder"))
			{
				Try(delegate
				{
					DirectoryInfo parent = Directory.GetParent(browseDirectory);
					if (parent != null)
					{
						Browse(parent.FullName);
					}
				});
			}
			fileScroll = GUILayout.BeginScrollView(fileScroll, GUILayout.Height(160f));
			for (int num4 = 0; num4 < Math.Min(100, imageFolders.Length); num4++)
			{
				string folder = imageFolders[num4];
				if (GUILayout.Button("[Folder] " + Path.GetFileName(folder)))
				{
					Try(delegate
					{
						Browse(folder);
					});
					break;
				}
			}
			for (int num5 = 0; num5 < Math.Min(100, imageFiles.Length); num5++)
			{
				string path = imageFiles[num5];
				if (GUILayout.Button(Path.GetFileName(path)))
				{
					imageFile = path;
					browseImages = false;
				}
			}
			GUILayout.EndScrollView();
			GUILayout.Label("Select a file, then click Import PNG. You can also paste any full path above.");
		}
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("Import PNG"))
		{
			Try(delegate
			{
				string imagePath = SlideImages.Import(imageFile);
				string imagePath2 = slide.imagePath;
				slide.imagePath = imagePath;
				try
				{
					SlideDocument.Validate(Document.Deck, forNetwork: false);
				}
				catch
				{
					slide.imagePath = imagePath2;
					throw;
				}
				slide.svgPath = "";
				slide.iconResource = "";
				slide.imageLabel = "Custom PNG";
				Document.Dirty = true;
				message = "PNG embedded. It may be resized to fit multiplayer. Everyone needs HostBoss 1.6.0 for custom PNGs.";
			});
		}
		if (GUILayout.Button("Remove image"))
		{
			slide.imagePath = "";
			slide.svgPath = "";
			slide.iconResource = "";
			Document.Dirty = true;
		}
		GUILayout.EndHorizontal();
		GUILayout.Label("Custom PNGs require HostBoss 1.6.0 on viewers. Images are embedded in saves; no shared file paths needed.");
		DrawPreview(slide);
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
		GUILayout.Label(message, new GUIStyle(GUI.skin.label)
		{
			wordWrap = true
		});
		int num6 = SlideDocument.NetworkBytes(Document.Deck);
		GUILayout.Label("Sharing size: " + num6 + " / " + 48000 + " bytes. Saved in: " + Document.DirectoryPath);
	}

	private static void DrawPreview(ReviewSlideDefinition slide)
	{
		GUILayout.Space(8f);
		GUILayout.BeginVertical(GUI.skin.box);
		GUILayout.Label("CONTENT PREVIEW  |  " + slide.layout);
		GUILayout.Label(slide.kicker ?? "");
		GUILayout.Label(slide.title ?? "", new GUIStyle(GUI.skin.label)
		{
			fontSize = 20,
			wordWrap = true
		});
		GUILayout.Label(slide.subtitle ?? "", new GUIStyle(GUI.skin.label)
		{
			wordWrap = true
		});
		GUILayout.Label(slide.quote ?? "", new GUIStyle(GUI.skin.label)
		{
			wordWrap = true
		});
		if (slide.imagePath != null && slide.imagePath.StartsWith("hostboss-png:", StringComparison.Ordinal))
		{
			Texture2D texture2D = SlideImages.Get(slide.imagePath);
			if (texture2D != null)
			{
				GUILayout.Label(texture2D, GUILayout.Height(140f), GUILayout.Width(300f));
			}
		}
		if (slide.bullets != null)
		{
			ReviewBulletDefinition[] bullets = slide.bullets;
			foreach (ReviewBulletDefinition reviewBulletDefinition in bullets)
			{
				GUILayout.Label("• " + reviewBulletDefinition.text);
			}
		}
		if (slide.metrics != null)
		{
			ReviewMetricDefinition[] metrics = slide.metrics;
			foreach (ReviewMetricDefinition reviewMetricDefinition in metrics)
			{
				GUILayout.Label(reviewMetricDefinition.label + ": " + reviewMetricDefinition.value + "  " + reviewMetricDefinition.detail);
			}
		}
		if (SlideDocument.UsesBuiltInRankings(slide))
		{
			GUILayout.Label("[The game's current-day player rankings appear here when applied]");
		}
		else if (slide.table != null && slide.table.rows != null)
		{
			string[][] rows = slide.table.rows;
			foreach (string[] value in rows)
			{
				GUILayout.Label(string.Join("  |  ", value));
			}
		}
		GUILayout.Label(slide.footer ?? "");
		GUILayout.EndVertical();
	}

	public void BuildLaptop(LaptopView ui, VisualElement root, BossPanel panel)
	{
		EnsureLoaded();
		SlideDocument doc = Document;
		ReviewSlideDeckDefinition openedDeck = doc.Deck;
		int openedSelection = doc.Selected;
		ReviewSlideDefinition openedSlide = doc.Deck.slides[doc.Selected];
		string openedSlideId = openedSlide.id;
		ui.RebuildWhen(() => Document != doc || doc.Deck != openedDeck || doc.Selected != openedSelection || doc.Deck.slides[doc.Selected] != openedSlide || openedSlide.id != openedSlideId);
		Widgets.Heading(root, "SLIDE MAKER");
		ui.Live(root, () => (Document.Dirty ? "Unsaved changes | " : "Saved / unchanged | ") + message);
		ui.Live(root, () => panel.Status);
		ui.Field(root, "Presentation filename", name, delegate(string value)
		{
			name = value;
		}, 69);
		ui.Button(root, "Save / confirm overwrite", delegate
		{
			string text3 = Document.FilePath(name);
			if (!File.Exists(text3) || string.Equals(currentFile, text3, StringComparison.OrdinalIgnoreCase) || Confirm("save:" + name, "This name exists. Click Save again to confirm overwrite."))
			{
				Document.Save(name);
				name = Path.GetFileNameWithoutExtension(text3);
				currentFile = text3;
				File.WriteAllText(lastFile, name);
				RefreshLibrary();
				pendingAction = null;
				message = "SAVED: " + text3;
			}
		});
		ui.Button(root, "Refresh saved decks", RefreshLibrary, null, rebuild: true);
		RefreshLibrary();
		string[] array = saved;
		foreach (string text in array)
		{
			string file = text;
			ui.Button(root, "Load: " + file, delegate
			{
				if (!Document.Dirty || Confirm("load:" + file, "Click Load again to discard unsaved changes, or Save first."))
				{
					Document.Load(file);
					name = file;
					currentFile = Document.FilePath(file);
					File.WriteAllText(lastFile, file);
					pendingAction = null;
					message = "Loaded " + file;
				}
			}, null, rebuild: true);
		}
		ui.Button(root, "New from default slides", delegate
		{
			if (!Document.Dirty || Confirm("new", "Click New again to discard unsaved changes, or save first."))
			{
				Document.Set(defaults);
				name = "My Presentation";
				currentFile = null;
				pendingAction = null;
			}
		}, null, rebuild: true);
		ui.Button(root, "Cancel pending overwrite / load", delegate
		{
			pendingAction = null;
			message = "Cancelled.";
		});
		ui.Button(root, "Use this deck for review", delegate
		{
			panel.ApplyMadeDeck(Document);
		}, () => panel.CanApplyMadeDeck);
		Widgets.Paragraph(root, "Edits and saves are available anytime. Apply while the manual review is connecting, then Start Presentation in HostBoss.");
		Action dirty = delegate
		{
			doc.Dirty = true;
		};
		PresentationOptions options = doc.Options;
		ui.Toggle(root, "Announce new apps with built-in slides", () => options.AnnounceApps, delegate(bool value)
		{
			options.AnnounceApps = value;
			dirty();
		});
		ui.Toggle(root, "Use the game's selected review slides", () => options.UseNativeSlides, delegate(bool value)
		{
			options.UseNativeSlides = value;
			dirty();
		});
		ui.Toggle(root, "Keep original boss TTS on built-in slides", () => options.NativeTts, delegate(bool value)
		{
			options.NativeTts = value;
			dirty();
		});
		ui.Field(root, "Presentation title", doc.Deck.title, delegate(string value)
		{
			doc.Deck.title = value;
			dirty();
		}, 160);
		ui.Field(root, "Presenter", doc.Deck.presenterName, delegate(string value)
		{
			doc.Deck.presenterName = value;
			dirty();
		}, 160);
		Widgets.Heading(root, "Slides");
		VisualElement parent = Widgets.WrapRow(root);
		for (int num2 = 0; num2 < doc.Deck.slides.Length; num2++)
		{
			int index = num2;
			Button button = ui.Button(parent, num2 + 1 + ". " + doc.Deck.slides[num2].title, delegate
			{
				doc.Selected = index;
				pendingAction = null;
			}, null, rebuild: true);
			Widgets.SetSelected((VisualElement)button, index == doc.Selected);
			button.EnableInClassList("hostboss-selected", index == doc.Selected);
		}
		parent = Widgets.WrapRow(root);
		ui.Button(parent, "Move up", delegate
		{
			doc.Move(-1);
		}, null, rebuild: true);
		ui.Button(parent, "Move down", delegate
		{
			doc.Move(1);
		}, null, rebuild: true);
		ui.Button(parent, "Duplicate", delegate
		{
			doc.Add(doc.Deck.slides[doc.Selected]);
		}, null, rebuild: true);
		ui.Button(parent, "Delete", doc.Remove, null, rebuild: true);
		Widgets.Heading(root, "Default templates");
		ReviewSlideDefinition[] array2 = templates;
		foreach (ReviewSlideDefinition reviewSlideDefinition in array2)
		{
			ReviewSlideDefinition template = reviewSlideDefinition;
			parent = Widgets.WrapRow(root);
			ui.Button(parent, "Add: " + reviewSlideDefinition.title, delegate
			{
				doc.Add(template);
			}, null, rebuild: true);
			ui.Button(parent, "Replace selected", delegate
			{
				if (Confirm("replace:" + doc.Selected + ":" + template.id, "Click Replace again to reset the selected slide."))
				{
					doc.Replace(template);
				}
			}, null, rebuild: true);
		}
		ReviewSlideDefinition slide = doc.Deck.slides[doc.Selected];
		SlideOptions slideOptions = options.For(slide.id);
		Widgets.Heading(root, "Slide " + (doc.Selected + 1) + " | " + slide.layout);
		ui.Choices(root, "Boss cam position", slideOptions.Camera, new string[5] { "auto", "top-left", "top-right", "bottom-left", "bottom-right" }, delegate(string value)
		{
			slideOptions.Camera = value;
			dirty();
		});
		if (slide.layout == "checklist")
		{
			ui.Toggle(root, "Use real What the Boss Heard and chosen recording slides", () => slideOptions.NativeHeard, delegate(bool value)
			{
				slideOptions.NativeHeard = value;
				dirty();
			});
		}
		ui.Field(root, "Title", slide.title, delegate(string value)
		{
			slide.title = value;
			dirty();
		}, 160);
		ui.Field(root, "Heading label", slide.kicker, delegate(string value)
		{
			slide.kicker = value;
			dirty();
		}, 160);
		ui.Field(root, "Subtitle", slide.subtitle, delegate(string value)
		{
			slide.subtitle = value;
			dirty();
		}, 400);
		ui.Field(root, "Quote / callout", slide.quote, delegate(string value)
		{
			slide.quote = value;
			dirty();
		}, 1200, multiline: true);
		ui.Toggle(root, "Boss TTS when shown", () => !string.IsNullOrWhiteSpace(slide.narration), delegate(bool value)
		{
			slide.narration = (value ? SlideDocument.TextForSpeech(slide) : "");
			dirty();
		}, rebuild: true);
		ui.Field(root, "Boss narration (blank = silent)", slide.narration, delegate(string value)
		{
			slide.narration = value;
			dirty();
		}, 1200, multiline: true);
		ui.Button(root, "Use title and quote as narration", delegate
		{
			slide.narration = SlideDocument.TextForSpeech(slide);
			dirty();
		}, null, rebuild: true);
		ui.Field(root, "Footer", slide.footer, delegate(string value)
		{
			slide.footer = value;
			dirty();
		}, 400);
		ui.Choices(root, "Theme", slide.theme, new string[3] { "white", "blue", "red" }, delegate(string value)
		{
			slide.theme = value;
			dirty();
		});
		if (slide.metrics != null)
		{
			ReviewMetricDefinition[] metrics = slide.metrics;
			foreach (ReviewMetricDefinition reviewMetricDefinition in metrics)
			{
				ReviewMetricDefinition metric = reviewMetricDefinition;
				ui.Field(root, "Metric label", metric.label, delegate(string value)
				{
					metric.label = value;
					dirty();
				}, 80);
				ui.Field(root, "Metric value", metric.value, delegate(string value)
				{
					metric.value = value;
					dirty();
				}, 120);
				ui.Field(root, "Metric detail", metric.detail, delegate(string value)
				{
					metric.detail = value;
					dirty();
				});
			}
		}
		if (slide.bullets != null || slide.layout == "checklist")
		{
			string text2 = string.Join("\n", (slide.bullets ?? new ReviewBulletDefinition[0]).Select((ReviewBulletDefinition b) => b.text).ToArray());
			ui.Field(root, "Checklist (one item per line)", text2, delegate(string value)
			{
				slide.bullets = (from line in value.Replace("\r", "").Split('\n').Take(8)
					select new ReviewBulletDefinition
					{
						text = line,
						tone = "neutral",
						iconSvgPath = "kolkata-review/svg/check.svg"
					}).ToArray();
				dirty();
			}, 1600, multiline: true);
		}
		if (slide.layout == "ranking")
		{
			ui.Toggle(root, "Use built-in scammer rankings", () => SlideDocument.UsesBuiltInRankings(slide), delegate(bool value)
			{
				string id = slide.id;
				SlideDocument.SetBuiltInRankings(slide, value);
				options.Slides.Remove(id);
				options.Slides[slide.id] = slideOptions;
				dirty();
			}, rebuild: true);
		}
		if (slide.table != null && !SlideDocument.UsesBuiltInRankings(slide))
		{
			ui.Field(root, "Column headings (separate with |)", string.Join(" | ", slide.table.headers ?? new string[0]), delegate(string value)
			{
				slide.table.headers = Cells(value);
				dirty();
			}, 400);
			ui.Field(root, "Table rows (one per line; cells with |)", string.Join("\n", (slide.table.rows ?? new string[0][]).Select((string[] cells) => string.Join(" | ", cells)).ToArray()), delegate(string value)
			{
				slide.table.rows = value.Replace("\r", "").Split('\n').Take(12)
					.Select(Cells)
					.ToArray();
				slide.table.tiers = null;
				dirty();
			}, 2000, multiline: true);
		}
		ui.Field(root, "PNG file path", imageFile, delegate(string value)
		{
			imageFile = value;
		}, 2048);
		ui.Button(root, "Browse PNGs", delegate
		{
			if (browseDirectory == null)
			{
				Browse(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
			}
			browseImages = !browseImages;
		}, null, rebuild: true);
		if (browseImages)
		{
			Widgets.Paragraph(root, browseDirectory);
			ui.Button(root, "Up one folder", delegate
			{
				DirectoryInfo parent2 = Directory.GetParent(browseDirectory);
				if (parent2 != null)
				{
					Browse(parent2.FullName);
				}
			}, null, rebuild: true);
			foreach (string item in imageFolders.Take(100))
			{
				string folder = item;
				ui.Button(root, "[Folder] " + Path.GetFileName(folder), delegate
				{
					Browse(folder);
				}, null, rebuild: true);
			}
			foreach (string item2 in imageFiles.Take(100))
			{
				string file2 = item2;
				ui.Button(root, Path.GetFileName(file2), delegate
				{
					imageFile = file2;
					browseImages = false;
				}, null, rebuild: true);
			}
		}
		ui.Button(root, "Import PNG", delegate
		{
			string imagePath = SlideImages.Import(imageFile);
			string imagePath2 = slide.imagePath;
			slide.imagePath = imagePath;
			try
			{
				SlideDocument.Validate(doc.Deck, forNetwork: false);
			}
			catch
			{
				slide.imagePath = imagePath2;
				throw;
			}
			slide.svgPath = "";
			slide.iconResource = "";
			slide.imageLabel = "Custom PNG";
			dirty();
		}, null, rebuild: true);
		ui.Button(root, "Remove image", delegate
		{
			slide.imagePath = "";
			slide.svgPath = "";
			slide.iconResource = "";
			dirty();
		}, null, rebuild: true);
		Widgets.Heading(root, "Content preview");
		ui.Live(root, () => slide.kicker + "\n" + slide.title + "\n" + slide.subtitle + "\n" + slide.quote + "\n" + slide.footer);
		if (slide.imagePath != null && slide.imagePath.StartsWith("hostboss-png:", StringComparison.Ordinal))
		{
			Image image = new Image
			{
				image = SlideImages.Get(slide.imagePath),
				scaleMode = ScaleMode.ScaleToFit
			};
			image.style.height = 150f;
			root.Add(image);
		}
		ui.Live(root, () => "Sharing: " + SlideDocument.NetworkBytes(doc.Deck) + " / " + 48000 + " bytes. Saves: " + doc.DirectoryPath);
	}
}
