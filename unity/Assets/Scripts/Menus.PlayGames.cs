// Google Play Games: a guest whose phone is signed in to Play Games gets their account at once (no button), and
// the sign-in card has a Play Games button. Play Games gives a one-time server code (Plugins/Android/BBGames.java)
// and the server trades it for the player's Play Games id (server/login.ts), so the same account comes back on any
// phone with the same Play Games profile. On only when the server has Google sign-in set up (its client id comes in
// "welcome") and the build has the game's Play Games project id.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Menus
{
    string pgClient;          // the server's Google client id, when Play Games sign-in is on there
    bool pgBusy, pgTriedQuiet;
    int pgAvail = -1;         // whether this build has Play Games (-1 not asked yet)

    bool PlayGamesOn
    {
        get
        {
            if (string.IsNullOrEmpty(pgClient) || Application.platform != RuntimePlatform.Android) return false;
            if (pgAvail < 0)
            {
                try { using (var c = new AndroidJavaClass("com.ahwa.games.BBGames")) pgAvail = c.CallStatic<bool>("available") ? 1 : 0; }
                catch (System.Exception e) { Debug.Log("[games] " + e.Message); pgAvail = 0; }
            }
            return pgAvail == 1;
        }
    }

    /// From "welcome": a guest signed in to Play Games on this phone is signed in quietly, once per launch.
    void OnLoginPlayGames(Dictionary<string, object> login)
    {
        // (only remembered: Play Games signs you in when you choose it under "Sign in", never by itself)
        pgClient = login?.Str("playGames");
    }

    /// ask: the player tapped the button (Play Games shows its own sign-in); otherwise only if already signed in.
    IEnumerator PlayGamesIn(bool ask)
    {
        if (pgBusy) yield break;
        if (!ask) { yield return new WaitForSeconds(1.5f); if (app.SignedIn) yield break; }   // (the account may already be one)
        pgBusy = true;
        int state = 0; string code = null, error = null;
        try { using (var c = new AndroidJavaClass("com.ahwa.games.BBGames")) c.CallStatic("start", pgClient, ask); }
        catch (System.Exception e) { error = e.Message; state = -1; }
        float start = Time.realtimeSinceStartup;
        while (state == 0 && Time.realtimeSinceStartup - start < (ask ? 90 : 30))
        {
            yield return new WaitForSeconds(0.4f);
            try
            {
                using (var c = new AndroidJavaClass("com.ahwa.games.BBGames"))
                {
                    state = c.CallStatic<int>("state");
                    if (state == 1) code = c.CallStatic<string>("code");
                    else if (state < 0) error = c.CallStatic<string>("error");
                }
            }
            catch (System.Exception e) { error = e.Message; state = -1; }
        }
        pgBusy = false;
        if (state == 1 && !string.IsNullOrEmpty(code))
        {
            Debug.Log("[games] signed in to Play Games, sending the code");
            app.Send(new Dictionary<string, object> { ["t"] = "playGames", ["code"] = code });
            yield break;
        }
        Debug.Log("[games] no Play Games sign-in: " + (error ?? "timed out"));
        if (ask) app.Toast(Loc.T("ماقدرناش ندخلك بـ Play Games، جرّب تاني"));
    }

    void SignInPlayGames() { Sfx.Tap(); if (open != null) Close(); StartCoroutine(PlayGamesIn(true)); }
}
