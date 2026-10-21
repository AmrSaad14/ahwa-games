// Google AdMob for Board Battles: an interstitial between games and a rewarded video for coins.
// The game calls these from C# (Scripts/Ads.cs); results come back as UnitySendMessage("Board Battles", "OnAdEvent", …):
//   inter_closed          the interstitial is gone (shown, or there was none to show)
//   reward_earned         the player watched the rewarded video to the end
//   reward_closed         the rewarded video is gone (earned or not)
//   reward_unavailable    no rewarded video could be shown
package com.ahwa.ads;

import android.app.Activity;

import com.google.android.gms.ads.AdError;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.FullScreenContentCallback;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.MobileAds;
import com.google.android.gms.ads.interstitial.InterstitialAd;
import com.google.android.gms.ads.interstitial.InterstitialAdLoadCallback;
import com.google.android.gms.ads.rewarded.RewardedAd;
import com.google.android.gms.ads.rewarded.RewardedAdLoadCallback;
import com.google.android.gms.ads.rewarded.ServerSideVerificationOptions;
import com.unity3d.player.UnityPlayer;

public class BBAds {
    static String go = "Board Battles", interId = "", rewardId = "";
    static InterstitialAd inter;
    static RewardedAd reward;
    static boolean loadingInter, loadingReward, started;

    static void send(String what) { try { UnityPlayer.UnitySendMessage(go, "OnAdEvent", what); } catch (Throwable ignored) { } }

    public static void init(String gameObject, String interstitialId, String rewardedId) {
        go = gameObject; interId = interstitialId; rewardId = rewardedId;
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> {
            try {
                MobileAds.initialize(a, status -> { started = true; loadInter(); loadReward(); });
            } catch (Throwable t) { send("error:" + t); }
        });
    }

    static void loadInter() {
        if (inter != null || loadingInter || interId.isEmpty()) return;
        loadingInter = true;
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> InterstitialAd.load(a, interId, new AdRequest.Builder().build(), new InterstitialAdLoadCallback() {
            @Override public void onAdLoaded(InterstitialAd ad) { inter = ad; loadingInter = false; }
            @Override public void onAdFailedToLoad(LoadAdError e) { loadingInter = false; send("inter_load_failed:" + e.getCode()); }
        }));
    }

    static void loadReward() {
        if (reward != null || loadingReward || rewardId.isEmpty()) return;
        loadingReward = true;
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> RewardedAd.load(a, rewardId, new AdRequest.Builder().build(), new RewardedAdLoadCallback() {
            @Override public void onAdLoaded(RewardedAd ad) { reward = ad; loadingReward = false; }
            @Override public void onAdFailedToLoad(LoadAdError e) { loadingReward = false; send("reward_load_failed:" + e.getCode()); }
        }));
    }

    public static boolean interstitialReady() { return inter != null; }
    public static boolean rewardedReady() { return reward != null; }

    public static void showInterstitial() {
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> {
            InterstitialAd ad = inter;
            if (ad == null) { send("inter_closed"); loadInter(); return; }
            inter = null;
            ad.setFullScreenContentCallback(new FullScreenContentCallback() {
                @Override public void onAdDismissedFullScreenContent() { send("inter_closed"); loadInter(); }
                @Override public void onAdFailedToShowFullScreenContent(AdError e) { send("inter_closed"); loadInter(); }
            });
            ad.show(a);
        });
    }

    /** The rewarded video; with the player's id and a one-time ticket so AdMob can tell the server (server-side verification). */
    public static void showRewarded(final String userId, final String ticket) {
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(() -> {
            RewardedAd ad = reward;
            if (ad == null) { send("reward_unavailable"); loadReward(); return; }
            reward = null;
            ad.setServerSideVerificationOptions(new ServerSideVerificationOptions.Builder().setUserId(userId).setCustomData(ticket).build());
            ad.setFullScreenContentCallback(new FullScreenContentCallback() {
                @Override public void onAdDismissedFullScreenContent() { send("reward_closed"); loadReward(); }
                @Override public void onAdFailedToShowFullScreenContent(AdError e) { send("reward_unavailable"); loadReward(); }
            });
            ad.show(a, item -> send("reward_earned"));
        });
    }
}
