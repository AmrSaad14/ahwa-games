// The café's own sound: a crowd of people talking, tea glasses, dominoes and dice (Resources/Audio/CafeCrowd,
// made by tools/audio/crowd.py), looping. Full on the opening and home screens; faded right down at a game
// table so the players (and voice chat) are heard. Off with the sound setting.
using UnityEngine;

public class Ambience : MonoBehaviour
{
    public static Ambience I;
    AudioSource src;
    float target = 0.6f;

    public static void Begin()
    {
        if (I != null) return;
        var go = new GameObject("Ambience"); DontDestroyOnLoad(go);
        I = go.AddComponent<Ambience>();
    }

    void Awake()
    {
        var clip = Resources.Load<AudioClip>("Audio/CafeCrowd");
        if (clip == null) { Debug.LogWarning("[ambience] no café sound"); enabled = false; return; }
        src = gameObject.AddComponent<AudioSource>();
        src.clip = clip; src.loop = true; src.spatialBlend = 0; src.volume = 0; src.playOnAwake = false;
        src.time = Random.Range(0f, clip.length * 0.8f);   // not always the same moment
        src.Play();
    }

    /// The home screen (true) or a game table (false).
    public static void Home(bool home) { if (I != null) I.target = home ? 0.6f : 0.12f; }

    void Update()
    {
        if (src == null) return;
        float want = Sfx.SoundOn && !App.Recording ? target : 0;
        src.volume = Mathf.MoveTowards(src.volume, want, Time.unscaledDeltaTime * 0.35f);
    }
}
