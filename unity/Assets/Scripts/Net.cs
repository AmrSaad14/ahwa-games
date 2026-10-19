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

public class Net
{
    public const string ServerUrl = "wss://kings-battle.onrender.com/ws";
    const string TokenKey = "ahwa.token";

    readonly string url;
    readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
    readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    readonly CancellationTokenSource stop = new CancellationTokenSource();
    ClientWebSocket ws;
    float lastPing;

    public bool Online { get; private set; }
    public event Action<Dictionary<string, object>> OnMessage;
    public event Action<bool> OnOnline;

    public Net(string url = ServerUrl) { this.url = url; }

    public string Token
    {
        get => PlayerPrefs.GetString(TokenKey, "");
        set { PlayerPrefs.SetString(TokenKey, value ?? ""); PlayerPrefs.Save(); }
    }

    public void Start() { _ = Run(); }
    public void Stop() { stop.Cancel(); try { ws?.Abort(); } catch { } }

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
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    for (int i = 0; i < r.Count; i++) msg.Add(buf[i]);
                    if (!r.EndOfMessage) continue;
                    inbox.Enqueue(Encoding.UTF8.GetString(msg.ToArray()));
                    msg.Clear();
                }
            }
            catch (Exception e)
            {
                if (!stop.IsCancellationRequested) inbox.Enqueue("\u0001log " + e.GetType().Name + ": " + e.Message);
            }
            inbox.Enqueue("\u0001closed");
            try { await Task.Delay(backoff, stop.Token); } catch { }
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

    /// Main thread: hand over what arrived.
    public void Pump()
    {
        while (inbox.TryDequeue(out var s))
        {
            if (s.Length > 0 && s[0] == '\u0001')
            {
                if (s == "\u0001open")
                {
                    var hello = new Dictionary<string, object> { ["t"] = "hello", ["v"] = 1 };
                    if (!string.IsNullOrEmpty(Token)) hello["token"] = Token;
                    Send(hello);
                    Debug.Log("[net] connected to " + url);
                }
                else if (s == "\u0001closed") { if (Online) { Online = false; OnOnline?.Invoke(false); } }
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
