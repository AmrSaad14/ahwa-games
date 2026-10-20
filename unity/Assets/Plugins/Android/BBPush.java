// Push reminders for Board Battles: starts Firebase Cloud Messaging and hands the phone's token to the game
// (App.OnPushToken). Firebase reads its settings from the resources Editor/PushGradle.cs writes from
// google-services.json; without them push is simply off.
package com.ahwa.push;

import android.app.Activity;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.content.Context;
import android.os.Build;

import com.google.android.gms.tasks.OnCompleteListener;
import com.google.android.gms.tasks.Task;
import com.google.firebase.FirebaseApp;
import com.google.firebase.messaging.FirebaseMessaging;
import com.unity3d.player.UnityPlayer;

public class BBPush {
    public static final String CHANNEL = "reminders";

    public static void init(final String gameObject, final String channelName) {
        final Activity act = UnityPlayer.currentActivity;
        try {
            if (FirebaseApp.getApps(act).isEmpty() && FirebaseApp.initializeApp(act) == null) {
                UnityPlayer.UnitySendMessage(gameObject, "OnPushError", "not configured (no google-services.json in this build)");
                return;
            }
            channel(act, channelName);
            FirebaseMessaging.getInstance().getToken().addOnCompleteListener(new OnCompleteListener<String>() {
                @Override public void onComplete(Task<String> task) {
                    if (task.isSuccessful() && task.getResult() != null) UnityPlayer.UnitySendMessage(gameObject, "OnPushToken", task.getResult());
                    else UnityPlayer.UnitySendMessage(gameObject, "OnPushError", String.valueOf(task.getException()));
                }
            });
        } catch (Throwable t) {
            UnityPlayer.UnitySendMessage(gameObject, "OnPushError", t.toString());
        }
    }

    static void channel(Context c, String name) {
        if (Build.VERSION.SDK_INT < 26) return;
        NotificationManager nm = (NotificationManager) c.getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm == null || nm.getNotificationChannel(CHANNEL) != null) return;
        nm.createNotificationChannel(new NotificationChannel(CHANNEL, name, NotificationManager.IMPORTANCE_DEFAULT));
    }
}
