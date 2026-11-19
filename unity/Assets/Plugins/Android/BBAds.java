// Unity Ads for Board Battles: an interstitial between games, and the same kind of ad for coins.
// Google Play's Families policy (the game is for children too, with no age question): every ad is for a child —
// not personalised (Unity "non-behavioral") and closable after 5 seconds. Unity's rewarded videos can't be closed,
// so the coins come from the skippable interstitial instead: watched to the end, the coins; closed early, none.
// The game calls these from C# (Scripts/Ads.cs); results come back as UnitySendMessage("Board Battles", "OnAdEvent", …):
//   inter_closed          the interstitial is gone (shown, or there was none to show)
//   reward_earned         the player watched the rewarded video to the end
//   reward_closed         the rewarded video is gone (earned or not)
//   reward_unavailable    no rewarded video could be shown
package com.ahwa.ads;

import android.app.Activity;

import com.unity3d.ads.IUnityAdsInitializationListener;
import com.unity3d.ads.IUnityAdsLoadListener;
import com.unity3d.ads.IUnityAdsShowListener;
import com.unity3d.ads.UnityAds;
import com.unity3d.ads.UnityAdsShowOptions;
import com.unity3d.ads.metadata.MetaData;
import com.unity3d.ads.metadata.PlayerMetaData;
import com.unity3d.player.UnityPlayer;

public class BBAds {
    static String go = "Board Battles", interId = "", rewardId = "", game = "", lastError = "";
    static boolean interReady, rewardReady, loadingInter, loadingReward, started, initializing, testMode;

    static void send(String what) { try { UnityPlayer.UnitySendMessage(go, "OnAdEvent", what); } catch (Throwable ignored) { } }
    static void fail(String why) { lastError = why; send("error:" + why); }

    /** Why the last ad couldn't load or start (shown to the player with "not ready", to find the cause). */
    public static String lastError() { return lastError; }

    public static void init(String gameObject, String gameId, String interstitialId, String rewardedId, boolean test) {
        // the coins come from the skippable ad too (see the top); the rewarded unit is only a fallback
        go = gameObject; interId = interstitialId; rewardId = interstitialId.isEmpty() ? rewardedId : interstitialId; game = gameId == null ? "" : gameId; testMode = test;
        start();
    }

    /** Google Play's Families policy: anyone may be a child, so nobody gets ads that use their behaviour (no
     *  interest-based ads, no remarketing). */
    static void privacy(android.content.Context c) {
        try {
            MetaData p = new MetaData(c); p.set("privacy.mode", "mixed"); p.commit();
            MetaData u = new MetaData(c); u.set("user.nonbehavioral", true); u.commit();
        } catch (Throwable t) { lastError = "privacy " + t; }
    }

    static void start() {
        if (started || initializing) return;
        if (game.isEmpty()) { fail("no game id"); return; }
        initializing = true;
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> {
            try {
                privacy(a.getApplicationContext());
                UnityAds.initialize(a.getApplicationContext(), game, testMode, new IUnityAdsInitializationListener() {
                    @Override public void onInitializationComplete() { started = true; initializing = false; lastError = ""; loadInter(); loadReward(); }
                    @Override public void onInitializationFailed(UnityAds.UnityAdsInitializationError e, String msg) { initializing = false; fail("init " + e + " " + msg); }
                });
            } catch (Throwable t) { initializing = false; fail(String.valueOf(t)); }
        });
    }

    /** Unity Ads asks for consent through its own flow where needed: no separate settings button. */
    public static boolean privacyOptionsRequired() { return false; }
    public static void showPrivacyOptions() { }

    static void loadInter() {
        if (!started || interReady || loadingInter || interId.isEmpty()) return;
        loadingInter = true;
        UnityAds.load(interId, new IUnityAdsLoadListener() {
            @Override public void onUnityAdsAdLoaded(String p) { interReady = true; loadingInter = false; }
            @Override public void onUnityAdsFailedToLoad(String p, UnityAds.UnityAdsLoadError e, String msg) { loadingInter = false; lastError = "load " + e + " " + msg; }
        });
    }

    static void loadReward() {
        if (!started || rewardReady || loadingReward || rewardId.isEmpty()) return;
        loadingReward = true;
        UnityAds.load(rewardId, new IUnityAdsLoadListener() {
            @Override public void onUnityAdsAdLoaded(String p) { rewardReady = true; loadingReward = false; }
            @Override public void onUnityAdsFailedToLoad(String p, UnityAds.UnityAdsLoadError e, String msg) { loadingReward = false; fail("load " + e + " " + msg); }
        });
    }

    /** One loaded ad was taken: the coins and the between-games ad may be the same unit, so both wait for the next. */
    static void used(String id) {
        if (id.equals(interId)) interReady = false;
        if (id.equals(rewardId)) rewardReady = false;
    }

    public static boolean interstitialReady() { return interReady; }
    public static boolean rewardedReady() { return rewardReady; }

    public static void showInterstitial() {
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> {
            if (!interReady) { send("inter_closed"); loadInter(); return; }
            used(interId);
            UnityAds.show(a, interId, new UnityAdsShowOptions(), new IUnityAdsShowListener() {
                @Override public void onUnityAdsShowFailure(String p, UnityAds.UnityAdsShowError e, String msg) { send("inter_closed"); loadInter(); }
                @Override public void onUnityAdsShowStart(String p) { }
                @Override public void onUnityAdsShowClick(String p) { }
                @Override public void onUnityAdsShowComplete(String p, UnityAds.UnityAdsShowCompletionState s) { send("inter_closed"); loadInter(); loadReward(); }
            });
        });
    }

    /** The rewarded video. The player's id and a one-time ticket go along as the "server id", so Unity can tell
     *  the server the video was watched (server-to-server callback, /ads/unity). */
    public static void showRewarded(final String userId, final String ticket) {
        // not loaded yet (just started, or the last load failed): start (again) and wait for it up to ten seconds
        if (!rewardReady) {
            start(); loadReward();
            final android.os.Handler h = new android.os.Handler(android.os.Looper.getMainLooper());
            final long until = System.currentTimeMillis() + 10_000;
            h.post(new Runnable() { @Override public void run() {
                if (rewardReady) { showRewardedNow(userId, ticket); return; }
                if (System.currentTimeMillis() > until) { send("reward_unavailable"); return; }
                if (started && !loadingReward) loadReward();
                h.postDelayed(this, 400);
            } });
            return;
        }
        showRewardedNow(userId, ticket);
    }

    static void showRewardedNow(final String userId, final String ticket) {
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> {
            if (!rewardReady) { send("reward_unavailable"); loadReward(); return; }
            used(rewardId);
            try { PlayerMetaData md = new PlayerMetaData(a); md.setServerId(userId + ":" + ticket); md.commit(); } catch (Throwable ignored) { }
            UnityAds.show(a, rewardId, new UnityAdsShowOptions(), new IUnityAdsShowListener() {
                @Override public void onUnityAdsShowFailure(String p, UnityAds.UnityAdsShowError e, String msg) { lastError = "show " + e + " " + msg; send("reward_unavailable"); loadReward(); }
                @Override public void onUnityAdsShowStart(String p) { }
                @Override public void onUnityAdsShowClick(String p) { }
                @Override public void onUnityAdsShowComplete(String p, UnityAds.UnityAdsShowCompletionState s) {
                    if (s == UnityAds.UnityAdsShowCompletionState.COMPLETED) send("reward_earned");
                    send("reward_closed"); loadReward(); loadInter();
                }
            });
        });
    }
}
