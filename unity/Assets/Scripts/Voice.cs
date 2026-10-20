// Table voice chat, like the web game's (client/src/voice.ts): everyone at the table who turns their mic on
// gets a direct audio connection (WebRTC) to every other player in voice — at most three, so no media
// server is needed. The game server only passes the offers and answers along ("rtc" messages), and gives
// the relay addresses (TURN) for phones that can't reach each other directly.
// The lower seat number starts each connection; the whole offer goes in one message (no trickle).
// Built only when Unity's WebRTC package is installed (BuildScript sets BB_VOICE); otherwise a stub.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if BB_VOICE
using Unity.WebRTC;
#endif

public class Voice : MonoBehaviour
{
    public static Voice I;
    public bool Joined { get; private set; }
    public bool MicMuted { get; private set; }
    public List<Dictionary<string, object>> ice = new List<Dictionary<string, object>>();
    App app;
    string tableId = "";
    int you = -1;

    public static bool Supported =>
#if BB_VOICE
        Microphone.devices != null && Microphone.devices.Length > 0;
#else
        false;
#endif

    public void Init(App a) { app = a; I = this; }

#if BB_VOICE
    class Peer { public RTCPeerConnection pc; public AudioSource audio; public bool initiator; }
    readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
    AudioStreamTrack micTrack;
    AudioClip micClip;
    string micDevice;
    int micPos;
    float[] buf = new float[48000];
    bool started;

    void Start() { StartCoroutine(WebRTC.Update()); started = true; }

    /// Turns the mic on and joins the table's voice chat (asks for the microphone first).
    public IEnumerator Join()
    {
        if (Joined) yield break;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            float t0 = Time.realtimeSinceStartup;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone) && Time.realtimeSinceStartup - t0 < 20) yield return null;
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) { app.Toast(Loc.T("اسمح بالميكروفون عشان تتكلم")); yield break; }
        }
#endif
        if (Microphone.devices.Length == 0) { app.Toast(Loc.T("اسمح بالميكروفون عشان تتكلم")); yield break; }
        micDevice = Microphone.devices[0];
        micClip = Microphone.Start(micDevice, true, 1, 48000);
        micPos = 0;
        micTrack = new AudioStreamTrack();
        Joined = true; MicMuted = false;
        app.Send(new Dictionary<string, object> { ["t"] = "voice", ["on"] = true });
        app.Toast(Loc.T("الصوت شغال"));
    }

    public void Leave()
    {
        if (Joined) app.Send(new Dictionary<string, object> { ["t"] = "voice", ["on"] = false });
        Joined = false;
        CloseAll();
        if (micDevice != null) Microphone.End(micDevice);
        micTrack?.Dispose(); micTrack = null; micClip = null;
    }

    public void SetMicMuted(bool m) { MicMuted = m; if (micTrack != null) micTrack.Enabled = !m; }

    void Update()
    {
        // the microphone's new samples go to the outgoing audio track
        if (!Joined || micClip == null || micTrack == null) return;
        int pos = Microphone.GetPosition(micDevice);
        if (pos < 0 || pos == micPos) return;
        int n = pos > micPos ? pos - micPos : micClip.samples - micPos + pos;
        if (n > buf.Length) n = buf.Length;
        var chunk = new float[n];
        if (pos > micPos) micClip.GetData(chunk, micPos);
        else
        {
            var a = new float[micClip.samples - micPos]; micClip.GetData(a, micPos);
            var b = new float[pos]; if (pos > 0) micClip.GetData(b, 0);
            a.CopyTo(chunk, 0); b.CopyTo(chunk, a.Length);
        }
        micPos = pos;
        if (MicMuted) System.Array.Clear(chunk, 0, chunk.Length);
        micTrack.SetData(chunk, 1, 48000);
    }

    /// Keep connections in step with who is in voice at the table.
    public void Sync(TableView v)
    {
        if (v == null) { CloseAll(); tableId = ""; return; }
        if (v.id != tableId) { CloseAll(); tableId = v.id; }
        you = v.you;
        if (!Joined || v.you < 0) return;
        var want = new HashSet<int>();
        for (int i = 0; i < v.seats.Count; i++) { var s = v.seats[i]; if (s != null && i != v.you && s.mic && s.online && !s.bot) want.Add(i); }
        foreach (var seat in new List<int>(peers.Keys)) if (!want.Contains(seat)) Close(seat);
        foreach (var seat in want) if (!peers.ContainsKey(seat) && v.you < seat) StartCoroutine(Call(seat));
    }

    public void OnSignal(int from, Dictionary<string, object> data)
    {
        if (!Joined || data == null || data.Str("table") != tableId) return;
        StartCoroutine(Signal(from, data.Str("type"), data.Str("sdp")));
    }

    IEnumerator Signal(int from, string type, string sdp)
    {
        if (type == "offer")
        {
            Close(from);
            var p = MakePeer(from, false);
            var remote = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
            var op = p.pc.SetRemoteDescription(ref remote); yield return op;
            if (op.IsError) yield break;
            var ans = p.pc.CreateAnswer(); yield return ans;
            if (ans.IsError) yield break;
            var desc = ans.Desc;
            var set = p.pc.SetLocalDescription(ref desc); yield return set;
            yield return Gathered(p.pc);
            if (peers.TryGetValue(from, out var still) && still == p)
                app.Send(new Dictionary<string, object> { ["t"] = "rtc", ["to"] = (double)from, ["data"] = new Dictionary<string, object> { ["type"] = "answer", ["sdp"] = p.pc.LocalDescription.sdp, ["table"] = tableId } });
        }
        else if (peers.TryGetValue(from, out var p) && p.pc.SignalingState == RTCSignalingState.HaveLocalOffer)
        {
            var remote = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = sdp };
            yield return p.pc.SetRemoteDescription(ref remote);
        }
    }

    IEnumerator Call(int seat)
    {
        var p = MakePeer(seat, true);
        var offer = p.pc.CreateOffer(); yield return offer;
        if (offer.IsError) yield break;
        var desc = offer.Desc;
        yield return p.pc.SetLocalDescription(ref desc);
        yield return Gathered(p.pc);
        if (peers.TryGetValue(seat, out var still) && still == p)
            app.Send(new Dictionary<string, object> { ["t"] = "rtc", ["to"] = (double)seat, ["data"] = new Dictionary<string, object> { ["type"] = "offer", ["sdp"] = p.pc.LocalDescription.sdp, ["table"] = tableId } });
    }

    /// Wait until the connection has gathered its network candidates (or 2.5 s), so one message carries everything.
    static IEnumerator Gathered(RTCPeerConnection pc)
    {
        float t0 = Time.realtimeSinceStartup;
        while (pc.GatheringState != RTCIceGatheringState.Complete && Time.realtimeSinceStartup - t0 < 2.5f) yield return null;
    }

    Peer MakePeer(int seat, bool initiator)
    {
        var config = new RTCConfiguration { iceServers = IceServers() };
        var p = new Peer { pc = new RTCPeerConnection(ref config), initiator = initiator };
        peers[seat] = p;
        if (micTrack != null) p.pc.AddTrack(micTrack);
        p.pc.OnTrack = e =>
        {
            if (!(e.Track is AudioStreamTrack at)) return;
            if (p.audio == null) { var go = new GameObject("Voice " + seat); go.transform.SetParent(transform, false); p.audio = go.AddComponent<AudioSource>(); }
            p.audio.SetTrack(at);
            p.audio.loop = true;
            p.audio.Play();
        };
        p.pc.OnConnectionStateChange = state =>
        {
            if (state == RTCPeerConnectionState.Failed && p.initiator && peers.TryGetValue(seat, out var still) && still == p)
                StartCoroutine(Retry(seat, p));
        };
        return p;
    }

    IEnumerator Retry(int seat, Peer p)
    {
        yield return new WaitForSeconds(2f);
        if (peers.TryGetValue(seat, out var still) && still == p) { Close(seat); if (Joined) StartCoroutine(Call(seat)); }
    }

    RTCIceServer[] IceServers()
    {
        var list = new List<RTCIceServer>();
        foreach (var s in ice)
        {
            var urls = new List<string>();
            if (s.TryGetValue("urls", out var u)) { if (u is string one) urls.Add(one); else if (u is List<object> many) foreach (var x in many) if (x is string xs) urls.Add(xs); }
            if (urls.Count == 0) continue;
            list.Add(new RTCIceServer { urls = urls.ToArray(), username = s.Str("username"), credential = s.Str("credential") });
        }
        if (list.Count == 0) list.Add(new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } });
        return list.ToArray();
    }

    void Close(int seat)
    {
        if (!peers.TryGetValue(seat, out var p)) return;
        p.pc.OnTrack = null; p.pc.OnConnectionStateChange = null;
        p.pc.Close(); p.pc.Dispose();
        if (p.audio != null) Destroy(p.audio.gameObject);
        peers.Remove(seat);
    }
    void CloseAll() { foreach (var s in new List<int>(peers.Keys)) Close(s); }
    void OnDestroy() { if (started) Leave(); }
#else
    public IEnumerator Join() { app?.Toast(Loc.T("اسمح بالميكروفون عشان تتكلم")); yield break; }
    public void Leave() { Joined = false; }
    public void SetMicMuted(bool m) { MicMuted = m; }
    public void Sync(TableView v) { }
    public void OnSignal(int from, Dictionary<string, object> data) { }
#endif
}
