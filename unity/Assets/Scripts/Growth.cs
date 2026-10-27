// Helping the game grow: the Google Play review card after your third win (never after a loss), sharing a
// result card to WhatsApp and friends with your invite link, and who invited a new player (the Play install
// referrer, or a code typed in Settings) so you both get the referral bonus. Android helpers in BBExtras.java.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class Growth
{
    /* ---------- the review card ---------- */

    /// A win just happened: on the third one ever, ask for a review (once).
    public static void Won()
    {
        int n = PlayerPrefs.GetInt("wins.count", 0) + 1;
        PlayerPrefs.SetInt("wins.count", n); PlayerPrefs.Save();
        if (n < 3 || PlayerPrefs.GetInt("review.asked", 0) == 1 || App.Recording) return;
        PlayerPrefs.SetInt("review.asked", 1); PlayerPrefs.Save();
        App.I.StartCoroutine(AskSoon());
    }

    static IEnumerator AskSoon()
    {
        yield return new WaitForSeconds(2.2f);   // after the celebration
#if UNITY_ANDROID && !UNITY_EDITOR
        try { using (var c = new AndroidJavaClass("com.ahwa.extras.BBExtras")) c.CallStatic("review", Activity()); }
        catch (Exception e) { Debug.Log("[review] " + e.Message); }
#endif
        yield break;
    }

    /* ---------- sharing ---------- */

    /// Your invite link: opens the game (or Google Play for a new player, who is then credited to you).
    public static string InviteLink(string code)
    {
        var net = App.I != null ? App.I.net : null;
        return (net != null ? net.HttpBase : "https://ahwa-games.a7medhussien.com") + "/i/" + (code ?? "").ToUpperInvariant();
    }

    /// The share sheet with a message (and a picture, when there is one).
    public static void Share(string text, string imagePath = null)
    {
        GUIUtility.systemCopyBuffer = text;
#if UNITY_ANDROID && !UNITY_EDITOR
        try { using (var c = new AndroidJavaClass("com.ahwa.extras.BBExtras")) c.CallStatic("share", Activity(), imagePath ?? "", text); return; }
        catch (Exception e) { Debug.Log("[share] " + e.Message); }
#endif
        App.I?.Toast(Loc.T("الرسالة اتنسخت"));
    }

    /// A picture of the screen as it is now (the result over the café), shared with a message.
    public static IEnumerator ShareScreen(string text, Action before = null, Action after = null)
    {
        before?.Invoke();
        yield return new WaitForEndOfFrame();
        string path = null;
        try
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            // not too big for a chat app
            if (tex.width > 1600)
            {
                var rt = RenderTexture.GetTemporary(1600, tex.height * 1600 / tex.width);
                Graphics.Blit(tex, rt);
                var small = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                small.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); small.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(tex); tex = small;
            }
            path = Path.Combine(Application.temporaryCachePath, "board-battles.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
        catch (Exception e) { Debug.Log("[share] picture: " + e.Message); path = null; }
        after?.Invoke();
        Share(text, path);
    }

    /* ---------- who invited this player ---------- */

    public static string PendingCode;   // a code waiting until we're connected (and the account is new)

    /// First start: ask Google Play who sent this install (a friend's invite link carries "ref=CODE").
    public static void CheckReferrer()
    {
        if (PlayerPrefs.GetInt("ref.checked", 0) == 1 || App.Recording) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        try { using (var c = new AndroidJavaClass("com.ahwa.extras.BBExtras")) c.CallStatic("referrer", Activity()); }
        catch (Exception e) { Debug.Log("[referrer] " + e.Message); }
#endif
    }

    /// From Google Play: "ref=CODE" (or another campaign's text).
    public static void OnReferrer(string value)
    {
        PlayerPrefs.SetInt("ref.checked", 1); PlayerPrefs.Save();
        if (string.IsNullOrEmpty(value)) return;
        foreach (var part in Uri.UnescapeDataString(value).Split('&'))
            if (part.StartsWith("ref=")) { PendingCode = part.Substring(4).Trim(); Debug.Log("[referrer] invited by a friend"); }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject Activity()
    {
        using (var p = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) return p.GetStatic<AndroidJavaObject>("currentActivity");
    }
#endif
}
