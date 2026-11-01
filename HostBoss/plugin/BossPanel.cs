using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Kolkata.UI;
using Metater;
using Mirror;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScamWYF.Modding.Core.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HostBoss;

public sealed class BossPanel : MonoBehaviour
{
	public ReviewWhiteboardController Board;

	public bool ManualBossEnabled = true;

	public string Status = "F6 opens the boss panel. Speak to your team using voice chat.";

	private readonly Dictionary<uint, Vector3> fired = new Dictionary<uint, Vector3>();

	private readonly List<PlayerInput> pausedInputs = new List<PlayerInput>();

	private readonly List<ReviewReplayMoment> quotes = new List<ReviewReplayMoment>();

	private readonly List<int> quoteSlides = new List<int>();

	private CancellationTokenSource preparation;

	private Coroutine prepareRoutine;

	private bool preparing;

	private int preparedDay = -1;

	private ReviewDayOverview preparedOverview;

	private ReviewSlideDeckDefinition nativeDeck;

	private bool presentationStarted;

	private ReviewSlideDeckDefinition defaultReviewDeck;

	private ReviewSlideDeckDefinition originalReviewDeck;

	private ReviewSlideDeckDefinition selectedReviewDeck;

	private ReviewWhiteboardController selectedReviewBoard;

	private int selectedReviewDay = -1;

	private uint selectedReviewGeneration;

	private PresentationOptions activeOptions;

	private bool madeDeckActive;

	private bool makerVisible;

	private SlideMaker maker;

	private int quoteIndex;

	private int quoteDay = -1;

	private int quoteReturnSlide;

	private bool confirmFireAll;

	private bool fireAllQueued;

	public bool FiringCutscene;

	private bool fireAllCompleted;

	private bool reputationDestroyed;

	private bool chaosActive;

	private bool previousFireLights;

	private int chaosDay;

	private float chaosUntil;

	private bool open;

	private int shift = -1;

	private int customQuota = -1;

	private float nextHold;

	private uint selected;

	private string quotaText = "";

	private readonly string[] nextDayGoals = new string[3] { "Meet tomorrow's quota.", "Keep callers engaged.", "Work together as a team." };

	private ReviewSlideDefinition goalsSlide;

	private Vector2 scroll;

	private CursorLockMode previousLock;

	private bool previousVisible;

	private Rect window = new Rect(24f, 70f, 580f, 630f);

	private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	public bool CanApplyMadeDeck
	{
		get
		{
			ScammerGameManager instance = ScammerGameManager.Instance;
			if (Hooks.ManualBossActive() && instance != null && instance.IsReviewPhase && !presentationStarted && !preparing && !FiringCutscene && !fireAllQueued && Board != null && nativeDeck != null)
			{
				return preparedDay == instance.CurrentDay;
			}
			return false;
		}
	}

	public bool IsFired(uint id)
	{
		return fired.ContainsKey(id);
	}

	public void TogglePanel()
	{
		Toggle(!open);
	}

	public void OpenMaker()
	{
		makerVisible = true;
		scroll = Vector2.zero;
		Toggle(value: true);
	}

	public void OpenHostControls()
	{
		makerVisible = false;
		scroll = Vector2.zero;
		Toggle(value: true);
	}

	public string HostName()
	{
		foreach (PlayerName instance in PlayerName.Instances)
		{
			if (instance != null && instance.isLocalPlayer)
			{
				return instance.playerName;
			}
		}
		return "Host";
	}

	private void Toggle(bool value)
	{
		if (value != open)
		{
			open = value;
			if (open)
			{
				previousLock = UnityEngine.Cursor.lockState;
				previousVisible = UnityEngine.Cursor.lockState != CursorLockMode.Locked;
			}
			else
			{
				UnityEngine.Cursor.lockState = previousLock;
				UnityEngine.Cursor.visible = previousVisible;
				confirmFireAll = false;
				selected = 0u;
			}
			BlockInput(open);
		}
	}

	private void BlockInput(bool block)
	{
		if (!block)
		{
			foreach (PlayerInput pausedInput in pausedInputs)
			{
				if (pausedInput != null)
				{
					pausedInput.enabled = true;
				}
			}
			pausedInputs.Clear();
		}
		foreach (PlayerName instance in PlayerName.Instances)
		{
			if (!(instance != null) || !instance.isLocalPlayer)
			{
				continue;
			}
			MetaPlayerController component = instance.GetComponent<MetaPlayerController>();
			if (component == null)
			{
				break;
			}
			if (block)
			{
				PlayerInput[] componentsInChildren = instance.GetComponentsInChildren<PlayerInput>(includeInactive: true);
				foreach (PlayerInput playerInput in componentsInChildren)
				{
					if (playerInput.enabled)
					{
						pausedInputs.Add(playerInput);
						playerInput.enabled = false;
					}
				}
			}
			MetaPlayerBlocker blocker = component.blocker;
			HashSet<MonoBehaviour>[] array = new HashSet<MonoBehaviour>[7] { blocker.InputBlockers, blocker.MoveBlockers, blocker.PivotBlockers, blocker.JumpBlockers, blocker.SprintBlockers, blocker.StrafeBlockers, blocker.StrafeCameraBlockers };
			foreach (HashSet<MonoBehaviour> hashSet in array)
			{
				if (block)
				{
					hashSet.Add(this);
				}
				else
				{
					hashSet.Remove(this);
				}
			}
		}
	}

	private void Update()
	{
		Keyboard current = Keyboard.current;
		if (open && current != null && current.escapeKey.wasPressedThisFrame)
		{
			Toggle(value: false);
		}
		if (!Hooks.IsHost())
		{
			CancelPreparation();
			fired.Clear();
			quotes.Clear();
			quoteSlides.Clear();
			preparedOverview = null;
			nativeDeck = null;
			defaultReviewDeck = null;
			goalsSlide = null;
			madeDeckActive = false;
			ResetNextDayGoals();
			preparedDay = -1;
			customQuota = -1;
			fireAllQueued = false;
			FiringCutscene = false;
			fireAllCompleted = false;
			reputationDestroyed = false;
			chaosActive = false;
			quoteDay = -1;
			shift = -1;
			return;
		}
		ScammerGameManager instance = ScammerGameManager.Instance;
		if (instance == null)
		{
			return;
		}
		if (Board == null)
		{
			Board = UnityEngine.Object.FindObjectOfType<ReviewWhiteboardController>();
		}
		if (open && current != null && current.escapeKey.wasPressedThisFrame)
		{
			Toggle(value: false);
		}
		if (!ManualBossEnabled)
		{
			return;
		}
		if (chaosActive && (Time.unscaledTime >= chaosUntil || instance.CurrentDay != chaosDay || !instance.IsReviewPhase))
		{
			StopChaos(instance);
		}
		if (customQuota > 0 && instance.Quota != customQuota)
		{
			ApplyQuota(instance);
		}
		if (instance.CurrentDay != shift || !instance.ScammerGameStarted)
		{
			fired.Clear();
			shift = instance.CurrentDay;
			selected = 0u;
			confirmFireAll = false;
			fireAllCompleted = false;
			reputationDestroyed = false;
		}
		if (instance.CurrentDay != quoteDay && !instance.IsReviewPhase)
		{
			CancelPreparation();
			quotes.Clear();
			quoteSlides.Clear();
			preparedOverview = null;
			nativeDeck = null;
			preparedDay = -1;
			quoteDay = instance.CurrentDay;
			quoteIndex = 0;
		}
		if (open && current != null && current.escapeKey.wasPressedThisFrame)
		{
			Toggle(value: false);
		}
		if (Board == null)
		{
			Board = UnityEngine.Object.FindObjectOfType<ReviewWhiteboardController>();
		}
		if (fireAllQueued && presentationStarted && !FiringCutscene && instance.IsReviewPhase && !preparing && preparedDay == instance.CurrentDay && nativeDeck != null)
		{
			fireAllQueued = false;
			FiringCutscene = true;
			StartCoroutine(FireAllWithCutscene());
		}
		if (Time.unscaledTime < nextHold)
		{
			return;
		}
		nextHold = Time.unscaledTime + 0.5f;
		foreach (PlayerName serverInstance in PlayerName.ServerInstances)
		{
			if (serverInstance != null && fired.TryGetValue(serverInstance.netId, out var value))
			{
				PlayerTeleporter component = serverInstance.GetComponent<PlayerTeleporter>();
				if (component != null && Vector3.Distance(serverInstance.transform.position, value) > 2f)
				{
					component.ServerTeleportToPosition(value, "Fired for this shift", "HostBoss");
				}
			}
		}
	}

	private void LateUpdate()
	{
		if (open)
		{
			UnityEngine.Cursor.lockState = CursorLockMode.None;
			UnityEngine.Cursor.visible = true;
		}
	}

	private void ApplyQuota(ScammerGameManager game)
	{
		if (Hooks.IsHost() && customQuota >= 1)
		{
			int quota = game.Quota;
			game.Networkquota = customQuota;
			if (quota != customQuota)
			{
				Hooks.Call(game, "OnQuotaSyncChanged", quota, customQuota);
			}
			if (game.MoneyPoolLong < customQuota)
			{
				game.NetworkquotaReached = false;
			}
			Hooks.Call(game, "ServerCheckQuotaReached");
		}
	}

	private void OnEnable()
	{
		if (maker == null)
		{
			maker = new SlideMaker(Path.Combine(Application.persistentDataPath, "HostBoss", "Presentations"));
		}
		ScammerGameManager.OnReviewPhaseEnded += HandlePresentationEnded;
		ScammerGameManager.OnGameEnded += HandleGameEnded;
	}

	private void OnDisable()
	{
		ScammerGameManager.OnReviewPhaseEnded -= HandlePresentationEnded;
		ScammerGameManager.OnGameEnded -= HandleGameEnded;
		RestoreDefaultTitles();
		if (open)
		{
			Toggle(value: false);
		}
		CancelPreparation();
		if (chaosActive && Hooks.IsHost() && ScammerGameManager.Instance != null)
		{
			StopChaos(ScammerGameManager.Instance);
		}
	}

	public void ReleaseHostControl()
	{
		if (open)
		{
			Toggle(value: false);
		}
		CancelPreparation();
		StopAllCoroutines();
		FiringCutscene = false;
		fireAllQueued = false;
		if (Board != null)
		{
			Hooks.Call(Board, "StopFiredPresentation");
		}
		if (Hooks.IsHost())
		{
			if (reputationDestroyed || chaosActive)
			{
				EndDestroyReputation();
			}
			SetAiBoss(enableAi: true);
		}
		ManualBossEnabled = false;
	}

	private void CaptureDefaultTitles()
	{
		defaultReviewDeck = SlideDocument.Clone(nativeDeck);
		madeDeckActive = false;
		activeOptions = null;
	}

	private void RestoreDefaultTitles()
	{
		if (defaultReviewDeck != null)
		{
			nativeDeck = SlideDocument.Clone(defaultReviewDeck);
			madeDeckActive = false;
			activeOptions = null;
			goalsSlide = FindGoalsSlide(nativeDeck);
			ReindexQuoteSlides();
		}
	}

	private static ReviewSlideDefinition FindGoalsSlide(ReviewSlideDeckDefinition deck)
	{
		ReviewSlideDefinition[] slides = deck.slides;
		foreach (ReviewSlideDefinition reviewSlideDefinition in slides)
		{
			if (reviewSlideDefinition.id == "runtime-ai-goals" || reviewSlideDefinition.id == "fallback-goals")
			{
				return reviewSlideDefinition;
			}
		}
		return null;
	}

	private void ReindexQuoteSlides()
	{
		for (int i = 0; i < quotes.Count; i++)
		{
			quoteSlides[i] = -1;
			for (int j = 0; j < nativeDeck.slides.Length; j++)
			{
				ReviewReplayMoment[] replayMoments = nativeDeck.slides[j].replayMoments;
				if (replayMoments == null)
				{
					continue;
				}
				ReviewReplayMoment[] array = replayMoments;
				foreach (ReviewReplayMoment reviewReplayMoment in array)
				{
					if (reviewReplayMoment != null && reviewReplayMoment.playerName == quotes[i].playerName && reviewReplayMoment.quote == quotes[i].quote)
					{
						quoteSlides[i] = j;
						break;
					}
				}
				if (quoteSlides[i] >= 0)
				{
					break;
				}
			}
		}
		quoteReturnSlide = 0;
	}

	public bool CanNarrateSlide(ReviewWhiteboardController board)
	{
		ScammerGameManager instance = ScammerGameManager.Instance;
		if (!presentationStarted || preparing || Board != board || instance == null || !instance.IsReviewPhase || preparedDay != instance.CurrentDay)
		{
			return false;
		}
		ReviewSlideDeckDefinition reviewSlideDeckDefinition = (ReviewSlideDeckDefinition)Hooks.Get(board, "m_deck");
		int currentSlideIndex = board.CurrentSlideIndex;
		if (reviewSlideDeckDefinition == null || reviewSlideDeckDefinition.slides == null || currentSlideIndex < 0 || currentSlideIndex >= reviewSlideDeckDefinition.slides.Length)
		{
			return false;
		}
		ReviewSlideDefinition reviewSlideDefinition = reviewSlideDeckDefinition.slides[currentSlideIndex];
		if (!string.IsNullOrWhiteSpace(reviewSlideDefinition.narration))
		{
			return true;
		}
		if (reviewSlideDefinition.replayMoments != null)
		{
			ReviewReplayMoment[] replayMoments = reviewSlideDefinition.replayMoments;
			foreach (ReviewReplayMoment reviewReplayMoment in replayMoments)
			{
				if (reviewReplayMoment != null && (!string.IsNullOrWhiteSpace(reviewReplayMoment.introduction) || !string.IsNullOrWhiteSpace(reviewReplayMoment.reaction) || !string.IsNullOrWhiteSpace(reviewReplayMoment.fallback)))
				{
					return true;
				}
			}
		}
		return false;
	}

	public void ApplyMadeDeck(SlideDocument document)
	{
		if (!CanApplyMadeDeck)
		{
			throw new InvalidOperationException("Wait for a manual review to prepare, before starting the presentation.");
		}
		SlideDocument.Validate(document.Deck);
		ReviewSlideDeckDefinition draft = SlideDocument.Clone(document.Deck);
		PresentationOptions presentationOptions = SlideDocument.Clone(document.Options);
		if (presentationOptions.NeedsAi)
		{
			if (selectedReviewDeck != null && selectedReviewBoard == Board && selectedReviewDay == ScammerGameManager.Instance.CurrentDay && selectedReviewGeneration == Board.Networkm_reviewGeneration)
			{
				CommitMadeDeck(draft, presentationOptions, selectedReviewDeck);
				return;
			}
			CancelPreparation();
			preparation = new CancellationTokenSource();
			preparing = true;
			prepareRoutine = StartCoroutine(PrepareSelectedSlides(draft, presentationOptions, ScammerGameManager.Instance.CurrentDay, preparation.Token));
			Status = "Preparing the game's selected slides, real quotes and original boss TTS...";
		}
		else
		{
			CommitMadeDeck(draft, presentationOptions, originalReviewDeck ?? defaultReviewDeck);
		}
	}

	private void CommitMadeDeck(ReviewSlideDeckDefinition draft, PresentationOptions options, ReviewSlideDeckDefinition source)
	{
		ReviewSlideDeckDefinition reviewSlideDeckDefinition = SlideDocument.ResolveNativeContent(draft, source, options);
		DeckJson(reviewSlideDeckDefinition, options);
		nativeDeck = reviewSlideDeckDefinition;
		madeDeckActive = true;
		activeOptions = options;
		for (int i = 0; i < reviewSlideDeckDefinition.slides.Length; i++)
		{
			if (reviewSlideDeckDefinition.slides[i].replayMoments == null)
			{
				continue;
			}
			ReviewReplayMoment[] replayMoments = reviewSlideDeckDefinition.slides[i].replayMoments;
			foreach (ReviewReplayMoment reviewReplayMoment in replayMoments)
			{
				if (reviewReplayMoment != null && !string.IsNullOrWhiteSpace(reviewReplayMoment.quote))
				{
					AddRecordingQuote(reviewReplayMoment, i);
				}
			}
		}
		goalsSlide = FindGoalsSlide(reviewSlideDeckDefinition);
		ReindexQuoteSlides();
		int num = 0;
		ReviewSlideDefinition[] slides = reviewSlideDeckDefinition.slides;
		for (int j = 0; j < slides.Length; j++)
		{
			if (SlideDocument.IsBossHeardSlide(slides[j]))
			{
				num++;
			}
		}
		Status = "Custom presentation applied. " + num + " built-in boss-heard slides. "
			+ (options.NeedsAi && num == 0 ? "The game selected no eligible call moments for this day. " : "")
			+ "Click Start Presentation when ready.";
		Debug.Log("[HostBoss] Custom deck applied: day=" + preparedDay + " slides=" + reviewSlideDeckDefinition.slides.Length + " bossHeardSlides=" + num);
	}

	private IEnumerator PrepareSelectedSlides(ReviewSlideDeckDefinition draft, PresentationOptions options, int day, CancellationToken token)
	{
		IEnumerator work = BuildSelectedSlides(draft, options, day, token);
		while (true)
		{
			bool flag;
			try
			{
				flag = work.MoveNext();
			}
			catch (Exception ex)
			{
				preparing = false;
				prepareRoutine = null;
				ScammerGameManager instance = ScammerGameManager.Instance;
				if (!token.IsCancellationRequested && !options.UseNativeSlides && Hooks.ManualBossActive() && instance != null && instance.IsReviewPhase && instance.CurrentDay == day && !presentationStarted)
				{
					try
					{
						CommitMadeDeck(draft, options, originalReviewDeck ?? defaultReviewDeck);
						Status = "Custom presentation applied. AI notes unavailable: " + ex.Message + " Click Start Presentation when ready.";
					}
					catch (Exception ex2)
					{
						Status = "Could not apply custom presentation: " + ex2.Message;
					}
				}
				else
				{
					Status = (token.IsCancellationRequested ? "Native slide preparation canceled." : ("Could not prepare the game's slides: " + ex.Message));
				}
				Debug.LogWarning("[HostBoss] " + Status);
				yield break;
			}
			if (!flag)
			{
				break;
			}
			yield return work.Current;
		}
		preparing = false;
		prepareRoutine = null;
	}

	private IEnumerator BuildSelectedSlides(ReviewSlideDeckDefinition draft, PresentationOptions options, int day, CancellationToken token)
	{
		ReviewDaySummaryTracker tracker = ReviewDaySummaryTracker.GetOrCreate();
		// Keep the same day's evidence captured before the game resets its tracker.
		ReviewDayOverview overview = preparedOverview != null && preparedOverview.currentDay == day
			? preparedOverview : tracker.BuildDayOverview();
		ReviewReplayService service = ReviewReplayService.Ensure();
		Task metadata = ReplayCompatibility.PrepareMetadata(service, overview, token);
		while (!metadata.IsCompleted)
		{
			token.ThrowIfCancellationRequested();
			yield return null;
		}
		metadata.GetAwaiter().GetResult();
		token.ThrowIfCancellationRequested();
		typeof(ReviewDayOverview).Assembly.GetType("Kolkata.UI.ReviewEvidencePacket").GetMethod("Freeze", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[1] { overview });
		Debug.Log("[HostBoss] Selected review evidence: day=" + day + " quotes=" + (overview.featuredQuotes?.Length ?? 0));
		Task<ReviewBossReview> task = ReviewBossAiClient.TryBuildBossReviewAsync(overview, (string)Hooks.Get(Board, "m_hostAiEndpoint"), (string)Hooks.Get(Board, "m_hostAiModel"), (float)Hooks.Get(Board, "m_hostAiTimeoutSeconds"), token).AsTask();
		while (!task.IsCompleted)
		{
			token.ThrowIfCancellationRequested();
			yield return null;
		}
		ReviewBossReview review = task.GetAwaiter().GetResult();
		if (review == null)
		{
			throw new InvalidOperationException("The game's review AI did not return a review. Check its backend and try again.");
		}
		Task highlights = service.PrepareHighlights(overview, token);
		while (!highlights.IsCompleted)
		{
			token.ThrowIfCancellationRequested();
			yield return null;
		}
		highlights.GetAwaiter().GetResult();
		token.ThrowIfCancellationRequested();
		ScammerGameManager instance = ScammerGameManager.Instance;
		if (!Hooks.ManualBossActive() || instance == null || !instance.IsReviewPhase || instance.CurrentDay != day || presentationStarted)
		{
			throw new OperationCanceledException();
		}
		ReviewSlideDeckDefinition value = tracker.BuildReviewDeck(review, overview);
		selectedReviewDeck = SlideDocument.Clone(value);
		selectedReviewBoard = Board;
		selectedReviewDay = day;
		selectedReviewGeneration = Board.Networkm_reviewGeneration;
		CommitMadeDeck(draft, options, selectedReviewDeck);
	}

	private void HandlePresentationEnded(int day)
	{
		selectedReviewDeck = null;
		selectedReviewBoard = null;
		selectedReviewDay = -1;
		selectedReviewGeneration = 0u;
		RestoreDefaultTitles();
		ResetNextDayGoals();
		goalsSlide = null;
		presentationStarted = false;
		preparedDay = -1;
		defaultReviewDeck = null;
		madeDeckActive = false;
	}

	private void HandleGameEnded()
	{
		HandlePresentationEnded(-1);
	}

	private void ResetNextDayGoals()
	{
		nextDayGoals[0] = "Meet tomorrow's quota.";
		nextDayGoals[1] = "Keep callers engaged.";
		nextDayGoals[2] = "Work together as a team.";
	}

	private void ApplyNextDayGoals()
	{
		if (goalsSlide == null)
		{
			return;
		}
		List<ReviewBulletDefinition> list = new List<ReviewBulletDefinition>();
		string[] array = nextDayGoals;
		foreach (string text in array)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				list.Add(new ReviewBulletDefinition
				{
					text = text.Trim(),
					tone = "good",
					iconSvgPath = "kolkata-review/svg/check.svg"
				});
			}
		}
		if (list.Count == 0)
		{
			list.Add(new ReviewBulletDefinition
			{
				text = "No goals set by the host.",
				tone = "neutral"
			});
		}
		goalsSlide.bullets = list.ToArray();
		goalsSlide.narration = "";
	}

	private void DrawNextDayGoals()
	{
		GUILayout.Space(8f);
		GUILayout.Label("NEXT DAY GOALS");
		if (!presentationStarted && !madeDeckActive)
		{
			GUILayout.Label("Edit up to three goals. Blank goals are omitted.");
			for (int i = 0; i < nextDayGoals.Length; i++)
			{
				GUILayout.Label("Goal " + (i + 1));
				string text = GUILayout.TextField(nextDayGoals[i]);
				nextDayGoals[i] = ((text.Length > 200) ? text.Substring(0, 200) : text);
			}
			if (GUILayout.Button("Reset goals to defaults"))
			{
				ResetNextDayGoals();
			}
		}
		GUI.enabled = presentationStarted && ScammerGameManager.Instance.IsReviewPhase && !preparing && !FiringCutscene && !fireAllQueued && Board != null && nativeDeck != null && goalsSlide != null;
		if (GUILayout.Button("Show Next Day Goals"))
		{
			int num = Array.IndexOf(nativeDeck.slides, goalsSlide);
			if (num >= 0)
			{
				Hooks.Call(Board, "ServerPublishSlideIndex", num, false);
				Status = "Showing next-day goals to everyone.";
			}
		}
		GUI.enabled = true;
		GUILayout.Label(madeDeckActive ? "Custom deck goals are edited in Slide Maker. Saved decks stay saved after the review." : "Shared on the presentation; goals reset after each review. These do not change quota rules.");
	}

	private void MoveSlide(int direction)
	{
		if (Hooks.IsHost() && !(Board == null) && ScammerGameManager.Instance.IsReviewPhase && Board.SlideCount != 0)
		{
			Hooks.Call(Board, "ServerPublishSlideIndex", Mathf.Clamp(Board.CurrentSlideIndex + direction, 0, Board.SlideCount - 1), false);
			Status = "Slide " + (Board.CurrentSlideIndex + 1) + " / " + Board.SlideCount;
		}
	}

	private void Fire(PlayerName p)
	{
		if (!Hooks.IsHost() || p == null || p.isLocalPlayer || p.isHostPlayer)
		{
			return;
		}
		PlayerTeleporter component = p.GetComponent<PlayerTeleporter>();
		if (component == null)
		{
			Status = "This employee has no teleporter.";
			return;
		}
		_ = ScammerGameManager.Instance;
		Transform transform = ((NetworkManager.singleton == null) ? null : NetworkManager.singleton.GetStartPosition());
		if (transform == null)
		{
			Status = "No lobby spawn exists yet.";
			return;
		}
		fired[p.netId] = transform.position;
		component.ServerTeleportToPosition(transform.position, "Fired by host", "HostBoss");
		selected = 0u;
		Status = p.playerName + " fired for this shift. Rehire below, or wait for the next day.";
		Debug.Log("[HostBoss] Fired employee netId=" + p.netId);
	}

	private void FireAll()
	{
		ScammerGameManager instance = ScammerGameManager.Instance;
		if (!Hooks.IsHost() || instance == null || !instance.ScammerGameStarted || FiringCutscene || fireAllQueued)
		{
			return;
		}
		confirmFireAll = false;
		if (instance.DayZeroTutorialActive)
		{
			Status = "Finish the tutorial before firing the team.";
			return;
		}
		fireAllQueued = true;
		fireAllCompleted = false;
		if (!instance.IsReviewPhase)
		{
			instance.ServerEndCurrentDayEarly();
		}
		Status = "Fire All queued. Start the presentation to play the built-in You're fired cutscene.";
	}

	private IEnumerator FireAllWithCutscene()
	{
		IEnumerator work = PlayFiredCutscene();
		Exception failure = null;
		while (true)
		{
			bool flag = false;
			object obj = null;
			try
			{
				flag = work.MoveNext();
				if (flag)
				{
					obj = work.Current;
				}
			}
			catch (Exception ex)
			{
				failure = ex;
			}
			if (failure != null || !flag)
			{
				break;
			}
			yield return obj;
		}
		FiringCutscene = false;
		if (Board != null)
		{
			Hooks.Set(Board, "m_playNarration", false);
		}
		if (Hooks.IsHost())
		{
			if (failure != null)
			{
				Debug.LogError("[HostBoss] Fire All cutscene failed: " + failure);
				Status = "Cutscene failed; employees were not fired. See Player.log.";
			}
			else
			{
				confirmFireAll = false;
				fireAllCompleted = true;
				Status = "You're fired cutscene finished. Nobody was fired. Destroy Reputation is available.";
			}
		}
	}

	private Vector3[] CollectFirePositions(ScammerGameManager game)
	{
		object value = Hooks.Get(game, "reviewRoomPoint");
		object value2 = Hooks.Get(game, "firedGroundVfxCount");
		object value3 = Hooks.Get(game, "firedGroundVfxRadius");
		List<Vector3> list = new List<Vector3>();
		List<Transform> list2 = new List<Transform>();
		string[] array = new string[4] { "reviewRoomPoint", "startDayPoint", "secondFloorStartDayPoint", "thirdFloorStartDayPoint" };
		foreach (string text in array)
		{
			Transform transform = Hooks.Get(game, text) as Transform;
			if (transform != null && !list2.Contains(transform))
			{
				list2.Add(transform);
			}
		}
		if (list2.Count == 0)
		{
			list2.Add(game.transform);
		}
		try
		{
			Hooks.Set(game, "firedGroundVfxCount", 32);
			Hooks.Set(game, "firedGroundVfxRadius", 18f);
			foreach (Transform item2 in list2)
			{
				Hooks.Set(game, "reviewRoomPoint", item2);
				Vector3[] array2 = (Vector3[])Hooks.Call(game, "GetGroundFirePositions");
				if (array2 == null)
				{
					continue;
				}
				Vector3[] array3 = array2;
				foreach (Vector3 item in array3)
				{
					if (list.Count >= 128)
					{
						break;
					}
					list.Add(item);
				}
			}
		}
		finally
		{
			Hooks.Set(game, "reviewRoomPoint", value);
			Hooks.Set(game, "firedGroundVfxCount", value2);
			Hooks.Set(game, "firedGroundVfxRadius", value3);
		}
		return list.ToArray();
	}

	private void DestroyReputation()
	{
		ScammerGameManager instance = ScammerGameManager.Instance;
		if (!Hooks.IsHost() || instance == null || !instance.IsReviewPhase || !fireAllCompleted || FiringCutscene || reputationDestroyed)
		{
			return;
		}
		try
		{
			Vector3[] array = CollectFirePositions(instance);
			uint[] array2 = (uint[])Hooks.Call(instance, "GetCurrentPlayerNetIds");
			previousFireLights = instance.NetworkfireLightActive;
			chaosDay = instance.CurrentDay;
			chaosUntil = Time.unscaledTime + 30f;
			chaosActive = true;
			Hooks.Call(instance, "ServerSetFireLightActive", true);
			Hooks.Call(instance, "RpcPlayFiredEffects", array2, array, 30f);
			reputationDestroyed = true;
			Status = "Reputation destroyed! Fire everywhere for 30 seconds.";
			if (open)
			{
				Toggle(value: false);
			}
		}
		catch (Exception ex)
		{
			if (chaosActive)
			{
				StopChaos(instance);
			}
			Debug.LogError("[HostBoss] Reputation sequence failed: " + ex);
			Status = "Could not start the fire continuation. See Player.log.";
		}
	}

	private void StopChaos(ScammerGameManager game)
	{
		chaosActive = false;
		Hooks.Call(game, "ServerSetFireLightActive", game.CurrentDay == chaosDay && game.IsReviewPhase && previousFireLights);
	}

	private void SetAiBoss(bool enableAi)
	{
		if (!Hooks.IsHost() || FiringCutscene)
		{
			return;
		}
		ScammerGameManager instance = ScammerGameManager.Instance;
		try
		{
			CancelPreparation();
			RestoreDefaultTitles();
			ResetNextDayGoals();
			goalsSlide = null;
			if (chaosActive && instance != null)
			{
				StopChaos(instance);
			}
			fireAllQueued = false;
			fireAllCompleted = false;
			fired.Clear();
			ManualBossEnabled = !enableAi;
			presentationStarted = false;
			preparedDay = -1;
			nativeDeck = null;
			preparedOverview = null;
			quotes.Clear();
			quoteSlides.Clear();
			if (Board != null)
			{
				Hooks.Call(Board, "StopReplaySequence", -1);
				Hooks.Call(Board, "StopAdvanceTimer");
				Hooks.Call(Board, "CancelReviewPreparation");
				Hooks.Set(Board, "m_playNarration", enableAi);
				Hooks.Set(Board, "m_useHostAiReview", enableAi);
				Hooks.Set(Board, "m_endReviewPhaseWhenDeckCompletes", enableAi);
				Hooks.Set(Board, "m_playOnEnable", true);
				if (instance != null && instance.IsReviewPhase)
				{
					Hooks.Call(Board, "HandleDeveloperReviewRestartRequested", instance.CurrentDay);
				}
			}
			if (instance != null && instance.IsReviewPhase)
			{
				Hooks.Call(instance, "StopServerReviewRoutine");
				instance.NetworkreviewTimeRemaining = (enableAi ? Mathf.Max(60f, instance.ReviewPhaseSeconds) : 3600f);
				Coroutine value = instance.StartCoroutine((IEnumerator)Hooks.Call(instance, "ServerReviewPhaseRoutine", instance.CurrentDay));
				Hooks.Set(instance, "serverReviewRoutine", value);
			}
			if (enableAi && instance != null && customQuota > 0)
			{
				customQuota = -1;
				instance.ServerSetQuota((int)Hooks.Call(instance, "CalculateQuotaForDay", instance.CurrentDay, instance.GetCurrentQuotaPlayerCount()));
			}
			Status = (enableAi ? "AI Boss enabled. Presentation restarted from the beginning; host controls disabled." : "Host Boss enabled. Manual presentation restarted from the beginning.");
			Debug.Log("[HostBoss] Boss mode changed: AI=" + enableAi);
		}
		catch (Exception ex)
		{
			Debug.LogError("[HostBoss] Boss mode switch failed: " + ex);
			Status = "Could not switch boss mode. See Player.log.";
		}
	}

	private void EndDestroyReputation()
	{
		if (!Hooks.ManualBossActive() || (!reputationDestroyed && !chaosActive))
		{
			return;
		}
		ScammerGameManager instance = ScammerGameManager.Instance;
		if (instance == null)
		{
			return;
		}
		try
		{
			CancelPreparation();
			fireAllQueued = false;
			FiringCutscene = false;
			fireAllCompleted = false;
			fired.Clear();
			chaosActive = false;
			reputationDestroyed = false;
			Hooks.Call(instance, "StopServerRoutines");
			Hooks.Set(instance, "serverDayCompletionInProgress", false);
			Hooks.Set(instance, "serverResetAfterReview", false);
			Hooks.Call(instance, "ServerSetFireLightActive", false);
			if (Board != null)
			{
				Hooks.Call(Board, "StopFiredPresentation");
				Hooks.Call(Board, "StopReplaySequence", -1);
				Hooks.Call(Board, "ClearSyncedReviewDeck");
			}
			instance.NetworkisReviewPhase = false;
			instance.NetworkscammerGameStarted = false;
			instance.NetworkdayTimeRemaining = 0f;
			instance.NetworkreviewTimeRemaining = 0f;
			Hooks.Call(instance, "RpcScammerGameEnded");
			((Action)typeof(ScammerGameManager).GetField("OnGameEnded", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null))?.Invoke();
			Hooks.Call(instance, "ServerTeleportAllPlayersToLobby");
			presentationStarted = false;
			preparedDay = -1;
			nativeDeck = null;
			preparedOverview = null;
			quotes.Clear();
			quoteSlides.Clear();
			Status = "Reputation sequence ended. Flames stopped and everyone returned to the lobby.";
		}
		catch (Exception ex)
		{
			Debug.LogError("[HostBoss] End reputation failed: " + ex);
			Status = "Could not end reputation sequence. See Player.log.";
		}
	}

	private IEnumerator PlayFiredCutscene()
	{
		if (open)
		{
			Toggle(value: false);
		}
		ReviewDaySummaryTracker orCreate = ReviewDaySummaryTracker.GetOrCreate();
		ReviewSlideDefinition reviewSlideDefinition = (ReviewSlideDefinition)typeof(ReviewDaySummaryTracker).GetMethod("BuildFiredSlide", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[2]
		{
			orCreate.BuildDaySummary(),
			new ReviewBossReview()
		});
		reviewSlideDefinition.kicker = "HOST DECISION";
		List<ReviewSlideDefinition> list = new List<ReviewSlideDefinition>();
		ReviewSlideDefinition[] slides = nativeDeck.slides;
		foreach (ReviewSlideDefinition reviewSlideDefinition2 in slides)
		{
			if (reviewSlideDefinition2.id != reviewSlideDefinition.id)
			{
				list.Add(reviewSlideDefinition2);
			}
		}
		list.Add(reviewSlideDefinition);
		nativeDeck.slides = list.ToArray();
		Hooks.Set(Board, "m_playNarration", true);
		PublishDeck(Board, ScammerGameManager.Instance.CurrentDay, nativeDeck, list.Count - 1);
		float deadline = Time.unscaledTime + 5f;
		while (Hooks.IsHost() && Board != null && !(bool)Hooks.Get(Board, "m_firedPresentationActive") && Time.unscaledTime < deadline)
		{
			yield return null;
		}
		if (Board == null || !Hooks.IsHost() || !(bool)Hooks.Get(Board, "m_firedPresentationActive"))
		{
			throw new InvalidOperationException("Native firing presentation did not start.");
		}
		deadline = Time.unscaledTime + 45f;
		while (Hooks.IsHost() && Board != null && (bool)Hooks.Get(Board, "m_firedPresentationActive") && Time.unscaledTime < deadline)
		{
			yield return null;
		}
		if (Board != null && (bool)Hooks.Get(Board, "m_firedPresentationActive"))
		{
			throw new InvalidOperationException("Native firing presentation timed out.");
		}
	}

	private static string DeckJson(ReviewSlideDeckDefinition deck, PresentationOptions options)
	{
		string text = JsonConvert.SerializeObject(deck);
		if (options != null)
		{
			JObject jObject = JObject.Parse(text);
			PresentationOptions presentationOptions = new PresentationOptions();
			ReviewSlideDefinition[] slides = deck.slides;
			foreach (ReviewSlideDefinition reviewSlideDefinition in slides)
			{
				if (options.Slides.TryGetValue(reviewSlideDefinition.id, out var value) && value.Camera != "auto")
				{
					presentationOptions.Slides[reviewSlideDefinition.id] = new SlideOptions
					{
						Camera = value.Camera
					};
				}
			}
			jObject["hostBossOptions"] = JObject.FromObject(presentationOptions);
			text = jObject.ToString(Formatting.None);
		}
		if (Encoding.UTF8.GetByteCount(text) > 60000)
		{
			throw new InvalidOperationException("Presentation is too large to share. Remove images or slides in Slide Maker.");
		}
		return text;
	}

	private void PublishDeck(ReviewWhiteboardController board, int day, ReviewSlideDeckDefinition deck, int slideIndex)
	{
		string text = DeckJson(deck, madeDeckActive ? activeOptions : null);
		board.Networkm_syncedDeckDay = day;
		board.Networkm_syncedReviewComplete = false;
		board.Networkm_syncedSlideIndex = slideIndex;
		board.Networkm_syncedDeckJson = text;
		Hooks.Call(board, "ApplySyncedDeckJson", text, day);
		Hooks.Call(board, "RpcApplySyncedReviewDeck", text, day);
		Hooks.Call(board, "ServerPublishSlideIndex", slideIndex, false);
	}

	private void CancelPreparation()
	{
		if (preparation != null)
		{
			preparation.Cancel();
			preparation.Dispose();
			preparation = null;
		}
		if (prepareRoutine != null)
		{
			StopCoroutine(prepareRoutine);
			prepareRoutine = null;
		}
		preparing = false;
	}

	public void PrepareReview(ReviewWhiteboardController board, int day, bool refresh)
	{
		if (!Hooks.IsHost() || board == null)
		{
			return;
		}
		if (!refresh && Board == board && preparedDay == day && nativeDeck != null)
		{
			ScammerGameManager.Instance.ServerNotifyReviewDeckReady(day);
		}
		else if (!preparing || !(Board == board) || quoteDay != day)
		{
			RestoreDefaultTitles();
			CancelPreparation();
			if (refresh && quotes.Count == 0)
			{
				preparedOverview = null;
			}
			presentationStarted = false;
			Board = board;
			quoteDay = day;
			preparing = true;
			preparation = new CancellationTokenSource();
			prepareRoutine = StartCoroutine(PrepareSafely(board, day, preparation.Token));
		}
	}

	private IEnumerator PrepareSafely(ReviewWhiteboardController board, int day, CancellationToken token)
	{
		IEnumerator work = PrepareNativeReview(board, day, token);
		Exception failure = null;
		while (true)
		{
			bool flag = false;
			object obj = null;
			try
			{
				flag = work.MoveNext();
				if (flag)
				{
					obj = work.Current;
				}
			}
			catch (Exception ex)
			{
				failure = ex;
			}
			if (failure != null || !flag)
			{
				break;
			}
			yield return obj;
		}
		preparing = false;
		prepareRoutine = null;
		if (board != null)
		{
			board.Networkm_syncedDeckIsGenerating = failure == null && !presentationStarted && ManualBossEnabled;
		}
		if (failure != null && !(failure is OperationCanceledException))
		{
			fireAllQueued = false;
			Debug.LogError("[HostBoss] Native replay preparation failed: " + failure);
			Status = "Recording preparation failed. Retry with Refresh recordings. See Player.log.";
			if (Hooks.IsHost() && ScammerGameManager.Instance != null)
			{
				ScammerGameManager.Instance.ServerNotifyReviewDeckReady(day);
			}
		}
	}

	private IEnumerator PrepareNativeReview(ReviewWhiteboardController board, int day, CancellationToken token)
	{
		Hooks.Set(board, "m_playNarration", false);
		Hooks.Set(board, "m_useHostAiReview", false);
		Hooks.Set(board, "m_endReviewPhaseWhenDeckCompletes", false);
		Hooks.Set(board, "m_loop", false);
		Hooks.Call(board, "CancelReviewPreparation");
		Hooks.Call(board, "StopAdvanceTimer");
		Hooks.Call(board, "BuildShell");
		Hooks.Call(board, "Pause");
		Hooks.Call(board, "StopReplaySequence", -1);
		Hooks.Call(board, "StopSlideFadeTransition");
		Hooks.Call(board, "ClearSyncedReviewDeck");
		Hooks.Set(board, "m_playOnEnable", false);
		Hooks.Set(board, "m_deck", null);
		Hooks.Set(board, "m_currentSlideIndex", -1);
		Hooks.Set(board, "m_appliedDeckJson", "");
		Hooks.Set(board, "m_appliedDeckDay", -1);
		Hooks.Call(board, "ShowLoadingState");
		if (ScammerGameManager.Instance.IsReviewPhase)
		{
			Hooks.Call(ScammerGameManager.Instance, "RpcDeveloperReviewRestartRequested", day);
		}
		board.Networkm_reviewGeneration++;
		Hooks.Call(board, "EnsureReplayPreparationGeneration", board.Networkm_reviewGeneration);
		board.Networkm_syncedDeckIsGenerating = true;
		quotes.Clear();
		quoteSlides.Clear();
		quoteIndex = 0;
		Status = "Preparing the game's quote recordings...";
		yield return null;
		token.ThrowIfCancellationRequested();
		ReviewDaySummaryTracker tracker = ReviewDaySummaryTracker.GetOrCreate();
		if (preparedOverview == null || preparedOverview.currentDay != day)
		{
			preparedOverview = tracker.BuildDayOverview();
		}
		ReviewDayOverview overview = preparedOverview;
		ReviewReplayService service = ReviewReplayService.Ensure();
		Task metadata = ReplayCompatibility.PrepareMetadata(service, overview, token);
		while (!metadata.IsCompleted)
		{
			token.ThrowIfCancellationRequested();
			yield return null;
		}
		metadata.GetAwaiter().GetResult();
		token.ThrowIfCancellationRequested();
		typeof(ReviewDayOverview).Assembly.GetType("Kolkata.UI.ReviewEvidencePacket").GetMethod("Freeze", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[1] { overview });
		Task highlights = service.PrepareHighlights(overview, token);
		while (!highlights.IsCompleted)
		{
			token.ThrowIfCancellationRequested();
			yield return null;
		}
		highlights.GetAwaiter().GetResult();
		token.ThrowIfCancellationRequested();
		ReviewSlideDeckDefinition reviewSlideDeckDefinition = tracker.BuildReviewDeck(null, overview);
		originalReviewDeck = SlideDocument.Clone(reviewSlideDeckDefinition);
		List<ReviewSlideDefinition> list = new List<ReviewSlideDefinition>();
		ReviewSlideDefinition[] slides = reviewSlideDeckDefinition.slides;
		foreach (ReviewSlideDefinition reviewSlideDefinition in slides)
		{
			if (!(bool)Hooks.Call(board, "IsFiredSlide", reviewSlideDefinition))
			{
				reviewSlideDefinition.narration = "";
				list.Add(reviewSlideDefinition);
			}
		}
		goalsSlide = null;
		foreach (ReviewSlideDefinition item in list)
		{
			if (!(item.id != "runtime-ai-goals"))
			{
				if (item.layout == "early-access")
				{
					item.id = "hostboss-prestige-summary";
				}
				else
				{
					goalsSlide = item;
				}
			}
		}
		if (goalsSlide == null)
		{
			goalsSlide = new ReviewSlideDefinition
			{
				id = "runtime-ai-goals",
				layout = "checklist",
				theme = "blue",
				kicker = "Next Day",
				title = "Tomorrow's Goals",
				subtitle = "Your host's goals for the next shift.",
				durationSeconds = 8f,
				narration = ""
			};
			list.Add(goalsSlide);
		}
		ApplyNextDayGoals();
		list.Add(new ReviewSlideDefinition
		{
			id = "host-verdict",
			layout = "quote",
			theme = "blue",
			kicker = "HOST BOSS",
			title = "The floor is yours",
			quote = "Your host decides who stays on the team.",
			subtitle = "Waiting for the host to finish the review.",
			narration = "",
			footer = "Host Boss | Manual review"
		});
		reviewSlideDeckDefinition.slides = list.ToArray();
		reviewSlideDeckDefinition.presenterName = HostName();
		reviewSlideDeckDefinition.presenterTitle = "Host / Boss";
		reviewSlideDeckDefinition.title = "Day " + day + " | Host review";
		nativeDeck = reviewSlideDeckDefinition;
		preparedDay = day;
		CaptureDefaultTitles();
		for (int j = 0; j < reviewSlideDeckDefinition.slides.Length; j++)
		{
			ReviewReplayMoment[] replayMoments = reviewSlideDeckDefinition.slides[j].replayMoments;
			if (replayMoments == null)
			{
				continue;
			}
			ReviewReplayMoment[] array = replayMoments;
			foreach (ReviewReplayMoment reviewReplayMoment in array)
			{
				if (reviewReplayMoment != null && !string.IsNullOrWhiteSpace(reviewReplayMoment.quote))
				{
					AddRecordingQuote(reviewReplayMoment, j);
				}
			}
		}
		if (overview.featuredQuotes != null)
		{
			ReviewFeaturedQuote[] featuredQuotes = overview.featuredQuotes;
			foreach (ReviewFeaturedQuote reviewFeaturedQuote in featuredQuotes)
			{
				if (reviewFeaturedQuote != null && !string.IsNullOrWhiteSpace(reviewFeaturedQuote.text))
				{
					AddRecordingQuote(new ReviewReplayMoment
					{
						playerName = reviewFeaturedQuote.playerName,
						quote = reviewFeaturedQuote.text,
						replayId = reviewFeaturedQuote.replayId
					}, -1);
				}
			}
		}
		ScammerGameManager.Instance.ServerNotifyReviewDeckReady(day);
		Status = "Presentation ready. Click Start Presentation; the screen stays connecting until then. " + quotes.Count + " quotes available.";
		Debug.Log("[HostBoss] Native replay deck ready: day=" + day + " slides=" + list.Count + " recordings=" + quotes.Count);
	}

	private void AddRecordingQuote(ReviewReplayMoment moment, int slide)
	{
		for (int i = 0; i < quotes.Count; i++)
		{
			if (!((quotes[i].playerName ?? "") != (moment.playerName ?? "")) && !(quotes[i].quote != moment.quote))
			{
				if (string.IsNullOrWhiteSpace(quotes[i].replayId) && !string.IsNullOrWhiteSpace(moment.replayId))
				{
					quotes[i] = moment;
					quoteSlides[i] = slide;
				}
				return;
			}
		}
		quotes.Add(moment);
		quoteSlides.Add(slide);
	}

	private void StartPresentation()
	{
		ScammerGameManager instance = ScammerGameManager.Instance;
		if (!Hooks.ManualBossActive() || instance == null || !instance.IsReviewPhase || preparing || presentationStarted || Board == null || nativeDeck == null || preparedDay != instance.CurrentDay)
		{
			return;
		}
		try
		{
			Hooks.Set(Board, "m_playOnEnable", true);
			if (!madeDeckActive)
			{
				ApplyNextDayGoals();
			}
			presentationStarted = true;
			Board.Networkm_syncedDeckIsGenerating = false;
			PublishDeck(Board, instance.CurrentDay, nativeDeck, 0);
			Status = "Presentation started at slide 1.";
			Debug.Log("[HostBoss] Host started presentation: day=" + instance.CurrentDay);
		}
		catch (Exception ex)
		{
			presentationStarted = false;
			Debug.LogError("[HostBoss] Start presentation failed: " + ex);
			Status = "Could not start presentation. See Player.log.";
		}
	}

	private void ShowQuote()
	{
		ScammerGameManager instance = ScammerGameManager.Instance;
		if (!Hooks.IsHost() || instance == null || !instance.IsReviewPhase || Board == null || quotes.Count == 0)
		{
			return;
		}
		try
		{
			if (!quoteSlides.Contains(Board.CurrentSlideIndex))
			{
				quoteReturnSlide = Board.CurrentSlideIndex;
			}
			ReviewReplayMoment reviewReplayMoment = quotes[quoteIndex];
			int num = quoteSlides[quoteIndex];
			if (num < 0)
			{
				num = 0;
			}
			ReviewSlideDefinition reviewSlideDefinition = JsonConvert.DeserializeObject<ReviewSlideDefinition>(JsonConvert.SerializeObject(nativeDeck.slides[num]));
			reviewSlideDefinition.id = "hostboss-quote-only";
			reviewSlideDefinition.imagePath = "";
			reviewSlideDefinition.narration = "";
			reviewSlideDefinition.replayMoments = new ReviewReplayMoment[1]
			{
				new ReviewReplayMoment
				{
					playerName = reviewReplayMoment.playerName,
					quote = reviewReplayMoment.quote,
					replayId = ""
				}
			};
			List<ReviewSlideDefinition> list = new List<ReviewSlideDefinition>();
			ReviewSlideDefinition[] slides = nativeDeck.slides;
			foreach (ReviewSlideDefinition reviewSlideDefinition2 in slides)
			{
				if (reviewSlideDefinition2.id != "hostboss-quote-only")
				{
					list.Add(reviewSlideDefinition2);
				}
			}
			list.Add(reviewSlideDefinition);
			nativeDeck.slides = list.ToArray();
			PublishDeck(Board, instance.CurrentDay, nativeDeck, list.Count - 1);
			StartCoroutine(EnsureQuoteOnlyVisible(Board, reviewSlideDefinition, list.Count - 1));
			Status = "Showing built-in quote from " + reviewReplayMoment.playerName + ".";
			Debug.Log("[HostBoss] Quote-only requested: day=" + instance.CurrentDay + " index=" + (list.Count - 1));
			if (open)
			{
				Toggle(value: false);
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("[HostBoss] Show quote failed: " + ex);
			Status = "Could not show quote. See Player.log.";
		}
	}

	private IEnumerator EnsureQuoteOnlyVisible(ReviewWhiteboardController board, ReviewSlideDefinition slide, int index)
	{
		yield return new WaitForSeconds(0.65f);
		if (Hooks.IsHost() && !(board == null) && board.CurrentSlideIndex == index && !(ScammerGameManager.Instance == null) && ScammerGameManager.Instance.IsReviewPhase)
		{
			ReviewSlideDeckDefinition reviewSlideDeckDefinition = (ReviewSlideDeckDefinition)Hooks.Get(board, "m_deck");
			if (reviewSlideDeckDefinition != null && index < reviewSlideDeckDefinition.slides.Length && !(reviewSlideDeckDefinition.slides[index].id != "hostboss-quote-only"))
			{
				Hooks.PresentQuoteOnly(board, slide);
				Debug.Log("[HostBoss] Quote-only applied on native slide " + index);
			}
		}
	}

	public IEnumerator BroadcastQuoteOnly(ReviewWhiteboardController board, ReviewSlideDefinition slide, uint beat)
	{
		yield return new WaitForSeconds(0.4f);
		if (Hooks.IsHost() && !(board == null) && board.Networkm_replayBeat == beat)
		{
			ReviewSlideDeckDefinition reviewSlideDeckDefinition = (ReviewSlideDeckDefinition)Hooks.Get(board, "m_deck");
			int currentSlideIndex = board.CurrentSlideIndex;
			if (reviewSlideDeckDefinition != null && currentSlideIndex >= 0 && currentSlideIndex < reviewSlideDeckDefinition.slides.Length && !(reviewSlideDeckDefinition.slides[currentSlideIndex].id != "hostboss-quote-only"))
			{
				ReviewReplayMoment reviewReplayMoment = slide.replayMoments[0];
				Hooks.Call(board, "RpcReplayPresentation", board.Networkm_reviewGeneration, beat, reviewReplayMoment.playerName, reviewReplayMoment.quote, "", -1.0);
			}
		}
	}

	private void PlayRecording()
	{
		ScammerGameManager instance = ScammerGameManager.Instance;
		if (!Hooks.ManualBossActive() || instance == null || !instance.IsReviewPhase || !presentationStarted || preparing || FiringCutscene || Board == null || nativeDeck == null || quotes.Count == 0)
		{
			return;
		}
		try
		{
			ReviewReplayMoment reviewReplayMoment = quotes[quoteIndex];
			object[] args = new object[2] { reviewReplayMoment.replayId, null };
			if (string.IsNullOrWhiteSpace(reviewReplayMoment.replayId) || !(bool)Hooks.Call(ReviewReplayService.Ensure(), "TryGetSelection", args))
			{
				Status = "No playable clip for this quote. Try another quote or Refresh recordings after recording uploads finish.";
				return;
			}
			if (!quoteSlides.Contains(Board.CurrentSlideIndex))
			{
				quoteReturnSlide = Board.CurrentSlideIndex;
			}
			ReviewSlideDefinition reviewSlideDefinition = JsonConvert.DeserializeObject<ReviewSlideDefinition>(JsonConvert.SerializeObject(nativeDeck.slides[0]));
			reviewSlideDefinition.id = "hostboss-recording";
			reviewSlideDefinition.narration = "";
			reviewSlideDefinition.imagePath = "";
			reviewSlideDefinition.replayMoments = new ReviewReplayMoment[1] { reviewReplayMoment };
			List<ReviewSlideDefinition> list = new List<ReviewSlideDefinition>();
			ReviewSlideDefinition[] slides = nativeDeck.slides;
			foreach (ReviewSlideDefinition reviewSlideDefinition2 in slides)
			{
				if (reviewSlideDefinition2.id != "hostboss-recording")
				{
					list.Add(reviewSlideDefinition2);
				}
			}
			list.Add(reviewSlideDefinition);
			nativeDeck.slides = list.ToArray();
			Hooks.Call(Board, "StopReplaySequence", -1);
			PublishDeck(Board, instance.CurrentDay, nativeDeck, list.Count - 1);
			StartCoroutine(EnsureRecordingPlaying(Board, reviewSlideDefinition, list.Count - 1));
			Status = "Playing built-in recording from " + reviewReplayMoment.playerName + ".";
			if (open)
			{
				Toggle(value: false);
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("[HostBoss] Play recording failed: " + ex);
			Status = "Could not play recording. See Player.log.";
		}
	}

	private IEnumerator EnsureRecordingPlaying(ReviewWhiteboardController board, ReviewSlideDefinition slide, int index)
	{
		yield return new WaitForSeconds(0.65f);
		if (Hooks.ManualBossActive() && !(board == null) && !(Board != board) && presentationStarted && !(ScammerGameManager.Instance == null) && ScammerGameManager.Instance.IsReviewPhase && board.CurrentSlideIndex == index)
		{
			ReviewSlideDeckDefinition reviewSlideDeckDefinition = (ReviewSlideDeckDefinition)Hooks.Get(board, "m_deck");
			if (reviewSlideDeckDefinition != null && index < reviewSlideDeckDefinition.slides.Length && !(reviewSlideDeckDefinition.slides[index].id != "hostboss-recording") && reviewSlideDeckDefinition.slides[index].replayMoments != null && !(reviewSlideDeckDefinition.slides[index].replayMoments[0].replayId != slide.replayMoments[0].replayId) && !(bool)Hooks.Get(board, "m_replaySequenceActive"))
			{
				Hooks.Call(board, "StartReplaySequence", slide);
			}
		}
	}

	private void ReturnToPresentation()
	{
		if (Hooks.IsHost() && !(Board == null))
		{
			Hooks.Call(Board, "StopReplaySequence", -1);
			Hooks.Call(Board, "RpcHideReplayPresentation", Board.Networkm_reviewGeneration, Board.Networkm_replayBeat);
			Hooks.Call(Board, "ServerPublishSlideIndex", Mathf.Clamp(quoteReturnSlide, 0, Board.SlideCount - 1), false);
		}
	}

	private void FinishReview()
	{
		if (Hooks.IsHost())
		{
			ScammerGameManager instance = ScammerGameManager.Instance;
			if (!(instance == null) && instance.IsReviewPhase)
			{
				Hooks.Set(instance, "serverResetAfterReview", false);
				instance.RequestEndReviewPhase();
				Status = "Review finished. Team approved to continue.";
			}
		}
	}

	private void OnGUI()
	{
		GUI.depth = -10000;
		GUI.color = Color.white;
		if (!open)
		{
			GUI.Box(new Rect(16f, 16f, 240f, 30f), (!Hooks.IsHost()) ? "SLIDE MAKER  |  F6 to open" : (ManualBossEnabled ? "HOST BOSS  |  F6 controls" : "AI BOSS  |  F6 / slide maker"));
			return;
		}
		window.width = Mathf.Min(makerVisible ? 960 : 580, Screen.width - 24);
		window.height = Mathf.Min(650, Screen.height - 50);
		window.x = Mathf.Clamp(window.x, 0f, (float)Screen.width - window.width);
		window.y = Mathf.Clamp(window.y, 0f, (float)Screen.height - window.height);
		window = GUILayout.Window(194061, window, Draw, "HOST BOSS  /  F6 to close");
	}

	private void Draw(int id)
	{
		ScammerGameManager instance = ScammerGameManager.Instance;
		scroll = GUILayout.BeginScrollView(scroll);
		GUILayout.BeginHorizontal();
		if (GUILayout.Toggle(makerVisible, "Slide Maker") && !makerVisible)
		{
			makerVisible = true;
			scroll = Vector2.zero;
		}
		if (GUILayout.Toggle(!makerVisible, "Host Controls") && makerVisible)
		{
			makerVisible = false;
			scroll = Vector2.zero;
		}
		GUILayout.EndHorizontal();
		if (makerVisible)
		{
			maker.Draw(this);
			GUILayout.EndScrollView();
			GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
			return;
		}
		if (!Hooks.IsHost())
		{
			GUILayout.Label("Slide Maker is available anytime. Host a lobby to use boss controls.");
			GUILayout.EndScrollView();
			GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
			return;
		}
		if (instance == null)
		{
			GUILayout.Label("Waiting for the game manager...");
			GUILayout.EndScrollView();
			GUI.DragWindow();
			return;
		}
		GUI.enabled = !FiringCutscene;
		bool flag = GUILayout.Toggle(!ManualBossEnabled, "AI Boss — enable AI / disable Host Boss");
		if (flag == ManualBossEnabled)
		{
			SetAiBoss(flag);
		}
		GUI.enabled = true;
		if (!ManualBossEnabled)
		{
			GUILayout.Label("AI Boss is in control. Turn the toggle off to restore host controls.");
			GUILayout.Label(Status);
			GUILayout.EndScrollView();
			GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
			return;
		}
		if (instance.IsReviewPhase && defaultReviewDeck != null && !presentationStarted && !preparing && GUILayout.Button("Restore default presentation"))
		{
			RestoreDefaultTitles();
			Status = "Default presentation restored; saved decks are unchanged.";
		}
		if (instance.IsReviewPhase && !preparing && nativeDeck != null && preparedDay == instance.CurrentDay)
		{
			DrawNextDayGoals();
		}
		GUI.enabled = instance.IsReviewPhase && !preparing && !presentationStarted && nativeDeck != null && preparedDay == instance.CurrentDay;
		if (GUILayout.Button(preparing ? "Preparing presentation..." : "Start Presentation", GUILayout.Height(36f)))
		{
			StartPresentation();
		}
		GUI.enabled = true;
		if (!presentationStarted)
		{
			GUILayout.Label("Presentation is connecting. Click Start Presentation when ready.");
		}
		GUILayout.Label("Day " + instance.CurrentDay + "  |  " + (instance.IsReviewPhase ? "MANUAL REVIEW" : "SHIFT IN PROGRESS"));
		GUILayout.Label("Quota: $" + instance.MoneyPool + " / $" + instance.Quota + "    Remaining: $" + Mathf.Max(0, instance.Quota - instance.MoneyPool));
		GUILayout.Space(8f);
		GUILayout.BeginHorizontal();
		quotaText = GUILayout.TextField(quotaText, GUILayout.Width(130f));
		if (GUILayout.Button("Set quota"))
		{
			if (int.TryParse(quotaText, out var result) && result >= 1 && result <= 100000000)
			{
				customQuota = result;
				ApplyQuota(instance);
				Status = "Quota set to $" + result + ". This override stays active until reset.";
			}
			else
			{
				Status = "Enter a quota from 1 to 100,000,000.";
			}
		}
		if (GUILayout.Button("Auto", GUILayout.Width(60f)))
		{
			customQuota = -1;
			Status = "Automatic quotas resume at the next shift.";
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(12f);
		GUI.enabled = presentationStarted && instance.IsReviewPhase && !preparing && !FiringCutscene && Board != null && Board.SlideCount > 0;
		GUILayout.Label((Board == null) ? "Slides loading..." : ("Presentation: " + (Board.CurrentSlideIndex + 1) + " / " + Board.SlideCount));
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("< Previous", GUILayout.Height(34f)))
		{
			MoveSlide(-1);
		}
		if (GUILayout.Button("Next >", GUILayout.Height(34f)))
		{
			MoveSlide(1);
		}
		GUILayout.EndHorizontal();
		GUI.enabled = instance.ScammerGameStarted && !instance.IsReviewPhase && !instance.DayZeroTutorialActive;
		if (GUILayout.Button("End shift / summon review"))
		{
			instance.ServerEndCurrentDayEarly();
			Status = "Ending shift...";
		}
		GUI.enabled = presentationStarted && instance.IsReviewPhase && !FiringCutscene && !fireAllQueued;
		if (GUILayout.Button("Finish review / approve next day"))
		{
			FinishReview();
		}
		GUI.enabled = true;
		GUILayout.Space(12f);
		GUILayout.Label("CALL QUOTES  |  built-in video and audio recordings");
		GUI.enabled = instance.IsReviewPhase && !preparing && !FiringCutscene && Board != null;
		if (GUILayout.Button(preparing ? "Preparing recordings..." : "Refresh recordings"))
		{
			PrepareReview(Board, instance.CurrentDay, refresh: true);
		}
		GUI.enabled = presentationStarted && instance.IsReviewPhase && !preparing && !FiringCutscene && quotes.Count > 0;
		if (quotes.Count > 0)
		{
			GUILayout.Label(quoteIndex + 1 + " / " + quotes.Count + "  |  " + quotes[quoteIndex].playerName);
			GUILayout.Label(quotes[quoteIndex].quote, new GUIStyle(GUI.skin.label)
			{
				wordWrap = true
			});
		}
		else
		{
			GUILayout.Label(preparing ? "Waiting for built-in recording metadata..." : "No built-in recordings ready. Finish calls, then refresh during review.");
		}
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("< Quote"))
		{
			quoteIndex = (quoteIndex + quotes.Count - 1) % quotes.Count;
		}
		if (GUILayout.Button("Show Quote"))
		{
			ShowQuote();
		}
		bool num = (GUI.enabled = GUI.enabled);
		if (GUILayout.Button("Play Recording"))
		{
			PlayRecording();
		}
		GUI.enabled = num;
		if (GUILayout.Button("Quote >"))
		{
			quoteIndex = (quoteIndex + 1) % quotes.Count;
		}
		GUILayout.EndHorizontal();
		GUI.enabled = presentationStarted && instance.IsReviewPhase && !preparing && !FiringCutscene && Board != null && Board.SlideCount > 0;
		if (GUILayout.Button("Return to presentation"))
		{
			ReturnToPresentation();
		}
		GUI.enabled = true;
		GUILayout.Space(12f);
		GUILayout.Label("EMPLOYEES  |  earned this shift");
		GUI.enabled = instance.ScammerGameStarted && !FiringCutscene && !fireAllQueued;
		if (GUILayout.Button(confirmFireAll ? "Confirm Fire All cutscene only" : "Fire All"))
		{
			if (confirmFireAll)
			{
				FireAll();
			}
			else
			{
				confirmFireAll = true;
				selected = 0u;
				Status = "Confirm to play the You're fired cutscene. Nobody will be fired.";
			}
		}
		if (confirmFireAll && GUILayout.Button("Cancel Fire All"))
		{
			confirmFireAll = false;
		}
		GUI.enabled = instance.IsReviewPhase && fireAllCompleted && !FiringCutscene && !reputationDestroyed;
		if (GUILayout.Button("DESTROY REPUTATION", GUILayout.Height(34f)))
		{
			DestroyReputation();
		}
		GUI.enabled = reputationDestroyed || chaosActive;
		if (GUILayout.Button("END DESTROY REPUTATION / return everyone to lobby", GUILayout.Height(34f)))
		{
			EndDestroyReputation();
		}
		GUI.enabled = true;
		GUILayout.Label(chaosActive ? "Fire continuation active (30 seconds)." : "Destroy Reputation unlocks after the Fire All cutscene.");
		foreach (PlayerName serverInstance in PlayerName.ServerInstances)
		{
			if (serverInstance == null)
			{
				continue;
			}
			GUILayout.BeginHorizontal();
			bool flag3 = IsFired(serverInstance.netId);
			GUILayout.Label(serverInstance.playerName + "  |  $" + instance.GetEarnedMoney(serverInstance) + (flag3 ? "  [FIRED]" : ""), GUILayout.Width(340f));
			GUI.enabled = instance.ScammerGameStarted && !serverInstance.isLocalPlayer && !serverInstance.isHostPlayer;
			if (flag3)
			{
				if (GUILayout.Button("Rehire", GUILayout.Width(100f)))
				{
					fired.Remove(serverInstance.netId);
					PlayerTeleporter component = serverInstance.GetComponent<PlayerTeleporter>();
					if (component != null)
					{
						component.ServerTeleportToPosition(PlayerModeManager.LocalPlayerInstance.transform.position, "Rehired by host", "HostBoss");
					}
					Status = serverInstance.playerName + " rehired.";
				}
			}
			else if (GUILayout.Button((selected == serverInstance.netId) ? "Confirm fire" : "Fire", GUILayout.Width(100f)))
			{
				if (selected == serverInstance.netId)
				{
					Fire(serverInstance);
				}
				else
				{
					selected = serverInstance.netId;
					Status = "Click Confirm fire to remove " + serverInstance.playerName + " from this shift.";
				}
			}
			GUI.enabled = true;
			GUILayout.EndHorizontal();
		}
		GUILayout.Space(14f);
		GUILayout.Label(Status);
		GUILayout.Label("Fired employees stay connected. Their earnings do not count until rehired.\nFiring expires at the next day. Reviews wait for your decision.");
		GUILayout.EndScrollView();
		GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
	}

	public void BuildLaptopMaker(LaptopView ui, VisualElement root)
	{
		maker.BuildLaptop(ui, root, this);
	}

	public void BuildLaptopControls(LaptopView ui, VisualElement root)
	{
		Widgets.Heading(root, "HOSTBOSS");
		ui.Live(root, () => Status);
		ui.Toggle(root, "Enable AI Boss", () => !ManualBossEnabled, SetAiBoss, rebuild: false, () => !FiringCutscene);
		Func<ScammerGameManager> game = () => ScammerGameManager.Instance;
		Func<bool> manual = () => game() != null && ManualBossEnabled;
		Func<bool> review = () => manual() && game().IsReviewPhase;
		Func<bool> ready = () => review() && !preparing && !presentationStarted && nativeDeck != null && preparedDay == game().CurrentDay;
		Func<bool> presenting = () => review() && presentationStarted && !preparing && !FiringCutscene;
		ui.Live(root, () => (!(game() == null)) ? ("Day " + game().CurrentDay + " | Quota: $" + game().MoneyPool + " / $" + game().Quota) : "Waiting for game manager...");
		ui.Live(root, () => (!preparing) ? ((!presentationStarted) ? "Connecting — click Start Presentation when ready." : "Presentation started") : "Preparing presentation...");
		ui.Button(root, "Start Presentation", StartPresentation, ready);
		ui.Button(root, "Restore default presentation", RestoreDefaultTitles, () => ready() && defaultReviewDeck != null);
		VisualElement parent = Widgets.WrapRow(root);
		ui.Button(parent, "Previous slide", delegate
		{
			MoveSlide(-1);
		}, presenting);
		ui.Button(parent, "Next slide", delegate
		{
			MoveSlide(1);
		}, presenting);
		ui.Live(root, () => (!(Board == null)) ? ("Slide " + (Board.CurrentSlideIndex + 1) + " / " + Board.SlideCount) : "Slides loading...");
		ui.Field(root, "Quota override", quotaText, delegate(string value)
		{
			quotaText = value;
		}, 9);
		ui.Button(root, "Set quota", delegate
		{
			if (!int.TryParse(quotaText, out var result) || result < 1 || result > 100000000)
			{
				Status = "Enter a quota from 1 to 100,000,000.";
			}
			else
			{
				customQuota = result;
				ApplyQuota(game());
				Status = "Quota set to $" + result + ".";
			}
		}, manual);
		ui.Button(root, "Automatic quota next shift", delegate
		{
			customQuota = -1;
			Status = "Automatic quotas resume at the next shift.";
		}, manual);
		Widgets.Heading(root, "Next day goals");
		for (int num = 0; num < nextDayGoals.Length; num++)
		{
			int index = num;
			TextField field = ui.Field(root, "Goal " + (num + 1), nextDayGoals[num], delegate(string value)
			{
				if (ready() && !madeDeckActive)
				{
					nextDayGoals[index] = value;
				}
			});
			ui.Watch(delegate
			{
				field.SetEnabled(ready() && !madeDeckActive);
				FocusController focusController = field.focusController;
				bool? obj;
				if (focusController == null)
				{
					obj = null;
				}
				else
				{
					Focusable focusedElement = focusController.focusedElement;
					obj = ((focusedElement != null) ? new bool?(!focusedElement.Equals(field)) : ((bool?)null));
				}
				if (obj ?? true)
				{
					field.SetValueWithoutNotify(nextDayGoals[index]);
				}
			});
		}
		ui.Button(root, "Show Next Day Goals", delegate
		{
			int num2 = Array.IndexOf(nativeDeck.slides, goalsSlide);
			if (num2 >= 0)
			{
				Hooks.Call(Board, "ServerPublishSlideIndex", num2, false);
			}
		}, () => presenting() && goalsSlide != null && nativeDeck != null);
		ui.Button(root, "Reset goals to defaults", ResetNextDayGoals, () => ready() && !madeDeckActive, rebuild: true);
		Widgets.Heading(root, "Quotes and recordings");
		ui.Live(root, () => (quotes.Count != 0) ? (quoteIndex + 1 + " / " + quotes.Count + " | " + quotes[quoteIndex].playerName + "\n" + quotes[quoteIndex].quote) : "No built-in recordings ready.");
		ui.Button(root, "Refresh recordings", delegate
		{
			PrepareReview(Board, game().CurrentDay, refresh: true);
		}, () => review() && !preparing && !FiringCutscene && Board != null);
		parent = Widgets.WrapRow(root);
		Func<bool> func = () => presenting() && quotes.Count > 0;
		ui.Button(parent, "Previous quote", delegate
		{
			quoteIndex = (quoteIndex + quotes.Count - 1) % quotes.Count;
		}, func);
		ui.Button(parent, "Show Quote", ShowQuote, func);
		ui.Button(parent, "Play Recording", PlayRecording, func);
		ui.Button(parent, "Next quote", delegate
		{
			quoteIndex = (quoteIndex + 1) % quotes.Count;
		}, func);
		ui.Button(root, "Return to presentation", ReturnToPresentation, presenting);
		Widgets.Heading(root, "Employees");
		ui.Button(root, "Refresh employees", delegate
		{
		}, null, rebuild: true);
		foreach (PlayerName serverInstance in PlayerName.ServerInstances)
		{
			if (serverInstance == null)
			{
				continue;
			}
			PlayerName p = serverInstance;
			ui.Live(root, () => (!(p == null)) ? (p.playerName + " | $" + ((!(game() == null)) ? game().GetEarnedMoney(p) : 0) + (IsFired(p.netId) ? " [FIRED]" : "")) : "Disconnected");
			ui.Button(root, "Fire / confirm fire: " + p.playerName, delegate
			{
				if (selected == p.netId)
				{
					Fire(p);
				}
				else
				{
					selected = p.netId;
					Status = "Click again to confirm firing " + p.playerName + ".";
				}
			}, () => manual() && game().ScammerGameStarted && !FiringCutscene && p != null && !p.isLocalPlayer && !p.isHostPlayer && !IsFired(p.netId));
			ui.Button(root, "Rehire: " + p.playerName, delegate
			{
				fired.Remove(p.netId);
				PlayerTeleporter component = p.GetComponent<PlayerTeleporter>();
				if (component != null)
				{
					component.ServerTeleportToPosition(PlayerModeManager.LocalPlayerInstance.transform.position, "Rehired by host", "HostBoss");
				}
			}, () => manual() && p != null && IsFired(p.netId));
		}
		ui.Live(root, () => (!confirmFireAll) ? "Fire All plays the cutscene only." : "Click Fire All again to confirm the cutscene. Nobody will be fired.");
		ui.Button(root, "Fire All", delegate
		{
			if (confirmFireAll)
			{
				FireAll();
			}
			else
			{
				confirmFireAll = true;
			}
		}, () => manual() && game().ScammerGameStarted && !FiringCutscene && !fireAllQueued);
		ui.Button(root, "Cancel Fire All", delegate
		{
			confirmFireAll = false;
		});
		ui.Button(root, "DESTROY REPUTATION", DestroyReputation, () => review() && fireAllCompleted && !FiringCutscene && !reputationDestroyed);
		ui.Button(root, "END DESTROY REPUTATION / return everyone to lobby", EndDestroyReputation, () => manual() && (reputationDestroyed || chaosActive));
		ui.Button(root, "End shift / summon review", delegate
		{
			game().ServerEndCurrentDayEarly();
		}, () => manual() && game().ScammerGameStarted && !game().IsReviewPhase && !game().DayZeroTutorialActive);
		ui.Button(root, "Finish review / approve next day", FinishReview, () => presenting() && !fireAllQueued);
	}
}
