using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Kolkata.UI;

namespace HostBoss {
public static class Hooks {
    public static BossPanel Panel;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    public static object Get(object o, string name) { return o.GetType().GetField(name, Flags).GetValue(o); }
    public static void Set(object o, string name, object value) { o.GetType().GetField(name, Flags).SetValue(o, value); }
    public static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, Flags).Invoke(o, args); }
    public static void Init() {
        if (Panel != null) return;
        GameObject go = new GameObject("HostBoss");
        UnityEngine.Object.DontDestroyOnLoad(go);
        Panel = go.AddComponent<BossPanel>();
#if BEPINEX
        Debug.Log("[HostBoss] Loaded 1.5.0 BepInEx plugin. Host controls: F6.");
#else
        Debug.Log("[HostBoss] Loaded 1.4.2. Recording playback fix; Fire All cutscene only. Host controls: F6.");
#endif
    }
    public static bool IsHost() { return NetworkServer.active && NetworkClient.active; }
    public static bool ManualBossActive() { return IsHost() && Panel != null && Panel.ManualBossEnabled; }
    public static bool SuppressAutomaticBoss() { return ManualBossActive() && !Panel.FiringCutscene; }
    public static bool PresentQuoteOnly(ReviewWhiteboardController board, ReviewSlideDefinition slide) {
        if (slide == null || slide.id != "hostboss-quote-only" || slide.replayMoments == null || slide.replayMoments.Length == 0) return false;
        Init();
        ReviewReplayMoment quote = slide.replayMoments[0];
        Call(board, "StopReplaySequence", -1);
        if (IsHost()) board.Networkm_replayBeat = board.Networkm_replayBeat + 1;
        Call(board, "ApplyReplayPresentation", board.Networkm_replayBeat, quote.playerName, quote.quote, "", -1d);
        if (IsHost()) Panel.StartCoroutine(Panel.BroadcastQuoteOnly(board, slide, board.Networkm_replayBeat));
        return true;
    }
    public static bool BuildDeck(ReviewWhiteboardController board, int day) {
        Init();
        if (!ManualBossActive()) return false;
        Panel.PrepareReview(board, day, false);
        return true;
    }
    public static bool AllowEarnings(PlayerName player) {
        return !ManualBossActive() || player == null || !Panel.IsFired(player.netId);
    }
    public static IEnumerator ManualReview(ScammerGameManager game, int day) {
        while (ManualBossActive() && game != null && game.ScammerGameStarted && game.IsReviewPhase && game.CurrentDay == day) {
            game.NetworkreviewTimeRemaining = 3600f;
            yield return null;
        }
    }
}

public sealed class BossPanel : MonoBehaviour {
    public ReviewWhiteboardController Board;
    public bool ManualBossEnabled = true;
    public string Status = "F6 opens the boss panel. Speak to your team using voice chat.";
    readonly Dictionary<uint, Vector3> fired = new Dictionary<uint, Vector3>();
    readonly List<PlayerInput> pausedInputs = new List<PlayerInput>();
    readonly List<ReviewReplayMoment> quotes = new List<ReviewReplayMoment>();
    readonly List<int> quoteSlides = new List<int>();
    CancellationTokenSource preparation;
    Coroutine prepareRoutine;
    bool preparing;
    int preparedDay = -1;
    ReviewDayOverview preparedOverview;
    ReviewSlideDeckDefinition nativeDeck;
    bool presentationStarted;
    string defaultDeckTitle;
    string[] defaultSlideTitles;
    bool editTitles = true;
    int quoteIndex;
    int quoteDay = -1;
    int quoteReturnSlide;
    bool confirmFireAll;
    bool fireAllQueued;
    public bool FiringCutscene;
    bool fireAllCompleted;
    bool reputationDestroyed;
    bool chaosActive;
    bool previousFireLights;
    int chaosDay;
    float chaosUntil;
    bool open;
    int shift = -1;
    int customQuota = -1;
    float nextHold;
    uint selected;
    string quotaText = "";
    Vector2 scroll;
    CursorLockMode previousLock;
    bool previousVisible;
    Rect window = new Rect(24, 70, 580, 630);
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    public bool IsFired(uint id) { return fired.ContainsKey(id); }
    public string HostName() {
        foreach (PlayerName p in PlayerName.Instances) if (p != null && p.isLocalPlayer) return p.playerName;
        return "Host";
    }
    void Toggle(bool value) {
        if (value == open) return;
        open = value;
        if (open) { previousLock = Cursor.lockState; previousVisible = Cursor.lockState != CursorLockMode.Locked; }
        else { Cursor.lockState = previousLock; Cursor.visible = previousVisible; confirmFireAll = false; selected = 0; }
        BlockInput(open);
    }
    void BlockInput(bool block) {
        if (!block) {
            foreach (PlayerInput input in pausedInputs) if (input != null) input.enabled = true;
            pausedInputs.Clear();
        }
        // Use the game's shared input blocker so menus never move the host's character.
        foreach (PlayerName p in PlayerName.Instances) if (p != null && p.isLocalPlayer) {
            Metater.MetaPlayerController controller = p.GetComponent<Metater.MetaPlayerController>();
            if (controller == null) return;
            if (block) foreach (PlayerInput input in p.GetComponentsInChildren<PlayerInput>(true)) {
                if (input.enabled) { pausedInputs.Add(input); input.enabled = false; }
            }
            Metater.MetaPlayerBlocker b = controller.blocker;
            foreach (HashSet<MonoBehaviour> set in new [] { b.InputBlockers, b.MoveBlockers, b.PivotBlockers, b.JumpBlockers, b.SprintBlockers, b.StrafeBlockers, b.StrafeCameraBlockers }) {
                if (block) set.Add(this); else set.Remove(this);
            }
        }
    }
    void Update() {
        if (!Hooks.IsHost()) { if (open) Toggle(false); CancelPreparation(); fired.Clear(); quotes.Clear(); quoteSlides.Clear(); preparedOverview = null; nativeDeck = null; preparedDay = -1; customQuota = -1; fireAllQueued = false; FiringCutscene = false; fireAllCompleted = false; reputationDestroyed = false; chaosActive = false; quoteDay = -1; shift = -1; return; }
        Keyboard k = Keyboard.current;
        if (k != null && k.f6Key.wasPressedThisFrame) Toggle(!open);
        ScammerGameManager game = ScammerGameManager.Instance;
        if (game == null) return;
        if (Board == null) Board = UnityEngine.Object.FindObjectOfType<ReviewWhiteboardController>();
        if (open && k != null && k.escapeKey.wasPressedThisFrame) Toggle(false);
        if (!ManualBossEnabled) return;
        if (chaosActive && (Time.unscaledTime >= chaosUntil || game.CurrentDay != chaosDay || !game.IsReviewPhase)) StopChaos(game);
        if (customQuota > 0 && game.Quota != customQuota) ApplyQuota(game);
        if (game.CurrentDay != shift || !game.ScammerGameStarted) { fired.Clear(); shift = game.CurrentDay; selected = 0; confirmFireAll = false; fireAllCompleted = false; reputationDestroyed = false; }
        if (game.CurrentDay != quoteDay && !game.IsReviewPhase) { CancelPreparation(); quotes.Clear(); quoteSlides.Clear(); preparedOverview = null; nativeDeck = null; preparedDay = -1; quoteDay = game.CurrentDay; quoteIndex = 0; }
        if (open && k != null && k.escapeKey.wasPressedThisFrame) Toggle(false);
        if (Board == null) Board = UnityEngine.Object.FindObjectOfType<ReviewWhiteboardController>();
        if (fireAllQueued && presentationStarted && !FiringCutscene && game.IsReviewPhase && !preparing && preparedDay == game.CurrentDay && nativeDeck != null) {
            fireAllQueued = false; FiringCutscene = true; StartCoroutine(FireAllWithCutscene());
        }
        if (Time.unscaledTime < nextHold) return;
        nextHold = Time.unscaledTime + 0.5f;
        foreach (PlayerName p in PlayerName.ServerInstances) {
            Vector3 position;
            if (p != null && fired.TryGetValue(p.netId, out position)) {
                PlayerTeleporter tp = p.GetComponent<PlayerTeleporter>();
                if (tp != null && Vector3.Distance(p.transform.position, position) > 2f)
                    tp.ServerTeleportToPosition(position, "Fired for this shift", "HostBoss");
            }
        }
    }
    void LateUpdate() { if (open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
    void ApplyQuota(ScammerGameManager game) {
        if (!Hooks.IsHost() || customQuota < 1) return;
        int previous = game.Quota;
        // Native ServerSetQuota forcibly clears quotas in sandbox and startup recalculates them.
        // Use the same SyncVar and notification hook, retaining the host's override across shifts.
        game.Networkquota = customQuota;
        if (previous != customQuota) Hooks.Call(game, "OnQuotaSyncChanged", previous, customQuota);
        if (game.MoneyPoolLong < customQuota) game.NetworkquotaReached = false;
        Hooks.Call(game, "ServerCheckQuotaReached");
    }
    void OnEnable() {
        ScammerGameManager.OnReviewPhaseEnded += HandlePresentationEnded;
        ScammerGameManager.OnGameEnded += HandleGameEnded;
    }
    void OnDisable() {
        ScammerGameManager.OnReviewPhaseEnded -= HandlePresentationEnded;
        ScammerGameManager.OnGameEnded -= HandleGameEnded;
        RestoreDefaultTitles();
        if (open) Toggle(false); CancelPreparation();
        if (chaosActive && Hooks.IsHost() && ScammerGameManager.Instance != null) StopChaos(ScammerGameManager.Instance);
    }
    public void ReleaseHostControl() {
        if (open) Toggle(false);
        CancelPreparation(); StopAllCoroutines();
        FiringCutscene = false; fireAllQueued = false;
        if (Board != null) Hooks.Call(Board, "StopFiredPresentation");
        if (Hooks.IsHost()) {
            if (reputationDestroyed || chaosActive) EndDestroyReputation();
            SetAiBoss(true);
        }
        ManualBossEnabled = false;
    }
    void CaptureDefaultTitles() {
        defaultDeckTitle = nativeDeck.title;
        defaultSlideTitles = new string[nativeDeck.slides.Length];
        for (int i = 0; i < nativeDeck.slides.Length; i++) defaultSlideTitles[i] = nativeDeck.slides[i].title;
        editTitles = true;
    }
    void RestoreDefaultTitles() {
        if (nativeDeck == null || defaultSlideTitles == null) return;
        nativeDeck.title = defaultDeckTitle;
        for (int i = 0; i < defaultSlideTitles.Length && i < nativeDeck.slides.Length; i++) nativeDeck.slides[i].title = defaultSlideTitles[i];
    }
    void HandlePresentationEnded(int day) {
        RestoreDefaultTitles();
        presentationStarted = false;
        preparedDay = -1;
        defaultSlideTitles = null;
        defaultDeckTitle = null;
    }
    void HandleGameEnded() { HandlePresentationEnded(-1); }
    string EditTitle(string title) {
        string edited = GUILayout.TextField(title ?? "");
        return edited.Length > 160 ? edited.Substring(0, 160) : edited;
    }
    void DrawTitleEditor() {
        if (presentationStarted || preparing || nativeDeck == null || defaultSlideTitles == null) return;
        editTitles = GUILayout.Toggle(editTitles, "Edit presentation titles (this review only)");
        if (!editTitles) return;
        GUILayout.Label("Presentation title");
        nativeDeck.title = EditTitle(nativeDeck.title);
        for (int i = 0; i < defaultSlideTitles.Length && i < nativeDeck.slides.Length; i++) {
            GUILayout.Label("Slide " + (i + 1));
            nativeDeck.slides[i].title = EditTitle(nativeDeck.slides[i].title);
        }
        if (GUILayout.Button("Reset titles to defaults")) RestoreDefaultTitles();
        GUILayout.Label("Titles are shared when you start. They reset when the presentation ends.");
        GUILayout.Space(8);
    }
    void MoveSlide(int direction) {
        if (!Hooks.IsHost() || Board == null || !ScammerGameManager.Instance.IsReviewPhase || Board.SlideCount == 0) return;
        Hooks.Call(Board, "ServerPublishSlideIndex", Mathf.Clamp(Board.CurrentSlideIndex + direction, 0, Board.SlideCount - 1), false);
        Status = "Slide " + (Board.CurrentSlideIndex + 1) + " / " + Board.SlideCount;
    }
    void Fire(PlayerName p) {
        if (!Hooks.IsHost() || p == null || p.isLocalPlayer || p.isHostPlayer) return;
        PlayerTeleporter tp = p.GetComponent<PlayerTeleporter>();
        if (tp == null) { Status = "This employee has no teleporter."; return; }
        ScammerGameManager game = ScammerGameManager.Instance;
        Transform lobby = NetworkManager.singleton == null ? null : NetworkManager.singleton.GetStartPosition();
        if (lobby == null) { Status = "No lobby spawn exists yet."; return; }
        fired[p.netId] = lobby.position;
        tp.ServerTeleportToPosition(lobby.position, "Fired by host", "HostBoss");
        selected = 0;
        Status = p.playerName + " fired for this shift. Rehire below, or wait for the next day.";
        Debug.Log("[HostBoss] Fired employee netId=" + p.netId);
    }
    void FireAll() {
        ScammerGameManager game = ScammerGameManager.Instance;
        if (!Hooks.IsHost() || game == null || !game.ScammerGameStarted || FiringCutscene || fireAllQueued) return;
        confirmFireAll = false;
        if (game.DayZeroTutorialActive) { Status = "Finish the tutorial before firing the team."; return; }
        fireAllQueued = true;
        fireAllCompleted = false;
        if (!game.IsReviewPhase) game.ServerEndCurrentDayEarly();
        Status = "Fire All queued. Start the presentation to play the built-in You're fired cutscene.";
    }
    IEnumerator FireAllWithCutscene() {
        IEnumerator work = PlayFiredCutscene();
        Exception failure = null;
        while (true) {
            bool next = false; object current = null;
            try { next = work.MoveNext(); if (next) current = work.Current; } catch (Exception e) { failure = e; }
            if (failure != null || !next) break;
            yield return current;
        }
        FiringCutscene = false;
        if (Board != null) Hooks.Set(Board, "m_playNarration", false);
        if (!Hooks.IsHost()) yield break;
        if (failure != null) { Debug.LogError("[HostBoss] Fire All cutscene failed: " + failure); Status = "Cutscene failed; employees were not fired. See Player.log."; yield break; }
        confirmFireAll = false;
        fireAllCompleted = true;
        Status = "You're fired cutscene finished. Nobody was fired. Destroy Reputation is available.";
    }
    Vector3[] CollectFirePositions(ScammerGameManager game) {
        object originalCenter = Hooks.Get(game, "reviewRoomPoint");
        object originalCount = Hooks.Get(game, "firedGroundVfxCount");
        object originalRadius = Hooks.Get(game, "firedGroundVfxRadius");
        List<Vector3> positions = new List<Vector3>();
        List<Transform> centers = new List<Transform>();
        foreach (string field in new [] { "reviewRoomPoint", "startDayPoint", "secondFloorStartDayPoint", "thirdFloorStartDayPoint" }) {
            Transform center = Hooks.Get(game, field) as Transform;
            if (center != null && !centers.Contains(center)) centers.Add(center);
        }
        if (centers.Count == 0) centers.Add(game.transform);
        try {
            Hooks.Set(game, "firedGroundVfxCount", 32);
            Hooks.Set(game, "firedGroundVfxRadius", 18f);
            foreach (Transform center in centers) {
                Hooks.Set(game, "reviewRoomPoint", center);
                Vector3[] native = (Vector3[])Hooks.Call(game, "GetGroundFirePositions");
                if (native != null) foreach (Vector3 position in native) {
                    if (positions.Count >= 128) break;
                    positions.Add(position);
                }
            }
        } finally {
            Hooks.Set(game, "reviewRoomPoint", originalCenter);
            Hooks.Set(game, "firedGroundVfxCount", originalCount);
            Hooks.Set(game, "firedGroundVfxRadius", originalRadius);
        }
        return positions.ToArray();
    }
    void DestroyReputation() {
        ScammerGameManager game = ScammerGameManager.Instance;
        if (!Hooks.IsHost() || game == null || !game.IsReviewPhase || !fireAllCompleted || FiringCutscene || reputationDestroyed) return;
        try {
            Vector3[] positions = CollectFirePositions(game);
            uint[] players = (uint[])Hooks.Call(game, "GetCurrentPlayerNetIds");
            previousFireLights = game.NetworkfireLightActive;
            chaosDay = game.CurrentDay; chaosUntil = Time.unscaledTime + 30f;
            chaosActive = true;
            Hooks.Call(game, "ServerSetFireLightActive", true);
            // This is the native continuation after the firing presentation: world flames,
            // screen fire, alarm, screams and fire audio on every connected client.
            Hooks.Call(game, "RpcPlayFiredEffects", players, positions, 30f);
            reputationDestroyed = true;
            Status = "Reputation destroyed! Fire everywhere for 30 seconds.";
            if (open) Toggle(false);
        } catch (Exception e) {
            if (chaosActive) StopChaos(game);
            Debug.LogError("[HostBoss] Reputation sequence failed: " + e);
            Status = "Could not start the fire continuation. See Player.log.";
        }
    }
    void StopChaos(ScammerGameManager game) {
        chaosActive = false;
        Hooks.Call(game, "ServerSetFireLightActive", game.CurrentDay == chaosDay && game.IsReviewPhase && previousFireLights);
    }
    void SetAiBoss(bool enableAi) {
        if (!Hooks.IsHost() || FiringCutscene) return;
        ScammerGameManager game = ScammerGameManager.Instance;
        try {
            CancelPreparation();
            RestoreDefaultTitles();
            if (chaosActive && game != null) StopChaos(game);
            fireAllQueued = false; fireAllCompleted = false; fired.Clear();
            ManualBossEnabled = !enableAi;
            presentationStarted = false; preparedDay = -1; nativeDeck = null; preparedOverview = null; quotes.Clear(); quoteSlides.Clear();
            if (Board != null) {
                Hooks.Call(Board, "StopReplaySequence", -1);
                Hooks.Call(Board, "StopAdvanceTimer");
                Hooks.Call(Board, "CancelReviewPreparation");
                Hooks.Set(Board, "m_playNarration", enableAi);
                Hooks.Set(Board, "m_useHostAiReview", enableAi);
                Hooks.Set(Board, "m_endReviewPhaseWhenDeckCompletes", enableAi);
                Hooks.Set(Board, "m_playOnEnable", true);
                if (game != null && game.IsReviewPhase) Hooks.Call(Board, "HandleDeveloperReviewRestartRequested", game.CurrentDay);
            }
            if (game != null && game.IsReviewPhase) {
                Hooks.Call(game, "StopServerReviewRoutine");
                game.NetworkreviewTimeRemaining = enableAi ? Mathf.Max(60f, game.ReviewPhaseSeconds) : 3600f;
                Coroutine routine = game.StartCoroutine((IEnumerator)Hooks.Call(game, "ServerReviewPhaseRoutine", game.CurrentDay));
                Hooks.Set(game, "serverReviewRoutine", routine);
            }
            if (enableAi && game != null && customQuota > 0) {
                customQuota = -1;
                game.ServerSetQuota((int)Hooks.Call(game, "CalculateQuotaForDay", game.CurrentDay, game.GetCurrentQuotaPlayerCount()));
            }
            Status = enableAi ? "AI Boss enabled. Presentation restarted from the beginning; host controls disabled." : "Host Boss enabled. Manual presentation restarted from the beginning.";
            Debug.Log("[HostBoss] Boss mode changed: AI=" + enableAi);
        } catch (Exception e) { Debug.LogError("[HostBoss] Boss mode switch failed: " + e); Status = "Could not switch boss mode. See Player.log."; }
    }
    void EndDestroyReputation() {
        if (!Hooks.ManualBossActive() || (!reputationDestroyed && !chaosActive)) return;
        ScammerGameManager game = ScammerGameManager.Instance;
        if (game == null) return;
        try {
            CancelPreparation(); fireAllQueued = false; FiringCutscene = false; fireAllCompleted = false;
            fired.Clear(); chaosActive = false; reputationDestroyed = false;
            Hooks.Call(game, "StopServerRoutines");
            Hooks.Set(game, "serverDayCompletionInProgress", false);
            Hooks.Set(game, "serverResetAfterReview", false);
            Hooks.Call(game, "ServerSetFireLightActive", false);
            if (Board != null) {
                Hooks.Call(Board, "StopFiredPresentation"); Hooks.Call(Board, "StopReplaySequence", -1);
                Hooks.Call(Board, "ClearSyncedReviewDeck");
            }
            game.NetworkisReviewPhase = false;
            game.NetworkscammerGameStarted = false;
            game.NetworkdayTimeRemaining = 0f; game.NetworkreviewTimeRemaining = 0f;
            // The game's end event destroys native screen/world fire and stops its audio
            // on every client. Preserve run saves instead of calling the reset-run method.
            Hooks.Call(game, "RpcScammerGameEnded");
            Action ended = (Action)typeof(ScammerGameManager).GetField("OnGameEnded", Flags).GetValue(null);
            if (ended != null) ended();
            Hooks.Call(game, "ServerTeleportAllPlayersToLobby");
            presentationStarted = false; preparedDay = -1; nativeDeck = null; preparedOverview = null; quotes.Clear(); quoteSlides.Clear();
            Status = "Reputation sequence ended. Flames stopped and everyone returned to the lobby.";
        } catch (Exception e) { Debug.LogError("[HostBoss] End reputation failed: " + e); Status = "Could not end reputation sequence. See Player.log."; }
    }
    IEnumerator PlayFiredCutscene() {
        if (open) Toggle(false);
        ReviewDaySummaryTracker tracker = ReviewDaySummaryTracker.GetOrCreate();
        ReviewSlideDefinition slide = (ReviewSlideDefinition)typeof(ReviewDaySummaryTracker).GetMethod("BuildFiredSlide", Flags).Invoke(null, new object[] { tracker.BuildDaySummary(), new ReviewBossReview() });
        slide.kicker = "HOST DECISION";
        List<ReviewSlideDefinition> slides = new List<ReviewSlideDefinition>();
        foreach (ReviewSlideDefinition existing in nativeDeck.slides) if (existing.id != slide.id) slides.Add(existing);
        slides.Add(slide); nativeDeck.slides = slides.ToArray();
        Hooks.Set(Board, "m_playNarration", true);
        PublishDeck(Board, ScammerGameManager.Instance.CurrentDay, nativeDeck, slides.Count - 1);
        float deadline = Time.unscaledTime + 5f;
        while (Hooks.IsHost() && Board != null && !(bool)Hooks.Get(Board, "m_firedPresentationActive") && Time.unscaledTime < deadline) yield return null;
        if (Board == null || !Hooks.IsHost() || !(bool)Hooks.Get(Board, "m_firedPresentationActive")) throw new InvalidOperationException("Native firing presentation did not start.");
        deadline = Time.unscaledTime + 45f;
        while (Hooks.IsHost() && Board != null && (bool)Hooks.Get(Board, "m_firedPresentationActive") && Time.unscaledTime < deadline) yield return null;
        if (Board != null && (bool)Hooks.Get(Board, "m_firedPresentationActive")) throw new InvalidOperationException("Native firing presentation timed out.");
    }
    void PublishDeck(ReviewWhiteboardController board, int day, ReviewSlideDeckDefinition deck, int slideIndex) {
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(deck);
        board.Networkm_syncedDeckDay = day;
        board.Networkm_syncedReviewComplete = false;
        board.Networkm_syncedSlideIndex = slideIndex;
        board.Networkm_syncedDeckJson = json;
        Hooks.Call(board, "ApplySyncedDeckJson", json, day);
        Hooks.Call(board, "RpcApplySyncedReviewDeck", json, day);
        Hooks.Call(board, "ServerPublishSlideIndex", slideIndex, false);
    }
    void CancelPreparation() {
        if (preparation != null) { preparation.Cancel(); preparation.Dispose(); preparation = null; }
        if (prepareRoutine != null) { StopCoroutine(prepareRoutine); prepareRoutine = null; }
        preparing = false;
    }
    public void PrepareReview(ReviewWhiteboardController board, int day, bool refresh) {
        if (!Hooks.IsHost() || board == null) return;
        if (!refresh && Board == board && preparedDay == day && nativeDeck != null) {
            ScammerGameManager.Instance.ServerNotifyReviewDeckReady(day);
            return;
        }
        if (preparing && Board == board && quoteDay == day) return;
        RestoreDefaultTitles();
        CancelPreparation();
        if (refresh && quotes.Count == 0) preparedOverview = null;
        presentationStarted = false;
        Board = board; quoteDay = day; preparing = true;
        preparation = new CancellationTokenSource();
        prepareRoutine = StartCoroutine(PrepareSafely(board, day, preparation.Token));
    }
    IEnumerator PrepareSafely(ReviewWhiteboardController board, int day, CancellationToken token) {
        IEnumerator work = PrepareNativeReview(board, day, token);
        Exception failure = null;
        while (true) {
            bool next = false; object current = null;
            try { next = work.MoveNext(); if (next) current = work.Current; }
            catch (Exception e) { failure = e; }
            if (failure != null || !next) break;
            yield return current;
        }
        preparing = false; prepareRoutine = null;
        if (board != null) board.Networkm_syncedDeckIsGenerating = failure == null && !presentationStarted && ManualBossEnabled;
        if (failure != null && !(failure is OperationCanceledException)) {
            fireAllQueued = false;
            Debug.LogError("[HostBoss] Native replay preparation failed: " + failure);
            Status = "Recording preparation failed. Retry with Refresh recordings. See Player.log.";
            if (Hooks.IsHost() && ScammerGameManager.Instance != null) ScammerGameManager.Instance.ServerNotifyReviewDeckReady(day);
        }
    }
    IEnumerator PrepareNativeReview(ReviewWhiteboardController board, int day, CancellationToken token) {
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
        if (ScammerGameManager.Instance.IsReviewPhase) Hooks.Call(ScammerGameManager.Instance, "RpcDeveloperReviewRestartRequested", day);
        board.Networkm_reviewGeneration = board.Networkm_reviewGeneration + 1;
        Hooks.Call(board, "EnsureReplayPreparationGeneration", board.Networkm_reviewGeneration);
        board.Networkm_syncedDeckIsGenerating = true;
        quotes.Clear(); quoteSlides.Clear(); quoteIndex = 0;
        Status = "Preparing the game's quote recordings...";
        // Let day-end recording notices finish before selecting the native highlights.
        yield return null;
        token.ThrowIfCancellationRequested();
        ReviewDaySummaryTracker tracker = ReviewDaySummaryTracker.GetOrCreate();
        if (preparedOverview == null || preparedOverview.currentDay != day) preparedOverview = tracker.BuildDayOverview();
        ReviewDayOverview overview = preparedOverview;
        ReviewReplayService service = ReviewReplayService.Ensure();
        Task metadata = service.WaitForMetadata(overview, token);
        while (!metadata.IsCompleted) { token.ThrowIfCancellationRequested(); yield return null; }
        metadata.GetAwaiter().GetResult();
        token.ThrowIfCancellationRequested();
        Type evidenceType = typeof(ReviewDayOverview).Assembly.GetType("Kolkata.UI.ReviewEvidencePacket");
        evidenceType.GetMethod("Freeze", Flags).Invoke(null, new object[] { overview });
        Task highlights = service.PrepareHighlights(overview, token);
        while (!highlights.IsCompleted) { token.ThrowIfCancellationRequested(); yield return null; }
        highlights.GetAwaiter().GetResult();
        token.ThrowIfCancellationRequested();
        ReviewSlideDeckDefinition deck = tracker.BuildReviewDeck(null, overview);
        List<ReviewSlideDefinition> slides = new List<ReviewSlideDefinition>();
        foreach (ReviewSlideDefinition slide in deck.slides) {
            if ((bool)Hooks.Call(board, "IsFiredSlide", slide)) continue;
            slide.narration = "";
            // Preserve the native replay IDs and moment slides. Recording audio is separate
            // from the disabled boss narration, and still plays through the native stream.
            slides.Add(slide);
        }
        slides.Add(new ReviewSlideDefinition { id="host-verdict", layout="quote", theme="blue", kicker="HOST BOSS", title="The floor is yours", quote="Your host decides who stays on the team.", subtitle="Waiting for the host to finish the review.", narration="", footer="Host Boss | Manual review" });
        deck.slides = slides.ToArray(); deck.presenterName = HostName(); deck.presenterTitle = "Host / Boss";
        deck.title = "Day " + day + " | Host review";
        nativeDeck = deck; preparedDay = day;
        CaptureDefaultTitles();
        for (int i = 0; i < deck.slides.Length; i++) {
            ReviewReplayMoment[] moments = deck.slides[i].replayMoments;
            if (moments == null) continue;
            foreach (ReviewReplayMoment moment in moments) {
                if (moment == null || string.IsNullOrWhiteSpace(moment.quote)) continue;
                AddRecordingQuote(moment, i);
            }
        }
        if (overview.featuredQuotes != null) foreach (ReviewFeaturedQuote quote in overview.featuredQuotes) {
            if (quote == null || string.IsNullOrWhiteSpace(quote.text)) continue;
            AddRecordingQuote(new ReviewReplayMoment { playerName = quote.playerName, quote = quote.text, replayId = quote.replayId }, -1);
        }
        ScammerGameManager.Instance.ServerNotifyReviewDeckReady(day);
        Status = "Presentation ready. Click Start Presentation; the screen stays connecting until then. " + quotes.Count + " quotes available.";
        Debug.Log("[HostBoss] Native replay deck ready: day=" + day + " slides=" + slides.Count + " recordings=" + quotes.Count);
    }
    void AddRecordingQuote(ReviewReplayMoment moment, int slide) {
        for (int i = 0; i < quotes.Count; i++) {
            if ((quotes[i].playerName ?? "") != (moment.playerName ?? "") || quotes[i].quote != moment.quote) continue;
            // A text-only occurrence must not hide the same quote's playable highlight.
            if (string.IsNullOrWhiteSpace(quotes[i].replayId) && !string.IsNullOrWhiteSpace(moment.replayId)) {
                quotes[i] = moment; quoteSlides[i] = slide;
            }
            return;
        }
        quotes.Add(moment); quoteSlides.Add(slide);
    }
    void StartPresentation() {
        ScammerGameManager game = ScammerGameManager.Instance;
        if (!Hooks.ManualBossActive() || game == null || !game.IsReviewPhase || preparing || presentationStarted || Board == null || nativeDeck == null || preparedDay != game.CurrentDay) return;
        try {
            Hooks.Set(Board, "m_playOnEnable", true);
            presentationStarted = true;
            Board.Networkm_syncedDeckIsGenerating = false;
            PublishDeck(Board, game.CurrentDay, nativeDeck, 0);
            Status = "Presentation started at slide 1.";
            Debug.Log("[HostBoss] Host started presentation: day=" + game.CurrentDay);
        } catch (Exception e) { presentationStarted = false; Debug.LogError("[HostBoss] Start presentation failed: " + e); Status = "Could not start presentation. See Player.log."; }
    }
    void ShowQuote() {
        ScammerGameManager game = ScammerGameManager.Instance;
        if (!Hooks.IsHost() || game == null || !game.IsReviewPhase || Board == null || quotes.Count == 0) return;
        try {
            if (!quoteSlides.Contains(Board.CurrentSlideIndex)) quoteReturnSlide = Board.CurrentSlideIndex;
            ReviewReplayMoment quote = quotes[quoteIndex];
            int source = quoteSlides[quoteIndex];
            if (source < 0) source = 0;
            ReviewSlideDefinition display = Newtonsoft.Json.JsonConvert.DeserializeObject<ReviewSlideDefinition>(Newtonsoft.Json.JsonConvert.SerializeObject(nativeDeck.slides[source]));
            display.id = "hostboss-quote-only";
            display.narration = "";
            // Keep a native replay-shaped slide so the game's quote RPC accepts it.
            // An empty recording ID plus PresentQuoteOnly prevents audio/video playback.
            display.replayMoments = new [] { new ReviewReplayMoment { playerName = quote.playerName, quote = quote.quote, replayId = "" } };
            List<ReviewSlideDefinition> slides = new List<ReviewSlideDefinition>();
            foreach (ReviewSlideDefinition existing in nativeDeck.slides) if (existing.id != "hostboss-quote-only") slides.Add(existing);
            slides.Add(display); nativeDeck.slides = slides.ToArray();
            PublishDeck(Board, game.CurrentDay, nativeDeck, slides.Count - 1);
            // Republishing the same quote may not redisplay a slide with an unchanged index.
            // Wait for the native slide transition before applying/rebroadcasting its text.
            StartCoroutine(EnsureQuoteOnlyVisible(Board, display, slides.Count - 1));
            Status = "Showing built-in quote from " + quote.playerName + ".";
            Debug.Log("[HostBoss] Quote-only requested: day=" + game.CurrentDay + " index=" + (slides.Count - 1));
            if (open) Toggle(false);
        } catch (Exception e) { Debug.LogError("[HostBoss] Show quote failed: " + e); Status = "Could not show quote. See Player.log."; }
    }
    IEnumerator EnsureQuoteOnlyVisible(ReviewWhiteboardController board, ReviewSlideDefinition slide, int index) {
        yield return new WaitForSeconds(0.65f);
        if (!Hooks.IsHost() || board == null || board.CurrentSlideIndex != index || ScammerGameManager.Instance == null || !ScammerGameManager.Instance.IsReviewPhase) yield break;
        ReviewSlideDeckDefinition deck = (ReviewSlideDeckDefinition)Hooks.Get(board, "m_deck");
        if (deck == null || index >= deck.slides.Length || deck.slides[index].id != "hostboss-quote-only") yield break;
        Hooks.PresentQuoteOnly(board, slide);
        Debug.Log("[HostBoss] Quote-only applied on native slide " + index);
    }
    public IEnumerator BroadcastQuoteOnly(ReviewWhiteboardController board, ReviewSlideDefinition slide, uint beat) {
        yield return new WaitForSeconds(0.4f);
        if (!Hooks.IsHost() || board == null || board.Networkm_replayBeat != beat) yield break;
        ReviewSlideDeckDefinition deck = (ReviewSlideDeckDefinition)Hooks.Get(board, "m_deck");
        int index = board.CurrentSlideIndex;
        if (deck == null || index < 0 || index >= deck.slides.Length || deck.slides[index].id != "hostboss-quote-only") yield break;
        ReviewReplayMoment quote = slide.replayMoments[0];
        Hooks.Call(board, "RpcReplayPresentation", board.Networkm_reviewGeneration, beat, quote.playerName, quote.quote, "", -1d);
    }
    void PlayRecording() {
        ScammerGameManager game = ScammerGameManager.Instance;
        if (!Hooks.ManualBossActive() || game == null || !game.IsReviewPhase || !presentationStarted || preparing || FiringCutscene || Board == null || nativeDeck == null || quotes.Count == 0) return;
        try {
            ReviewReplayMoment moment = quotes[quoteIndex];
            object[] selection = new object[] { moment.replayId, null };
            if (string.IsNullOrWhiteSpace(moment.replayId) || !(bool)Hooks.Call(ReviewReplayService.Ensure(), "TryGetSelection", selection)) {
                Status = "No playable clip for this quote. Try another quote or Refresh recordings after recording uploads finish.";
                return;
            }
            if (!quoteSlides.Contains(Board.CurrentSlideIndex)) quoteReturnSlide = Board.CurrentSlideIndex;
            // Highlights can exist without an authored replay slide. Publish one selected
            // native moment so playback never depends on the old slide-index mapping.
            ReviewSlideDefinition display = Newtonsoft.Json.JsonConvert.DeserializeObject<ReviewSlideDefinition>(Newtonsoft.Json.JsonConvert.SerializeObject(nativeDeck.slides[0]));
            display.id = "hostboss-recording"; display.narration = "";
            display.replayMoments = new [] { moment };
            List<ReviewSlideDefinition> slides = new List<ReviewSlideDefinition>();
            foreach (ReviewSlideDefinition existing in nativeDeck.slides) if (existing.id != "hostboss-recording") slides.Add(existing);
            slides.Add(display); nativeDeck.slides = slides.ToArray();
            Hooks.Call(Board, "StopReplaySequence", -1);
            PublishDeck(Board, game.CurrentDay, nativeDeck, slides.Count - 1);
            StartCoroutine(EnsureRecordingPlaying(Board, display, slides.Count - 1));
            Status = "Playing built-in recording from " + moment.playerName + ".";
            if (open) Toggle(false);
        } catch (Exception e) { Debug.LogError("[HostBoss] Play recording failed: " + e); Status = "Could not play recording. See Player.log."; }
    }
    IEnumerator EnsureRecordingPlaying(ReviewWhiteboardController board, ReviewSlideDefinition slide, int index) {
        yield return new WaitForSeconds(0.65f);
        if (!Hooks.ManualBossActive() || board == null || Board != board || !presentationStarted || ScammerGameManager.Instance == null || !ScammerGameManager.Instance.IsReviewPhase || board.CurrentSlideIndex != index) yield break;
        ReviewSlideDeckDefinition deck = (ReviewSlideDeckDefinition)Hooks.Get(board, "m_deck");
        if (deck == null || index >= deck.slides.Length || deck.slides[index].id != "hostboss-recording" || deck.slides[index].replayMoments == null || deck.slides[index].replayMoments[0].replayId != slide.replayMoments[0].replayId) yield break;
        // Republish of an unchanged slide may skip the normal native start callback.
        if (!(bool)Hooks.Get(board, "m_replaySequenceActive")) Hooks.Call(board, "StartReplaySequence", slide);
    }
    void ReturnToPresentation() {
        if (!Hooks.IsHost() || Board == null) return;
        Hooks.Call(Board, "StopReplaySequence", -1);
        Hooks.Call(Board, "RpcHideReplayPresentation", Board.Networkm_reviewGeneration, Board.Networkm_replayBeat);
        Hooks.Call(Board, "ServerPublishSlideIndex", Mathf.Clamp(quoteReturnSlide, 0, Board.SlideCount - 1), false);
    }
    void FinishReview() {
        if (!Hooks.IsHost()) return;
        ScammerGameManager game = ScammerGameManager.Instance;
        if (game == null || !game.IsReviewPhase) return;
        Hooks.Set(game, "serverResetAfterReview", false);
        game.RequestEndReviewPhase();
        Status = "Review finished. Team approved to continue.";
    }
    void OnGUI() {
        if (!Hooks.IsHost()) return;
        GUI.depth = -10000;
        GUI.color = Color.white;
        if (!open) { GUI.Box(new Rect(16, 16, 220, 30), ManualBossEnabled ? "HOST BOSS  |  F6 controls" : "AI BOSS  |  F6 toggle"); return; }
        window.width = Mathf.Min(580, Screen.width - 24);
        window.height = Mathf.Min(650, Screen.height - 50);
        window.x = Mathf.Clamp(window.x, 0, Screen.width - window.width);
        window.y = Mathf.Clamp(window.y, 0, Screen.height - window.height);
        window = GUILayout.Window(194061, window, Draw, "HOST BOSS  /  F6 to close");
    }
    void Draw(int id) {
        ScammerGameManager game = ScammerGameManager.Instance;
        scroll = GUILayout.BeginScrollView(scroll);
        if (game == null) { GUILayout.Label("Waiting for the game manager..."); GUILayout.EndScrollView(); GUI.DragWindow(); return; }
        GUI.enabled = !FiringCutscene;
        bool aiBoss = GUILayout.Toggle(!ManualBossEnabled, "AI Boss — enable AI / disable Host Boss");
        if (aiBoss == ManualBossEnabled) SetAiBoss(aiBoss);
        GUI.enabled = true;
        if (!ManualBossEnabled) {
            GUILayout.Label("AI Boss is in control. Turn the toggle off to restore host controls.");
            GUILayout.Label(Status);
            GUILayout.EndScrollView(); GUI.DragWindow(new Rect(0, 0, 10000, 22)); return;
        }
        if (game.IsReviewPhase) DrawTitleEditor();
        GUI.enabled = game.IsReviewPhase && !preparing && !presentationStarted && nativeDeck != null && preparedDay == game.CurrentDay;
        if (GUILayout.Button(preparing ? "Preparing presentation..." : "Start Presentation", GUILayout.Height(36))) StartPresentation();
        GUI.enabled = true;
        if (!presentationStarted) GUILayout.Label("Presentation is connecting. Click Start Presentation when ready.");
        GUILayout.Label("Day " + game.CurrentDay + "  |  " + (game.IsReviewPhase ? "MANUAL REVIEW" : "SHIFT IN PROGRESS"));
        GUILayout.Label("Quota: $" + game.MoneyPool + " / $" + game.Quota + "    Remaining: $" + Mathf.Max(0, game.Quota - game.MoneyPool));
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        quotaText = GUILayout.TextField(quotaText, GUILayout.Width(130));
        if (GUILayout.Button("Set quota")) {
            int amount;
            if (int.TryParse(quotaText, out amount) && amount >= 1 && amount <= 100000000) { customQuota = amount; ApplyQuota(game); Status = "Quota set to $" + amount + ". This override stays active until reset."; }
            else Status = "Enter a quota from 1 to 100,000,000.";
        }
        if (GUILayout.Button("Auto", GUILayout.Width(60))) { customQuota = -1; Status = "Automatic quotas resume at the next shift."; }
        GUILayout.EndHorizontal();
        GUILayout.Space(12);
        GUI.enabled = presentationStarted && game.IsReviewPhase && !preparing && !FiringCutscene && Board != null && Board.SlideCount > 0;
        GUILayout.Label(Board == null ? "Slides loading..." : "Presentation: " + (Board.CurrentSlideIndex + 1) + " / " + Board.SlideCount);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("< Previous", GUILayout.Height(34))) MoveSlide(-1);
        if (GUILayout.Button("Next >", GUILayout.Height(34))) MoveSlide(1);
        GUILayout.EndHorizontal();
        GUI.enabled = game.ScammerGameStarted && !game.IsReviewPhase && !game.DayZeroTutorialActive;
        if (GUILayout.Button("End shift / summon review")) { game.ServerEndCurrentDayEarly(); Status = "Ending shift..."; }
        GUI.enabled = presentationStarted && game.IsReviewPhase && !FiringCutscene && !fireAllQueued;
        if (GUILayout.Button("Finish review / approve next day")) FinishReview();
        GUI.enabled = true;
        GUILayout.Space(12);
        GUILayout.Label("CALL QUOTES  |  built-in video and audio recordings");
        GUI.enabled = game.IsReviewPhase && !preparing && !FiringCutscene && Board != null;
        if (GUILayout.Button(preparing ? "Preparing recordings..." : "Refresh recordings")) {
            PrepareReview(Board, game.CurrentDay, true);
        }
        GUI.enabled = presentationStarted && game.IsReviewPhase && !preparing && !FiringCutscene && quotes.Count > 0;
        if (quotes.Count > 0) {
            GUILayout.Label((quoteIndex + 1) + " / " + quotes.Count + "  |  " + quotes[quoteIndex].playerName);
            GUILayout.Label(quotes[quoteIndex].quote, new GUIStyle(GUI.skin.label) { wordWrap = true });
        } else GUILayout.Label(preparing ? "Waiting for built-in recording metadata..." : "No built-in recordings ready. Finish calls, then refresh during review.");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("< Quote")) quoteIndex = (quoteIndex + quotes.Count - 1) % quotes.Count;
        if (GUILayout.Button("Show Quote")) ShowQuote();
        bool canSelectQuote = GUI.enabled;
        GUI.enabled = canSelectQuote;
        if (GUILayout.Button("Play Recording")) PlayRecording();
        GUI.enabled = canSelectQuote;
        if (GUILayout.Button("Quote >")) quoteIndex = (quoteIndex + 1) % quotes.Count;
        GUILayout.EndHorizontal();
        GUI.enabled = presentationStarted && game.IsReviewPhase && !preparing && !FiringCutscene && Board != null && Board.SlideCount > 0;
        if (GUILayout.Button("Return to presentation")) ReturnToPresentation();
        GUI.enabled = true;
        GUILayout.Space(12);
        GUILayout.Label("EMPLOYEES  |  earned this shift");
        GUI.enabled = game.ScammerGameStarted && !FiringCutscene && !fireAllQueued;
        if (GUILayout.Button(confirmFireAll ? "Confirm Fire All cutscene only" : "Fire All")) {
            if (confirmFireAll) FireAll();
            else { confirmFireAll = true; selected = 0; Status = "Confirm to play the You're fired cutscene. Nobody will be fired."; }
        }
        if (confirmFireAll && GUILayout.Button("Cancel Fire All")) confirmFireAll = false;
        GUI.enabled = game.IsReviewPhase && fireAllCompleted && !FiringCutscene && !reputationDestroyed;
        if (GUILayout.Button("DESTROY REPUTATION", GUILayout.Height(34))) DestroyReputation();
        GUI.enabled = reputationDestroyed || chaosActive;
        if (GUILayout.Button("END DESTROY REPUTATION / return everyone to lobby", GUILayout.Height(34))) EndDestroyReputation();
        GUI.enabled = true;
        GUILayout.Label(chaosActive ? "Fire continuation active (30 seconds)." : "Destroy Reputation unlocks after the Fire All cutscene.");
        foreach (PlayerName p in PlayerName.ServerInstances) {
            if (p == null) continue;
            GUILayout.BeginHorizontal();
            bool isFired = IsFired(p.netId);
            GUILayout.Label(p.playerName + "  |  $" + game.GetEarnedMoney(p) + (isFired ? "  [FIRED]" : ""), GUILayout.Width(340));
            GUI.enabled = game.ScammerGameStarted && !p.isLocalPlayer && !p.isHostPlayer;
            if (isFired) {
                if (GUILayout.Button("Rehire", GUILayout.Width(100))) {
                    fired.Remove(p.netId);
                    PlayerTeleporter tp = p.GetComponent<PlayerTeleporter>();
                    if (tp != null) tp.ServerTeleportToPosition(PlayerModeManager.LocalPlayerInstance.transform.position, "Rehired by host", "HostBoss");
                    Status = p.playerName + " rehired.";
                }
            } else if (GUILayout.Button(selected == p.netId ? "Confirm fire" : "Fire", GUILayout.Width(100))) {
                if (selected == p.netId) Fire(p); else { selected = p.netId; Status = "Click Confirm fire to remove " + p.playerName + " from this shift."; }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(14);
        GUILayout.Label(Status);
        GUILayout.Label("Fired employees stay connected. Their earnings do not count until rehired.\nFiring expires at the next day. Reviews wait for your decision.");
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, 10000, 22));
    }
}
}
