using System.Collections;
using System.Reflection;
using Kolkata.UI;
using Mirror;
using UnityEngine;

namespace HostBoss;

public static class Hooks
{
	public static BossPanel Panel;

	private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	public static object Get(object o, string name)
	{
		return o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(o);
	}

	public static void Set(object o, string name, object value)
	{
		o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SetValue(o, value);
	}

	public static object Call(object o, string name, params object[] args)
	{
		return o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
	}

	public static void Init()
	{
		if (!(Panel != null))
		{
			GameObject gameObject = new GameObject("HostBoss");
			Object.DontDestroyOnLoad(gameObject);
			Panel = gameObject.AddComponent<BossPanel>();
			Debug.Log("[HostBoss] Loaded 1.60.6 BepInEx plugin. Host controls: F6.");
		}
	}

	public static bool IsHost()
	{
		if (NetworkServer.active)
		{
			return NetworkClient.active;
		}
		return false;
	}

	public static bool ManualBossActive()
	{
		if (IsHost() && Panel != null)
		{
			return Panel.ManualBossEnabled;
		}
		return false;
	}

	public static bool SuppressAutomaticBoss()
	{
		if (ManualBossActive())
		{
			return !Panel.FiringCutscene;
		}
		return false;
	}

	public static bool PresentQuoteOnly(ReviewWhiteboardController board, ReviewSlideDefinition slide)
	{
		if (slide == null || slide.id != "hostboss-quote-only" || slide.replayMoments == null || slide.replayMoments.Length == 0)
		{
			return false;
		}
		Init();
		ReviewReplayMoment reviewReplayMoment = slide.replayMoments[0];
		Call(board, "StopReplaySequence", -1);
		if (IsHost())
		{
			board.Networkm_replayBeat++;
		}
		Call(board, "ApplyReplayPresentation", board.Networkm_replayBeat, reviewReplayMoment.playerName, reviewReplayMoment.quote, "", -1.0);
		if (IsHost())
		{
			Panel.StartCoroutine(Panel.BroadcastQuoteOnly(board, slide, board.Networkm_replayBeat));
		}
		return true;
	}

	public static bool BuildDeck(ReviewWhiteboardController board, int day)
	{
		Init();
		if (!ManualBossActive())
		{
			return false;
		}
		Panel.PrepareReview(board, day, refresh: false);
		return true;
	}

	public static bool AllowEarnings(PlayerName player)
	{
		if (ManualBossActive() && !(player == null))
		{
			return !Panel.IsFired(player.netId);
		}
		return true;
	}

	public static IEnumerator ManualReview(ScammerGameManager game, int day)
	{
		while (ManualBossActive() && game != null && game.ScammerGameStarted && game.IsReviewPhase && game.CurrentDay == day)
		{
			game.NetworkreviewTimeRemaining = 3600f;
			yield return null;
		}
	}
}
