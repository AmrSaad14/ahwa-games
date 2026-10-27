// Google AdMob ads (Plugins/Android/BBAds.java): an interstitial after a game when you play again, and a
// rewarded video for coins. The ad units come from Resources/AdsConfig.json (written at build time;
// Google's test ads until the real ones are set). Off the phone there are no real ads: the game's own
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

    public static void Begin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var cfg = Resources.Load<TextAsset>("AdsConfig");
            var d = cfg != null ? Json.Parse(cfg.text) as Dictionary<string, object> : null;
            string inter = d?.Str("interstitial") ?? "", reward = d?.Str("rewarded") ?? "";
            using (var c = new AndroidJavaClass("com.ahwa.ads.BBAds")) c.CallStatic("init", "Board Battles", inter, reward);
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
        earned = false; rewardDone = done;
        Sfx.Pause(true);
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
                Sfx.Pause(false);
                var r = rewardDone; rewardDone = null; r?.Invoke(e == "reward_closed" && earned);
                break;
        }
    }
}
