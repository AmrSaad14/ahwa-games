// Unity Ads (Plugins/Android/BBAds.java): an interstitial after a game when you play again, and a
// rewarded video for coins. The game id and ad units come from Resources/AdsConfig.json (written at build time
// from the UNITY_ADS_* settings; test ads until UNITY_ADS_TEST is 0). No game id: no ads. Off the phone there are no real ads: the game's own
// short spot stands in for the rewarded video, and the interstitial is skipped.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class Ads
{
    static bool started;
    static Action interDone;
    static Action<bool> rewardDone;
    static bool earned;

    public static bool Available
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return started;
#else
            return false;
#endif
        }
    }

    /// Ads are offered to this player at all: not to children (Age.cs), not in the browser.
    public static bool Offered
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            return !Age.Child;
#endif
        }
    }

    public static void Begin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (started) return;
        if (!Offered) { Debug.Log("[ads] a child: no ads, the ads SDK isn't started"); return; }
        try
        {
            var cfg = Resources.Load<TextAsset>("AdsConfig");
            var d = cfg != null ? Json.Parse(cfg.text) as Dictionary<string, object> : null;
            string game = d?.Str("game") ?? "", inter = d?.Str("interstitial") ?? "", reward = d?.Str("rewarded") ?? "";
            bool test = d == null || d.Bool("test");
            if (string.IsNullOrEmpty(game)) { Debug.Log("[ads] no Unity Ads game id in this build: ads are off"); return; }
            using (var c = new AndroidJavaClass("com.ahwa.ads.BBAds")) c.CallStatic("init", "Board Battles", game, inter, reward, test, Age.Minor);
            started = true;
            Debug.Log("[ads] started");
        }
        catch (Exception e) { Debug.Log("[ads] not available: " + e.Message); }
#endif
    }

    /// Players where the law asks for it (EU, UK) can change their ad privacy choice from Settings.
    public static bool PrivacyOptionsRequired
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var c = new AndroidJavaClass("com.ahwa.ads.BBAds")) return c.CallStatic<bool>("privacyOptionsRequired"); }
            catch (Exception) { }
#endif
            return false;
        }
    }
    public static void ShowPrivacyOptions() => Call("showPrivacyOptions");

    static bool Call(string method, params object[] args)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { using (var c = new AndroidJavaClass("com.ahwa.ads.BBAds")) c.CallStatic(method, args); return true; }
        catch (Exception e) { Debug.Log("[ads] " + method + ": " + e.Message); }
#endif
        return false;
    }

    /// One full-screen ad in every `Every` games (the control room sets it; 0: none): the 1st game shows it, the
    /// 2nd doesn't, the 3rd does… A game finished or left counts. The count is kept across restarts.
    public static int Every = 2;
    public static void BetweenGames(Action done)
    {
        int n = PlayerPrefs.GetInt("ads.games", 0);
        PlayerPrefs.SetInt("ads.games", n + 1); PlayerPrefs.Save();
        bool show = Every > 0 && n % Every == 0;
        Debug.Log($"[ads] game {n + 1}: {(show ? "an ad" : "no ad")} (1 in {Every})");
        if (show) Interstitial(done); else done?.Invoke();
    }

    /// The between-games ad, then `done` (straight away when there is no ad to show).
    public static void Interstitial(Action done)
    {
        if (!Available || !Call("showInterstitial")) { done?.Invoke(); return; }
        interDone = done;
        Sfx.Pause(true);
    }

    /// The rewarded video; `done(true)` if it was watched to the end.
    public static void Rewarded(string userId, string ticket, Action<bool> done)
    {
        if (!Available || !Call("showRewarded", userId ?? "", ticket ?? "")) { done?.Invoke(false); return; }
        earned = false; rewardDone = done; Unavailable = false;
        Sfx.Pause(true);
    }

    /// The last ad couldn't be shown at all (not loaded, or the network said no), and why.
    public static bool Unavailable { get; private set; }
    public static string LastError
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var c = new AndroidJavaClass("com.ahwa.ads.BBAds")) return c.CallStatic<string>("lastError"); }
            catch (Exception) { }
#endif
            return "";
        }
    }

    /// From Java, through App.OnAdEvent.
    public static void OnEvent(string e)
    {
        Debug.Log("[ads] " + e);
        switch (e)
        {
            case "inter_closed":
                Sfx.Pause(false);
                var d = interDone; interDone = null; d?.Invoke();
                break;
            case "reward_earned": earned = true; break;
            case "reward_closed":
            case "reward_unavailable":
                if (e == "reward_unavailable") Unavailable = true;
                Sfx.Pause(false);
                var r = rewardDone; rewardDone = null; r?.Invoke(e == "reward_closed" && earned);
                break;
        }
    }
}
