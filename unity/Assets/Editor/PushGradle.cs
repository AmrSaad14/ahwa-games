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
            File.AppendAllText(gradle, "\n// push reminders (Editor/PushGradle.cs)\ndependencies {\n    implementation platform('com.google.firebase:firebase-bom:33.7.0')\n    implementation 'com.google.firebase:firebase-messaging'\n}\n");
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
