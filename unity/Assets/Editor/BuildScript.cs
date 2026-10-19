// Builds the game from the command line (GitHub Actions):
//   unity-editor -batchmode -quit -projectPath unity -executeMethod BuildScript.BuildAll
// Settings are applied here in code, so the project needs no hand-made settings files.
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildScript
{
    const string Scene = "Assets/Scenes/Main.unity";

    static void Common()
    {
        // one empty scene: the game builds itself at start (see App.cs)
        if (!File.Exists(Scene))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(s, Scene);
        }
        PlayerSettings.companyName = "Ahwa";
        PlayerSettings.productName = "Kings Battle";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
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
        IncludeShaders("Legacy Shaders/Specular", "Legacy Shaders/Diffuse", "Legacy Shaders/Transparent/Diffuse", "Unlit/Texture", "Unlit/Color");
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
        return Build(BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone, "../build/linux/KingsBattle.x86_64");
    }

    static bool Android()
    {
        Common();
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ahwa.kingsbattle");
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.forceInternetPermission = true;
        EditorUserBuildSettings.buildAppBundle = false;
        return Build(BuildTarget.Android, BuildTargetGroup.Android, "../build/android/KingsBattle.apk");
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
