// Board Battles' forced update, called from C# (Scripts/Updater.cs), through Google Play's in-app updates:
//  - check(): is a newer version on Play for this phone? Answers UnitySendMessage("Board Battles", "OnUpdateCheck",
//    "available:<versionCode>" | "none" | "error")
//  - start(): Play's full-screen "update now" (download, install and restart in one go). Answers "OnUpdateFlow"
//    with "ok" | "cancelled" | "failed" (cancelled: the game's own dialog stays up and asks again)
//  - store(): the game's page in the Play Store (when the in-app update can't run, e.g. not installed from Play)
package com.ahwa.extras;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;

import com.google.android.play.core.appupdate.AppUpdateInfo;
import com.google.android.play.core.appupdate.AppUpdateManager;
import com.google.android.play.core.appupdate.AppUpdateManagerFactory;
import com.google.android.play.core.appupdate.AppUpdateOptions;
import com.google.android.play.core.install.model.AppUpdateType;
import com.google.android.play.core.install.model.UpdateAvailability;
import com.unity3d.player.UnityPlayer;

public class BBUpdate {
    static void send(String method, String value) { try { UnityPlayer.UnitySendMessage("Board Battles", method, value); } catch (Throwable ignored) { } }

    static boolean pending(AppUpdateInfo info) {
        int a = info.updateAvailability();
        return a == UpdateAvailability.DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS
            || (a == UpdateAvailability.UPDATE_AVAILABLE && info.isUpdateTypeAllowed(AppUpdateType.IMMEDIATE));
    }

    public static void check(final Activity activity) {
        try {
            AppUpdateManager m = AppUpdateManagerFactory.create(activity);
            m.getAppUpdateInfo()
                .addOnSuccessListener(info -> send("OnUpdateCheck", pending(info) ? "available:" + info.availableVersionCode() : "none"))
                .addOnFailureListener(e -> send("OnUpdateCheck", "error"));
        } catch (Throwable e) { send("OnUpdateCheck", "error"); }
    }

    public static void start(final Activity activity) {
        activity.runOnUiThread(() -> {
            try {
                final AppUpdateManager m = AppUpdateManagerFactory.create(activity);
                m.getAppUpdateInfo()
                    .addOnSuccessListener(info -> {
                        if (!pending(info)) { send("OnUpdateFlow", "failed"); return; }
                        try {
                            m.startUpdateFlow(info, activity, AppUpdateOptions.defaultOptions(AppUpdateType.IMMEDIATE))
                                .addOnSuccessListener(code -> send("OnUpdateFlow", code != null && code == Activity.RESULT_OK ? "ok" : code != null && code == Activity.RESULT_CANCELED ? "cancelled" : "failed"))
                                .addOnFailureListener(e -> send("OnUpdateFlow", "failed"));
                        } catch (Throwable e) { send("OnUpdateFlow", "failed"); }
                    })
                    .addOnFailureListener(e -> send("OnUpdateFlow", "failed"));
            } catch (Throwable e) { send("OnUpdateFlow", "failed"); }
        });
    }

    public static void store(final Activity activity) {
        activity.runOnUiThread(() -> {
            String id = activity.getPackageName();
            try {
                Intent i = new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + id));
                i.setPackage("com.android.vending");
                i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                activity.startActivity(i);
            } catch (Throwable e) {
                try { activity.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + id))); } catch (Throwable ignored) { }
            }
        });
    }
}
