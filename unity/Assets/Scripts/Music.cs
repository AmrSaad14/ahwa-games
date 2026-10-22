// Light café music (Resources/Audio/CafeMusic: oud and qanun in maqam Hijaz over a soft frame drum, made by
// tools/audio/music.py), looping. Its own switch in Settings, apart from the sound effects. Quieter at a table.
using UnityEngine;

public class Music : MonoBehaviour
{
    public static Music I;
    public static bool On { get => PlayerPrefs.GetInt("music", 1) == 1; set { PlayerPrefs.SetInt("music", value ? 1 : 0); PlayerPrefs.Save(); } }
    AudioSource src;
    float target = 0.32f;

    public static void Begin()
    {
        if (I != null) return;
        var go = new GameObject("Music"); DontDestroyOnLoad(go);
        I = go.AddComponent<Music>();
    }

    void Awake()
    {
        var clip = Resources.Load<AudioClip>("Audio/CafeMusic");
        if (clip == null) { enabled = false; return; }
        src = gameObject.AddComponent<AudioSource>();
        src.clip = clip; src.loop = true; src.spatialBlend = 0; src.volume = 0; src.playOnAwake = false;
        src.Play();
    }

    /// The home screen (true) or a game table (false).
    public static void Home(bool home) { if (I != null) I.target = home ? 0.32f : 0.14f; }

    void Update()
    {
        if (src == null) return;
        float want = On && !App.Recording ? target : 0;
        src.volume = Mathf.MoveTowards(src.volume, want, Time.unscaledDeltaTime * 0.3f);
    }
}
