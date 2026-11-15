// Push reminders (Firebase Cloud Messaging) in the Android build. After Unity writes its Gradle project this:
//  - adds Firebase Messaging to it,
//  - copies the notification icon and colour (unity/AndroidRes),
//  - turns google-services.json (unity/google-services.json, written by the build workflow from the
//    GOOGLE_SERVICES_JSON secret) into the resources Firebase starts from. Without that file the game builds and
//    plays as usual, with push off.
using System.IO;
using System.Text;
using UnityEditor.Android;
using UnityEngine;

public class PushGradle : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 10;
    const string Package = "com.ahwa.boardbattles";

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // 1. the library
        var gradle = Path.Combine(path, "build.gradle");
        var g = File.ReadAllText(gradle);
        if (!g.Contains("firebase-messaging"))
            File.AppendAllText(gradle, "\n// push reminders and ads (Editor/PushGradle.cs)\ndependencies {\n    implementation platform('com.google.firebase:firebase-bom:33.7.0')\n    implementation 'com.google.firebase:firebase-messaging'\n}\n");
        if (!g.Contains("installreferrer"))
            File.AppendAllText(gradle, "\n// the review card, sharing a picture, the install referrer (Plugins/Android/BBExtras.java)\ndependencies {\n    implementation 'com.google.android.play:review:2.0.2'\n    implementation 'androidx.core:core:1.13.1'\n    implementation 'com.android.installreferrer:installreferrer:2.2'\n}\n");
        if (!g.Contains("unity-ads"))
            File.AppendAllText(gradle, "\n// Unity Ads: the interstitial and the rewarded video (Plugins/Android/BBAds.java)\ndependencies {\n    implementation 'com.unity3d.ads:unity-ads:4.18.0'\n}\n");
        if (!g.Contains("billingclient"))
            File.AppendAllText(gradle, "\n// Google Play purchases: coin packs, the season pass, the ad-free month (Plugins/Android/BBBilling.java)\ndependencies {\n    implementation 'com.android.billingclient:billing:8.3.0'\n}\n");
        if (!g.Contains("app-update"))
            File.AppendAllText(gradle, "\n// the forced update: Play's in-app update (Plugins/Android/BBUpdate.java)\ndependencies {\n    implementation 'com.google.android.play:app-update:2.1.0'\n}\n");
        // Google Play Games Services v2 (Plugins/Android/BBGames.java)
        if (!g.Contains("play-services-games-v2"))
            File.AppendAllText(gradle, "\n// Google Play Games: signing in with the phone's Play Games profile (Plugins/Android/BBGames.java)\ndependencies {\n    implementation 'com.google.android.gms:play-services-games-v2:21.0.0'\n}\n");
        // Play's SDK index flags fragment 1.1.0 as outdated: the Google Play libraries still ask for it, so a newer one
        // is named here and Gradle takes the newest
        if (!g.Contains("androidx.fragment:fragment"))
            File.AppendAllText(gradle, "\n// a current androidx.fragment (Play Console: fragment 1.1.0 is outdated)\ndependencies {\n    implementation 'androidx.fragment:fragment:1.8.5'\n}\n");
        PlayGamesId(path);
        // R8 (code shrinking and optimising, BuildScript: minifyRelease): the classes C# calls by name stay whole (a library's
        // "consumer" rules file: only keep/dontwarn/keepattributes, no global options such as -ignorewarnings).
        // The libraries (Firebase, Play, billing, Unity Ads) bring their own rules; Unity keeps its player classes.
        File.WriteAllText(Path.Combine(path, "proguard-bb.txt"),
            "# Board Battles (Editor/PushGradle.cs): the Java helpers called from C# through JNI, by class and method name\n" +
            "-keep class com.ahwa.** { *; }\n" +
            "-keep class com.unity3d.player.** { *; }\n" +
            "# voice chat (com.unity.webrtc): its native libraries find these classes by name when they load; without them\n" +
            "# the app closes at once\n" +
            "-keep class org.webrtc.** { *; }\n" +
            "-keep class com.unity.webrtc.** { *; }\n" +
            "-keep class org.fmod.** { *; }\n" +
            "# any class with native methods keeps its name (JNI looks them up by name)\n" +
            "-keepclasseswithmembernames,includedescriptorclasses class * { native <methods>; }\n" +
            "-dontwarn org.webrtc.**\n" +
            "-keepattributes *Annotation*,Signature,InnerClasses,EnclosingMethod,SourceFile,LineNumberTable\n" +
            "-dontwarn com.unity3d.**\n" +
            "-dontwarn com.google.android.gms.**\n" +
            "-dontwarn javax.annotation.**\n");
        if (!g.Contains("proguard-bb.txt"))
            File.AppendAllText(gradle, "\n// the keep rules for R8 (Editor/PushGradle.cs)\nandroid {\n    defaultConfig {\n        consumerProguardFiles 'proguard-bb.txt'\n    }\n}\n");
        var props = Path.Combine(Path.GetDirectoryName(path), "gradle.properties");
        if (File.Exists(props))
        {
            var p = File.ReadAllText(props);
            if (!p.Contains("android.useAndroidX")) File.AppendAllText(props, "\nandroid.useAndroidX=true\n");
        }

        // 2. the icon and colour
        var res = Path.Combine(path, "src", "main", "res");
        Copy(Path.Combine(Directory.GetCurrentDirectory(), "AndroidRes"), res);

        // 3. the Firebase project
        var json = Path.Combine(Directory.GetCurrentDirectory(), "google-services.json");
        if (!File.Exists(json)) { Debug.Log("[push] no google-services.json: push reminders are off in this build"); return; }
        try
        {
            var root = Json.Parse(File.ReadAllText(json)) as System.Collections.Generic.Dictionary<string, object>;
            var info = root.Obj("project_info");
            string projectId = info.Str("project_id"), sender = info.Str("project_number"), bucket = info.Str("storage_bucket");
            string appId = "", apiKey = "";
            foreach (var c in root.Arr("client") ?? new System.Collections.Generic.List<object>())
            {
                var ci = c.Obj("client_info");
                var pkg = ci?.Obj("android_client_info")?.Str("package_name");
                if (pkg != Package && appId != "") continue;
                appId = ci.Str("mobilesdk_app_id");
                var keys = c.Arr("api_key");
                if (keys != null && keys.Count > 0) apiKey = keys[0].Str("current_key");
                if (pkg == Package) break;
            }
            if (appId == "" || apiKey == "") { Debug.LogWarning("[push] google-services.json has no Android app " + Package); return; }
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n");
            void S(string name, string v) { if (!string.IsNullOrEmpty(v)) sb.Append($"  <string name=\"{name}\" translatable=\"false\">{System.Security.SecurityElement.Escape(v)}</string>\n"); }
            S("google_app_id", appId); S("gcm_defaultSenderId", sender); S("google_api_key", apiKey);
            S("google_crash_reporting_api_key", apiKey); S("project_id", projectId); S("google_storage_bucket", bucket);
            sb.Append("</resources>\n");
            Directory.CreateDirectory(Path.Combine(res, "values"));
            File.WriteAllText(Path.Combine(res, "values", "firebase_config.xml"), sb.ToString());
            Debug.Log("[push] Firebase project " + projectId + " set up for push reminders");
        }
        catch (System.Exception e) { Debug.LogWarning("[push] couldn't read google-services.json: " + e.Message); }
    }

    /// The game's Play Games project id (Play Console › Play Games Services › Configuration), from
    /// unity/play-games-id.txt (the build workflow writes it from the PLAY_GAMES_APP_ID variable). With it the
    /// manifest gets the entry Play Games starts from; without it Play Games stays off and the build is as before.
    static void PlayGamesId(string path)
    {
        var file = Path.Combine(Directory.GetCurrentDirectory(), "play-games-id.txt");
        var id = File.Exists(file) ? File.ReadAllText(file).Trim() : "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9]{6,20}$")) { Debug.Log("[games] no Play Games project id: Play Games is off in this build"); return; }
        var values = Path.Combine(path, "src", "main", "res", "values");
        Directory.CreateDirectory(values);
        File.WriteAllText(Path.Combine(values, "games_ids.xml"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n  <string name=\"game_services_project_id\" translatable=\"false\">" + id + "</string>\n</resources>\n");
        var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        var m = File.ReadAllText(manifest);
        if (!m.Contains("com.google.android.gms.games.APP_ID"))
        {
            int at = m.LastIndexOf("</application>", System.StringComparison.Ordinal);
            if (at < 0) { Debug.LogWarning("[games] the manifest has no <application>"); return; }
            m = m.Insert(at, "    <meta-data android:name=\"com.google.android.gms.games.APP_ID\" android:value=\"@string/game_services_project_id\" />\n  ");
            File.WriteAllText(manifest, m);
        }
        Debug.Log("[games] Play Games is on (project " + id + ")");
    }

    static void Copy(string from, string to)
    {
        if (!Directory.Exists(from)) return;
        foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var dst = Path.Combine(to, f.Substring(from.Length + 1));
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(f, dst, true);
        }
    }
}
