// The phone's rough location (network or last known, about a city's size), only to find the player's governorate
// for the governorate ranking. Nothing here keeps or sends the location: Scripts/GovLocation.cs reads it once and
// sends it to the server, which turns it into a governorate and keeps only that.
// Needs ACCESS_COARSE_LOCATION (asked by the game first).
package com.ahwa.location;

import android.content.Context;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.Bundle;
import android.os.Looper;
import com.unity3d.player.UnityPlayer;

public class BBLocation {
    static volatile double lat, lon;
    static volatile int state;   // 0 waiting, 1 found, -1 failed

    /** Starts looking: a recent last-known fix at once, otherwise one new fix from the network (or GPS). */
    public static void start() {
        state = 0;
        try {
            final LocationManager lm = (LocationManager) UnityPlayer.currentActivity.getSystemService(Context.LOCATION_SERVICE);
            if (lm == null) { state = -1; return; }
            Location best = null;
            for (String p : new String[] { LocationManager.NETWORK_PROVIDER, LocationManager.PASSIVE_PROVIDER, LocationManager.GPS_PROVIDER }) {
                try {
                    Location l = lm.getLastKnownLocation(p);
                    if (l != null && (best == null || l.getTime() > best.getTime())) best = l;
                } catch (Throwable t) { /* that provider isn't allowed */ }
            }
            if (best != null && System.currentTimeMillis() - best.getTime() < 3L * 86_400_000L) { found(best); return; }
            final String provider = lm.isProviderEnabled(LocationManager.NETWORK_PROVIDER) ? LocationManager.NETWORK_PROVIDER
                : lm.isProviderEnabled(LocationManager.GPS_PROVIDER) ? LocationManager.GPS_PROVIDER : null;
            if (provider == null) { if (best != null) found(best); else state = -1; return; }
            final Location old = best;
            UnityPlayer.currentActivity.runOnUiThread(new Runnable() {
                public void run() {
                    try {
                        lm.requestSingleUpdate(provider, new LocationListener() {
                            public void onLocationChanged(Location l) { found(l); }
                            public void onStatusChanged(String p, int s, Bundle b) { }
                            public void onProviderEnabled(String p) { }
                            public void onProviderDisabled(String p) { if (state == 0) { if (old != null) found(old); else state = -1; } }
                        }, Looper.getMainLooper());
                    } catch (Throwable t) { if (old != null) found(old); else state = -1; }
                }
            });
        } catch (Throwable t) { state = -1; }
    }

    static void found(Location l) { lat = l.getLatitude(); lon = l.getLongitude(); state = 1; }
    public static int state() { return state; }
    public static double lat() { return lat; }
    public static double lon() { return lon; }
}
