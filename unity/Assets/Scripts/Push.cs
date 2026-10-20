// Push reminders on the phone: asks for permission to show notifications (Android 13+), starts Firebase Cloud
// Messaging (Plugins/Android/BBPush.java) and keeps the phone's token, which App sends to the server.
using UnityEngine;

public static class Push
{
    public static string Token;

    public static void Begin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            const string perm = "android.permission.POST_NOTIFICATIONS";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(perm)) UnityEngine.Android.Permission.RequestUserPermission(perm);
            using (var c = new AndroidJavaClass("com.ahwa.push.BBPush")) c.CallStatic("init", "Board Battles", Loc.En ? "Reminders" : "تذكير");
        }
        catch (System.Exception e) { Debug.Log("[push] not available: " + e.Message); }
#endif
    }
}
