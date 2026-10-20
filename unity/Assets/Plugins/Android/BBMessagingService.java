// Shows a push that arrives while the game is open (Android shows the others by itself), and keeps the token fresh.
package com.ahwa.push;

import android.app.Notification;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.os.Build;

import com.google.firebase.messaging.FirebaseMessagingService;
import com.google.firebase.messaging.RemoteMessage;
import com.unity3d.player.UnityPlayer;

public class BBMessagingService extends FirebaseMessagingService {
    @Override public void onNewToken(String token) {
        try { UnityPlayer.UnitySendMessage("Board Battles", "OnPushToken", token); } catch (Throwable ignored) { }
    }

    @Override public void onMessageReceived(RemoteMessage msg) {
        RemoteMessage.Notification n = msg.getNotification();
        if (n == null) return;
        Context c = getApplicationContext();
        BBPush.channel(c, "Reminders");
        Intent open = c.getPackageManager().getLaunchIntentForPackage(c.getPackageName());
        if (open != null) open.addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP);
        int flags = PendingIntent.FLAG_UPDATE_CURRENT | (Build.VERSION.SDK_INT >= 23 ? PendingIntent.FLAG_IMMUTABLE : 0);
        PendingIntent pi = open != null ? PendingIntent.getActivity(c, 0, open, flags) : null;
        int icon = c.getResources().getIdentifier("ic_stat_bb", "drawable", c.getPackageName());
        Notification.Builder b = Build.VERSION.SDK_INT >= 26 ? new Notification.Builder(c, BBPush.CHANNEL) : new Notification.Builder(c);
        b.setSmallIcon(icon != 0 ? icon : android.R.drawable.ic_dialog_info)
         .setContentTitle(n.getTitle()).setContentText(n.getBody()).setAutoCancel(true).setColor(0xFFF3CD62);
        if (pi != null) b.setContentIntent(pi);
        NotificationManager nm = (NotificationManager) c.getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm != null) nm.notify((int) (System.currentTimeMillis() & 0x7fffffff), b.build());
    }
}
