// Unity Ads for Board Battles: an interstitial between games and a rewarded video for coins.
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
import com.unity3d.ads.metadata.PlayerMetaData;
import com.unity3d.player.UnityPlayer;

public class BBAds {
    static String go = "Board Battles", interId = "", rewardId = "";
    static boolean interReady, rewardReady, loadingInter, loadingReward, started;

    static void send(String what) { try { UnityPlayer.UnitySendMessage(go, "OnAdEvent", what); } catch (Throwable ignored) { } }

    public static void init(String gameObject, String gameId, String interstitialId, String rewardedId, boolean test) {
        go = gameObject; interId = interstitialId; rewardId = rewardedId;
        final Activity a = UnityPlayer.currentActivity;
        if (gameId == null || gameId.isEmpty()) { send("error:no game id"); return; }
        a.runOnUiThread(() -> {
            try {
                UnityAds.initialize(a.getApplicationContext(), gameId, test, new IUnityAdsInitializationListener() {
                    @Override public void onInitializationComplete() { started = true; loadInter(); loadReward(); }
                    @Override public void onInitializationFailed(UnityAds.UnityAdsInitializationError e, String msg) { send("error:init " + e + " " + msg); }
                });
            } catch (Throwable t) { send("error:" + t); }
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
            @Override public void onUnityAdsFailedToLoad(String p, UnityAds.UnityAdsLoadError e, String msg) { loadingInter = false; send("inter_load_failed:" + e); }
        });
    }

    static void loadReward() {
        if (!started || rewardReady || loadingReward || rewardId.isEmpty()) return;
        loadingReward = true;
        UnityAds.load(rewardId, new IUnityAdsLoadListener() {
            @Override public void onUnityAdsAdLoaded(String p) { rewardReady = true; loadingReward = false; }
            @Override public void onUnityAdsFailedToLoad(String p, UnityAds.UnityAdsLoadError e, String msg) { loadingReward = false; send("reward_load_failed:" + e); }
        });
    }

    public static boolean interstitialReady() { return interReady; }
    public static boolean rewardedReady() { return rewardReady; }

    public static void showInterstitial() {
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> {
            if (!interReady) { send("inter_closed"); loadInter(); return; }
            interReady = false;
            UnityAds.show(a, interId, new UnityAdsShowOptions(), new IUnityAdsShowListener() {
                @Override public void onUnityAdsShowFailure(String p, UnityAds.UnityAdsShowError e, String msg) { send("inter_closed"); loadInter(); }
                @Override public void onUnityAdsShowStart(String p) { }
                @Override public void onUnityAdsShowClick(String p) { }
                @Override public void onUnityAdsShowComplete(String p, UnityAds.UnityAdsShowCompletionState s) { send("inter_closed"); loadInter(); }
            });
        });
    }

    /** The rewarded video. The player's id and a one-time ticket go along as the "server id", so Unity can tell
     *  the server the video was watched (server-to-server callback, /ads/unity). */
    public static void showRewarded(final String userId, final String ticket) {
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> {
            if (!rewardReady) { send("reward_unavailable"); loadReward(); return; }
            rewardReady = false;
            try { PlayerMetaData md = new PlayerMetaData(a); md.setServerId(userId + ":" + ticket); md.commit(); } catch (Throwable ignored) { }
            UnityAds.show(a, rewardId, new UnityAdsShowOptions(), new IUnityAdsShowListener() {
                @Override public void onUnityAdsShowFailure(String p, UnityAds.UnityAdsShowError e, String msg) { send("reward_unavailable"); loadReward(); }
                @Override public void onUnityAdsShowStart(String p) { }
                @Override public void onUnityAdsShowClick(String p) { }
                @Override public void onUnityAdsShowComplete(String p, UnityAds.UnityAdsShowCompletionState s) {
                    if (s == UnityAds.UnityAdsShowCompletionState.COMPLETED) send("reward_earned");
                    send("reward_closed"); loadReward();
                }
            });
        });
    }
}
