// Builds the game from the command line (GitHub Actions):
//   unity-editor -batchmode -quit -projectPath unity -executeMethod BuildScript.BuildAll
// Settings are applied here in code, so the project needs no hand-made settings files.
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using KingsBattle.Forge;

public static class BuildScript
{
    const string Scene = "Assets/Scenes/Main.unity";

    static bool forged;

    static void Common()
    {
        // the café regulars: realistic humans made in Blender (Resources/People, see tools/characters);
        // the older generated ones (Editor/Characters) only when those are missing
        if (!forged)
        {
            forged = true;
            if (Directory.Exists("Assets/Resources/People") && Directory.GetFiles("Assets/Resources/People", "*.glb").Length > 0)
            {
                if (Directory.Exists("Assets/Resources/Characters")) AssetDatabase.DeleteAsset("Assets/Resources/Characters");
            }
            else CharacterForge.BuildAll();
        }
        // one empty scene: the game builds itself at start (see App.cs)
        if (!File.Exists(Scene))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(s, Scene);
        }
        PlayerSettings.companyName = "Ahwa";
        PlayerSettings.productName = "Board Battles";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        // the studio's logo from the very first moment (instead of a black screen while the engine loads);
        // the game's own animated splash (Scripts/Splash.cs) follows once the game starts.
        // The logo is drawn from design/studio/studio.html (render.mjs).
        const string splashLogo = "Assets/Splash/StudioLogo.png";
        AssetDatabase.ImportAsset(splashLogo, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(splashLogo) is TextureImporter imp && (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single))
        {
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            AssetDatabase.ImportAsset(splashLogo, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
        Sprite logoSprite = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(splashLogo)) if (o is Sprite sp) logoSprite = sp;
        if (logoSprite == null) Debug.Log("[build] splash logo: importer " + (AssetImporter.GetAtPath(splashLogo)?.GetType().Name ?? "none"));
        PlayerSettings.SplashScreen.show = logoSprite != null;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.backgroundColor = new Color32(0x0b, 0x0f, 0x17, 0xff);
        PlayerSettings.SplashScreen.overlayOpacity = 0;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
        PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
        if (logoSprite != null) PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2.2f, logoSprite) };
        Debug.Log("[build] splash logo " + (logoSprite != null ? "on" : "missing"));
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/UI/AppIcon.png");
        if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        QualitySettings.shadowDistance = 60;
        // shaders the game makes materials from in code (tiles, highlights) must be in every build
        IncludeShaders("Legacy Shaders/Specular", "Legacy Shaders/Diffuse", "Legacy Shaders/Transparent/Diffuse", "Unlit/Texture", "Unlit/Color", "KingsBattle/CafeLook");
        // the board games' Standard material: kept in the build through one plain material (only the variants it
        // needs), not the whole shader (thousands of variants and a very long build)
        Directory.CreateDirectory("Assets/Resources/Materials");
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Standard.mat") == null)
            AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")), "Assets/Resources/Materials/Standard.mat");
        // ad units (Scripts/Ads.cs): the Board Battles units in AdMob (ADMOB_* build settings override them)
        string Env(string k, string d) { var v = System.Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v.Trim(); }
        File.WriteAllText("Assets/Resources/AdsConfig.json", "{\"interstitial\":\"" + Env("ADMOB_INTERSTITIAL_ID", "ca-app-pub-3492998296780013/2334487395")
            + "\",\"rewarded\":\"" + Env("ADMOB_REWARDED_ID", "ca-app-pub-3492998296780013/1021405722") + "\"}");
        AssetDatabase.ImportAsset("Assets/Resources/AdsConfig.json");
        // voice chat is built when Unity's WebRTC package is installed (Scripts/Voice.cs)
        bool webrtc = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.webrtc") != null;
        foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.Standalone })
        {
            var defs = PlayerSettings.GetScriptingDefineSymbols(t);
            bool has = defs.Contains("BB_VOICE");
            if (webrtc && !has) PlayerSettings.SetScriptingDefineSymbols(t, string.IsNullOrEmpty(defs) ? "BB_VOICE" : defs + ";BB_VOICE");
        }
        Debug.Log("[build] voice chat " + (webrtc ? "on" : "off (no WebRTC package)"));
    }

    static void IncludeShaders(params string[] names)
    {
        var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (gs == null) { Debug.Log("[build] no GraphicsSettings asset"); return; }
        var so = new SerializedObject(gs);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        foreach (var n in names)
        {
            var sh = Shader.Find(n);
            if (sh == null) { Debug.Log("[build] shader not found: " + n); continue; }
            bool has = false;
            for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
            if (has) continue;
            arr.InsertArrayElementAtIndex(arr.arraySize);
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    public static void BuildLinux() { if (!Linux()) EditorApplication.Exit(1); }
    public static void BuildAndroid() { if (!Android()) EditorApplication.Exit(1); }

    static bool Linux()
    {
        Common();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        return Build(BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone, "../build/linux/BoardBattles.x86_64");
    }

    static bool Android()
    {
        Common();
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ahwa.boardbattles");
        // the classic activity, so the custom manifest (Plugins/Android) can add the sign-in link to it
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        // each build a higher version code (Google Play only accepts a higher one than the last upload)
        PlayerSettings.Android.bundleVersionCode = int.TryParse(System.Environment.GetEnvironmentVariable("BUILD_NUMBER"), out var bn) && bn > 0 ? bn : 1;
        PlayerSettings.bundleVersion = "1.0." + PlayerSettings.Android.bundleVersionCode;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.forceInternetPermission = true;
        bool signed = Sign();
        EditorUserBuildSettings.buildAppBundle = false;
        bool ok = Build(BuildTarget.Android, BuildTargetGroup.Android, "../build/android/BoardBattles.apk");
        // with the upload key: the app bundle (.aab) for Google Play too
        if (ok && signed)
        {
            EditorUserBuildSettings.buildAppBundle = true;
            if (!Build(BuildTarget.Android, BuildTargetGroup.Android, "../build/android/BoardBattles.aab")) Debug.Log("[build] the app bundle failed");
            EditorUserBuildSettings.buildAppBundle = false;
        }
        return ok;
    }

    /// Release signing with your upload key (GitHub secrets ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASS,
    /// ANDROID_KEY_ALIAS, ANDROID_KEY_PASS; the workflow writes the key to unity/release.keystore). Without them:
    /// the debug key, fine for testing but not for Google Play.
    static bool Sign()
    {
        string pass = System.Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS"), alias = System.Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS"),
               keyPass = System.Environment.GetEnvironmentVariable("ANDROID_KEY_PASS");
        string store = Path.GetFullPath("release.keystore");
        if (!File.Exists(store) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(alias))
        {
            PlayerSettings.Android.useCustomKeystore = false;
            Debug.Log("[build] signing: debug key (no upload key set)");
            return false;
        }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = store;
        PlayerSettings.Android.keystorePass = pass;
        PlayerSettings.Android.keyaliasName = alias;
        PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(keyPass) ? pass : keyPass;
        Debug.Log("[build] signing: the upload key");
        return true;
    }

    /// The desktop build (for the screenshots) and the phone build, in one editor session.
    public static void BuildAll()
    {
        bool desktop = Linux();          // (a failed desktop build still lets the phone build run)
        bool phone = Android();
        if (!phone) EditorApplication.Exit(1);
        if (!desktop) Debug.Log("[build] the desktop build failed (no screenshots this time)");
    }

    static bool Build(BuildTarget target, BuildTargetGroup group, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (EditorUserBuildSettings.activeBuildTarget != target) EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Scene }, target = target, locationPathName = path, options = BuildOptions.None });
        var sum = report.summary;
        Debug.Log($"[build] {target}: {sum.result}, {sum.totalErrors} errors, {sum.totalSize / 1048576f:0.0} MB, {sum.totalTime}");
        foreach (var step in report.steps)
            foreach (var m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception) Debug.Log("[build error] " + m.content);
        return sum.result == BuildResult.Succeeded;
    }
}
