// Board Battles' small Android helpers, called from C# (Scripts/Growth.cs):
//  - review(): Google Play's in-app review card (Play decides whether to show it)
//  - share(imagePath, text): the share sheet with a picture (the result card) and a message
//  - referrer(): who invited this install (Play's install referrer: "ref=CODE"), sent back as
//    UnitySendMessage("Board Battles", "OnReferrer", value)
package com.ahwa.extras;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;

import androidx.core.content.FileProvider;

import com.android.installreferrer.api.InstallReferrerClient;
import com.android.installreferrer.api.InstallReferrerStateListener;
import com.android.installreferrer.api.ReferrerDetails;
import com.google.android.play.core.review.ReviewInfo;
import com.google.android.play.core.review.ReviewManager;
import com.google.android.play.core.review.ReviewManagerFactory;
import com.unity3d.player.UnityPlayer;

import java.io.File;

public class BBExtras {
    static void send(String method, String value) { try { UnityPlayer.UnitySendMessage("Board Battles", method, value); } catch (Throwable ignored) { } }

    public static void review(final Activity activity) {
        activity.runOnUiThread(() -> {
            try {
                final ReviewManager manager = ReviewManagerFactory.create(activity);
                manager.requestReviewFlow().addOnCompleteListener(task -> {
                    if (!task.isSuccessful()) { send("OnReview", "unavailable"); return; }
                    ReviewInfo info = task.getResult();
                    manager.launchReviewFlow(activity, info).addOnCompleteListener(t -> send("OnReview", "done"));
                });
            } catch (Throwable e) { send("OnReview", "error"); }
        });
    }

    public static void share(final Activity activity, final String imagePath, final String text) {
        activity.runOnUiThread(() -> {
            try {
                Intent i = new Intent(Intent.ACTION_SEND);
                i.putExtra(Intent.EXTRA_TEXT, text);
                File f = imagePath == null || imagePath.isEmpty() ? null : new File(imagePath);
                if (f != null && f.exists()) {
                    Uri uri = FileProvider.getUriForFile(activity, activity.getPackageName() + ".bbshare", f);
                    i.setType("image/png");
                    i.putExtra(Intent.EXTRA_STREAM, uri);
                    i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                } else i.setType("text/plain");
                activity.startActivity(Intent.createChooser(i, "Board Battles"));
            } catch (Throwable e) {
                try {
                    Intent t = new Intent(Intent.ACTION_SEND); t.setType("text/plain"); t.putExtra(Intent.EXTRA_TEXT, text);
                    activity.startActivity(Intent.createChooser(t, "Board Battles"));
                } catch (Throwable ignored) { }
            }
        });
    }

    public static void referrer(final Activity activity) {
        try {
            final InstallReferrerClient client = InstallReferrerClient.newBuilder(activity).build();
            client.startConnection(new InstallReferrerStateListener() {
                @Override public void onInstallReferrerSetupFinished(int code) {
                    String value = "";
                    if (code == InstallReferrerClient.InstallReferrerResponse.OK) {
                        try { ReferrerDetails d = client.getInstallReferrer(); value = d.getInstallReferrer(); } catch (Throwable ignored) { }
                    }
                    try { client.endConnection(); } catch (Throwable ignored) { }
                    send("OnReferrer", value == null ? "" : value);
                }
                @Override public void onInstallReferrerServiceDisconnected() { }
            });
        } catch (Throwable e) { send("OnReferrer", ""); }
    }
}
