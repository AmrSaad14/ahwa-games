// Light café music (Resources/Audio/CafeMusic: oud and qanun in maqam Hijaz over a soft frame drum, made by
// tools/audio/music.py), looping. Its own switch in Settings, apart from the sound effects. Quieter at a table.
using UnityEngine;

public class Music : MonoBehaviour
{
    public static Music I;
    public static bool On { get => PlayerPrefs.GetInt("music", 1) == 1; set { PlayerPrefs.SetInt("music", value ? 1 : 0); PlayerPrefs.Save(); } }
    /// The café around you: people talking at the other tables, spoons in tea glasses (tools/audio/ambience.py).
    public static bool CafeOn { get => PlayerPrefs.GetInt("ambience", 1) == 1; set { PlayerPrefs.SetInt("ambience", value ? 1 : 0); PlayerPrefs.Save(); } }
    AudioSource src, amb;
    float target = 0.32f, ambTarget = 0.26f;

    public static void Begin()
    {
        if (I != null) return;
        var go = new GameObject("Music"); DontDestroyOnLoad(go);
        I = go.AddComponent<Music>();
    }

    void Awake()
    {
        var clip = Resources.Load<AudioClip>("Audio/CafeMusic");
        if (clip != null)
        {
            src = gameObject.AddComponent<AudioSource>();
            src.clip = clip; src.loop = true; src.spatialBlend = 0; src.volume = 0; src.playOnAwake = false;
            src.Play();
        }
        var ac = Resources.Load<AudioClip>("Audio/CafeAmbience");
        if (ac != null)
        {
            amb = gameObject.AddComponent<AudioSource>();
            amb.clip = ac; amb.loop = true; amb.spatialBlend = 0; amb.volume = 0; amb.playOnAwake = false;
            amb.time = Random.Range(0f, ac.length * 0.9f);
            amb.Play();
        }
    }

    /// The home screen (true) or a game table (false).
    public static void Home(bool home) { if (I != null) { I.target = home ? 0.32f : 0.14f; I.ambTarget = home ? 0.26f : 0.16f; } }

    void Update()
    {
        if (src != null) src.volume = Mathf.MoveTowards(src.volume, On && !App.Recording ? target : 0, Time.unscaledDeltaTime * 0.3f);
        if (amb != null) amb.volume = Mathf.MoveTowards(amb.volume, CafeOn && !App.Recording ? ambTarget : 0, Time.unscaledDeltaTime * 0.25f);
    }
}
