// One connection to the game server (the same server and messages as the web/Capacitor app),
// reconnecting with backoff and re-sending the saved session token. Messages are handed to the
// main thread from Pump(), called every frame.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// In the web version (WebGL) the browser's own WebSocket carries the connection (Plugins/WebGL/BBSocket.jslib):
// browsers have no threads or .NET sockets, so it is polled from Pump() instead of read on a task.
public class Net
{
    public const string ServerUrl = "wss://qimma-labs.com/boardbattles/ws";
    const string TokenKey = "ahwa.token";

    readonly string url;
    readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
    readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    readonly CancellationTokenSource stop = new CancellationTokenSource();
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void BBSock_Open(string url);
    [DllImport("__Internal")] static extern int BBSock_State();
    [DllImport("__Internal")] static extern void BBSock_Send(string text);
    [DllImport("__Internal")] static extern void BBSock_Close();
    [DllImport("__Internal")] static extern string BBSock_Next();
    [DllImport("__Internal")] static extern string BBWeb_Base();
    bool webStarted, webStopped, webWaitingResume;
    float webRetryAt = -1; int webBackoff = 1000;
    public bool Open => BBSock_State() == 1;
#else
    ClientWebSocket ws;
    /// The socket is open right now (Online can lag a moment behind a dropped connection).
    public bool Open { get { var w = ws; return w != null && w.State == WebSocketState.Open; } }
#endif
    float lastPing;

    public bool Online { get; private set; }
    public event Action<Dictionary<string, object>> OnMessage;
    public event Action<bool> OnOnline;

    public Net(string url = null) { this.url = url ?? Arg("-server") ?? PageServer() ?? ServerUrl; }

    /// The web version talks to the server it was loaded from (https://host/boardbattles/ → wss://host/boardbattles/ws).
    static string PageServer()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { var b = BBWeb_Base(); if (!string.IsNullOrEmpty(b)) return (b.StartsWith("https://") ? "wss://" + b.Substring(8) : "ws://" + b.Substring(7)) + "/ws"; }
        catch { }
#endif
        return null;
    }

    /// The same server over https (sign-in pages): wss://host/ws → https://host
    public string HttpBase
    {
        get
        {
            var u = url.StartsWith("wss://") ? "https://" + url.Substring(6) : url.StartsWith("ws://") ? "http://" + url.Substring(5) : url;
            return u.EndsWith("/ws") ? u.Substring(0, u.Length - 3) : u.TrimEnd('/');
        }
    }

    /// A command-line value ("-server ws://…" points the test build at a local server).
    public static string Arg(string name)
    {
        var a = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }

    public string Token
    {
        get => PlayerPrefs.GetString(TokenKey, "");
        set { PlayerPrefs.SetString(TokenKey, value ?? ""); PlayerPrefs.Save(); }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    public void Start() { webStarted = true; webRetryAt = 0; }
    public void Stop() { webStopped = true; try { BBSock_Close(); } catch { } }
    public void Resume() { webWaitingResume = false; if (BBSock_State() != 1) webRetryAt = 0; }
    public event Action OnReplaced;
    public void Send(Dictionary<string, object> m) { if (BBSock_State() == 1) BBSock_Send(Json.Write(m)); }

    /// The browser's socket: hand on what arrived, and reconnect with backoff when it closes.
    void WebTick()
    {
        if (!webStarted || webStopped) return;
        string s;
        while ((s = BBSock_Next()) != null && s.Length > 0)
        {
            if (s == "\u0001open") { webBackoff = 1000; inbox.Enqueue(s); continue; }
            if (s.StartsWith("\u0001closed"))
            {
                inbox.Enqueue("\u0001closed");
                // the same account opened elsewhere: wait for the player to come back to this tab
                if (s == "\u0001closed:4001") { webWaitingResume = true; inbox.Enqueue("\u0001replaced"); }
                else { webRetryAt = Time.unscaledTime + webBackoff / 1000f; webBackoff = Math.Min(webBackoff * 2, 15000); }
                continue;
            }
            inbox.Enqueue(s);
        }
        if (!webWaitingResume && webRetryAt >= 0 && Time.unscaledTime >= webRetryAt && BBSock_State() != 1 && BBSock_State() != 0)
        {
            webRetryAt = -1;
            BBSock_Open(url);
        }
        else if (webRetryAt == 0 && BBSock_State() == 3) { webRetryAt = -1; BBSock_Open(url); }
    }
#else
    public void Start() { _ = Run(); }
    public void Stop() { stop.Cancel(); try { ws?.Abort(); } catch { } }

    /// Back from the background: phones often drop the connection silently, so start a fresh one at once
    /// (the server puts you back at your table).
    CancellationTokenSource wake = new CancellationTokenSource();
    public void Resume()
    {
        try { if (ws == null || ws.State != WebSocketState.Open) { wake.Cancel(); wake = new CancellationTokenSource(); } else ws.Abort(); } catch { }
        quickRetry = true;
    }
    volatile bool quickRetry, replaced;
    /// The account was opened on another phone (this one stays offline until the player comes back to it).
    public event Action OnReplaced;

    async Task Run()
    {
        int backoff = 1000;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                await ws.ConnectAsync(new Uri(url), stop.Token);
                inbox.Enqueue("\u0001open");
                backoff = 1000;
                var buf = new byte[16384];
                var msg = new List<byte>();
                while (ws.State == WebSocketState.Open && !stop.IsCancellationRequested)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), stop.Token);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        // the same account opened on another phone: don't fight it for the connection
                        if ((int?)ws.CloseStatus == 4001) replaced = true;
                        break;
                    }
                    for (int i = 0; i < r.Count; i++) msg.Add(buf[i]);
                    if (!r.EndOfMessage) continue;
                    inbox.Enqueue(Encoding.UTF8.GetString(msg.ToArray()));
                    msg.Clear();
                }
            }
            catch (Exception e)
            {
                if (!stop.IsCancellationRequested) inbox.Enqueue("\u0001log " + e.GetType().Name + ": " + e.Message + (e.InnerException != null ? " / " + e.InnerException.Message : ""));
            }
            inbox.Enqueue("\u0001closed");
            if (replaced)
            {
                // wait until the player comes back to this phone (Resume) before taking the account back
                inbox.Enqueue("\u0001replaced");
                try { using (var both = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, wake.Token)) await Task.Delay(Timeout.Infinite, both.Token); } catch { }
                replaced = false; backoff = 300;
                continue;
            }
            if (quickRetry) { quickRetry = false; backoff = 300; }
            try { using (var both = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, wake.Token)) await Task.Delay(backoff, both.Token); } catch { }
            backoff = Math.Min(backoff * 2, 15000);
        }
    }

    public void Send(Dictionary<string, object> m)
    {
        var w = ws;
        if (w == null || w.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(Json.Write(m));
        _ = SendAsync(w, bytes);
    }

    async Task SendAsync(ClientWebSocket w, byte[] bytes)
    {
        await sendLock.WaitAsync();
        try { await w.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, stop.Token); }
        catch (Exception e) { Debug.LogWarning("[net] send failed: " + e.Message); }
        finally { sendLock.Release(); }
    }
#endif

    /// Main thread: hand over what arrived.
    public void Pump()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebTick();
#endif
        while (inbox.TryDequeue(out var s))
        {
            if (s.Length > 0 && s[0] == '\u0001')
            {
                if (s == "\u0001open")
                {
                    var hello = new Dictionary<string, object> { ["t"] = "hello", ["v"] = 2, ["build"] = Updater.Build };
                    var country = Menus.DeviceCountry(); if (country != null) hello["country"] = country;   // for the country ranking
                    if (!string.IsNullOrEmpty(Token)) hello["token"] = Token;
                    Send(hello);
                    Debug.Log("[net] connected to " + url);
                }
                else if (s == "\u0001closed") { if (Online) { Online = false; OnOnline?.Invoke(false); } }
                else if (s == "\u0001replaced") OnReplaced?.Invoke();
                else Debug.Log("[net] " + s.Substring(1));
                continue;
            }
            Dictionary<string, object> m;
            try { m = Json.Parse(s) as Dictionary<string, object>; }
            catch (Exception e) { Debug.LogWarning("[net] bad message: " + e.Message); continue; }
            if (m == null) continue;
            if (m.Str("t") == "welcome")
            {
                Token = m.Str("token");
                if (!Online) { Online = true; OnOnline?.Invoke(true); }
            }
            OnMessage?.Invoke(m);
        }
        // keep the connection warm
        if (Online && Time.unscaledTime - lastPing > 25f)
        {
            lastPing = Time.unscaledTime;
            Send(new Dictionary<string, object> { ["t"] = "ping", ["c"] = (double)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds });
        }
    }
}
