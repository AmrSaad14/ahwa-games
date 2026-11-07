using System.Collections.Generic;
using UnityEngine;

// Tells the server when the game closed by itself last time, and about script errors: the phone model, its
// graphics chip and API, its memory, the Android version and the screen it was on, so crashes on some phones
// can be found. A "running" mark is kept while the game is in the foreground; it's cleared when the game goes to
// the background or quits, so finding it still set at the next start means the last run ended in a crash.
public static class CrashReport
{
    const string Mark = "bb_running", Where = "bb_where", WhenKey = "bb_running_since";
    static readonly List<Dictionary<string, object>> queue = new List<Dictionary<string, object>>();
    static int errors;
    static string where = "start";

    /// At start: a crash last time? Then the mark goes up for this run.
    public static void Begin()
    {
        if (PlayerPrefs.GetInt(Mark, 0) == 1)
            Add("crash", "the game closed by itself while open (last screen: " + PlayerPrefs.GetString(Where, "?") + ", " + PlayerPrefs.GetString(WhenKey, "?") + ")");
        SetMark(true);
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type != LogType.Exception && type != LogType.Assert) return;
            if (errors++ >= 5) return;   // a few per run is enough
            Add("error", msg + "\n" + (stack.Length > 1200 ? stack.Substring(0, 1200) : stack));
        };
    }

    /// The screen the player is on (for the report).
    public static void At(string screen)
    {
        if (screen == where) return;
        where = screen;
        PlayerPrefs.SetString(Where, screen);
    }

    /// The game went to the background (false) or came back (true).
    public static void SetMark(bool running)
    {
        PlayerPrefs.SetInt(Mark, running ? 1 : 0);
        if (running) PlayerPrefs.SetString(WhenKey, System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC");
        PlayerPrefs.Save();
    }

    static void Add(string kind, string msg)
    {
        queue.Add(new Dictionary<string, object>
        {
            ["t"] = "crashReport", ["kind"] = kind, ["msg"] = msg, ["where"] = where,
            ["model"] = SystemInfo.deviceModel, ["gpu"] = SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceVersion,
            ["api"] = SystemInfo.graphicsDeviceType.ToString(), ["os"] = SystemInfo.operatingSystem,
            ["mem"] = SystemInfo.systemMemorySize, ["gmem"] = SystemInfo.graphicsMemorySize, ["ver"] = Application.version,
        });
        if (queue.Count > 8) queue.RemoveAt(0);
    }

    /// Sends what's waiting (called once connected).
    public static void Flush(App app)
    {
        foreach (var r in queue) app.Send(r);
        queue.Clear();
    }
}
