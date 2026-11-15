// Google Play Games Services (v2): the player is signed in to Play Games by the phone itself (no button, a
// "Welcome back" bar), and the game asks Play Games for a one-time server code that the server trades for the
// player's Play Games id (server/login.ts), so the same account comes back on any phone with the same Play Games
// profile. Scripts/PlayGames.cs drives it and polls state().
// On only when the build has the game's Play Games project id (Editor/PushGradle.cs writes the manifest entry from
// the PLAY_GAMES_APP_ID variable); without it every call here just answers "off".
package com.ahwa.games;

import android.app.Activity;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import com.google.android.gms.games.GamesSignInClient;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;
import com.unity3d.player.UnityPlayer;

public class BBGames {
    static boolean ready;
    static volatile int state;        // 0 working, 1 got a code, -1 not signed in / failed, -2 off in this build
    static volatile String code = "", error = "";

    /** Whether this build has a Play Games project id. */
    public static boolean available() {
        try {
            Activity a = UnityPlayer.currentActivity;
            ApplicationInfo ai = a.getPackageManager().getApplicationInfo(a.getPackageName(), PackageManager.GET_META_DATA);
            return ai.metaData != null && ai.metaData.get("com.google.android.gms.games.APP_ID") != null;
        } catch (Throwable t) { return false; }
    }

    /**
     * Gets a server code for serverClientId: quietly when the phone is already signed in to Play Games
     * (ask=false), or showing Play Games' own sign-in when the player taps the button (ask=true).
     */
    public static void start(final String serverClientId, final boolean ask) {
        code = ""; error = "";
        if (!available()) { state = -2; return; }
        state = 0;
        final Activity a = UnityPlayer.currentActivity;
        a.runOnUiThread(new Runnable() {
            public void run() {
                try {
                    if (!ready) { PlayGamesSdk.initialize(a.getApplicationContext()); ready = true; }
                    final GamesSignInClient c = PlayGames.getGamesSignInClient(a);
                    (ask ? c.signIn() : c.isAuthenticated()).addOnCompleteListener(t -> {
                        boolean ok = t.isSuccessful() && t.getResult() != null && t.getResult().isAuthenticated();
                        if (!ok) { error = t.getException() != null ? String.valueOf(t.getException().getMessage()) : "not signed in"; state = -1; return; }
                        c.requestServerSideAccess(serverClientId, false).addOnCompleteListener(s -> {
                            if (s.isSuccessful() && s.getResult() != null) { code = s.getResult(); state = 1; }
                            else { error = s.getException() != null ? String.valueOf(s.getException().getMessage()) : "no code"; state = -1; }
                        });
                    });
                } catch (Throwable e) { error = String.valueOf(e.getMessage()); state = -1; }
            }
        });
    }

    public static int state() { return state; }
    public static String code() { return code; }
    public static String error() { return error; }
}
