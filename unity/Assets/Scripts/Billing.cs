// Buying with real money through Google Play: coin packs, the season pass (299 EGP) and the ad-free month.
// Google Play takes the payment (Plugins/Android/BBBilling.java); the app sends the purchase token to the
// server, which checks it with Google and credits it exactly once ("bought"); then the purchase is finished on
// the phone (consumed, or acknowledged for the subscription). Products are set up in the Play Console with
// these ids: season_pass (one-time), no_ads_monthly (subscription). Coins are not sold.
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class Billing
{
    public const string NoAds = "no_ads_monthly";
    static string uid;
    static readonly HashSet<string> sent = new HashSet<string>();

    /// Who is playing (the server's user id): purchases carry a tag made from it, so only this account is credited.
    public static void SetUser(string id)
    {
        bool first = string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(id);
        uid = id;
        if (first) Restore();   // anything paid for last time but not credited yet
    }
    static string Tag()
    {
        using (var md5 = MD5.Create())
        {
            var h = md5.ComputeHash(Encoding.UTF8.GetBytes(uid ?? ""));
            var sb = new StringBuilder(); foreach (var b in h) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    public static void Buy(string product)
    {
        var app = App.I;
        if (string.IsNullOrEmpty(uid)) { app?.Toast(Loc.T("استنى لحد ما تتصل بالسيرفر")); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        try { using (var c = new AndroidJavaClass("com.ahwa.extras.BBBilling")) c.CallStatic("buy", Activity(), product, product == NoAds, Tag()); }
        catch (System.Exception e) { Debug.Log("[billing] " + e.Message); app?.Toast(Loc.T("الشراء مش متاح دلوقتي")); }
#else
        app?.Toast(Loc.T("الشراء من Google Play على الموبايل بس"));
#endif
    }

    static void Restore()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { using (var c = new AndroidJavaClass("com.ahwa.extras.BBBilling")) c.CallStatic("restore", Activity()); }
        catch (System.Exception e) { Debug.Log("[billing] " + e.Message); }
#endif
    }

    /// Google Play says it's paid: ask the server to check it and credit it.
    public static void OnPurchase(string value)
    {
        int bar = value.IndexOf('|'); if (bar <= 0) return;
        string product = value.Substring(0, bar), token = value.Substring(bar + 1);
        Debug.Log("[billing] paid " + product);
        sent.Add(token);
        App.I?.Send(new Dictionary<string, object> { ["t"] = "buy", ["product"] = product, ["token"] = token });
    }

    /// The server credited it: finish it on the phone, and say so.
    public static void OnBought(Dictionary<string, object> m)
    {
        string product = m.Str("product"), token = m.Str("token");
#if UNITY_ANDROID && !UNITY_EDITOR
        try { using (var c = new AndroidJavaClass("com.ahwa.extras.BBBilling")) c.CallStatic("finish", Activity(), token, product != NoAds); }
        catch (System.Exception e) { Debug.Log("[billing] " + e.Message); }
#endif
        var app = App.I; if (app == null) return;
        if (product == "season_pass") app.Toast(Loc.T("المميز اتفتح! الموسم كله بقى ليك"));
        else if (product == NoAds) app.Toast(Loc.T("مفيش إعلانات خلاص، استمتع"));
        else if (m.Num("coins") > 0) { app.Toast("+" + Loc.Short((long)m.Num("coins"))); Sfx.Coins(); }
    }

    public static void OnFailed(Dictionary<string, object> m)
    {
        string code = m.Str("code");
        Debug.Log("[billing] refused: " + code);
        App.I?.Toast(Loc.T(code == "purchases_off" ? "الشراء لسه مش شغال على السيرفر" : code == "purchase_pending" ? "الدفع لسه بيتأكد، هيوصلك أول ما يخلص" : "الشراء ما تمش، محدش خصم منك حاجة"));
    }

    public static void OnError(string code)
    {
        Debug.Log("[billing] " + code);
        if (code == "cancelled") return;
        App.I?.Toast(Loc.T(code == "pending" ? "الدفع لسه بيتأكد، هيوصلك أول ما يخلص" : code == "no_product" ? "المنتج ده لسه مش متاح في Google Play" : "الشراء مش متاح دلوقتي"));
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject Activity()
    {
        using (var p = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) return p.GetStatic<AndroidJavaObject>("currentActivity");
    }
#endif
}
