// Light café music (Resources/Audio/CafeMusic: oud and qanun in maqam Hijaz over a soft frame drum, made by
// tools/audio/music.py), heard from the old radio on the counter (CafeRadio, tools/audio/radio.py), looping. Its own
// switch in Settings, apart from the sound effects. Around it the café (people talking, tea glasses) and the street
// outside the door (CafeStreet, tools/audio/street.py): busier at night, calmer in the morning, silent at a table.
using UnityEngine;

public class Music : MonoBehaviour
{
    public static Music I;
    public static bool On { get => PlayerPrefs.GetInt("music", 1) == 1; set { PlayerPrefs.SetInt("music", value ? 1 : 0); PlayerPrefs.Save(); } }
    /// The café around you: people talking at the other tables, spoons in tea glasses (tools/audio/ambience.py).
    public static bool CafeOn { get => PlayerPrefs.GetInt("ambience", 1) == 1; set { PlayerPrefs.SetInt("ambience", value ? 1 : 0); PlayerPrefs.Save(); } }
    AudioSource src, amb, street;
    float target = 0.3f, ambTarget = 0.26f, streetTarget = 0.12f;

    public static void Begin()
    {
        if (I != null) return;
        var go = new GameObject("Music"); DontDestroyOnLoad(go);
        I = go.AddComponent<Music>();
    }

    void Awake()
    {
        var clip = Resources.Load<AudioClip>("Audio/CafeRadio") ?? Resources.Load<AudioClip>("Audio/CafeMusic");
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
        var sc = Resources.Load<AudioClip>("Audio/CafeStreet");
        if (sc != null)
        {
            street = gameObject.AddComponent<AudioSource>();
            street.clip = sc; street.loop = true; street.spatialBlend = 0; street.volume = 0; street.playOnAwake = false;
            street.time = Random.Range(0f, sc.length * 0.9f);
            street.Play();
        }
    }

    /// How lively the café is at this hour in Cairo: quiet in the morning, fuller after sunset, busiest at night.
    static float Busy()
    {
        float h = Cafe.CairoHour();
        if (h >= 5 && h < 11) return 0.6f;
        if (h >= 11 && h < 17) return 0.8f;
        if (h >= 17 && h < 20) return 1f;
        return 1.15f;
    }

    /// The home screen (true) or a game table (false).
    public static void Home(bool home) { if (I != null) { I.target = home ? 0.3f : 0; I.ambTarget = home ? 0.26f : 0; I.streetTarget = home ? 0.12f : 0; } }

    void Update()
    {
        // quieter while you're in voice chat, so voices are clear
        float duck = Voice.I != null && Voice.I.Joined ? 0.35f : 1f;
        if (src != null) src.volume = Mathf.MoveTowards(src.volume, On && !App.Recording ? target * duck : 0, Time.unscaledDeltaTime * 0.3f);
        float busy = Busy();
        if (amb != null) amb.volume = Mathf.MoveTowards(amb.volume, CafeOn && !App.Recording ? ambTarget * busy * duck : 0, Time.unscaledDeltaTime * 0.25f);
        if (street != null) street.volume = Mathf.MoveTowards(street.volume, CafeOn && !App.Recording ? streetTarget * busy * duck : 0, Time.unscaledDeltaTime * 0.2f);
    }
}
