// Players' photos: the picture from their Google account (Apple and guests have none: their character's face
// stays). Downloaded once per address and kept for the session; shown cropped to fill the frame it goes in.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public static class Photo
{
    static readonly Dictionary<string, Texture2D> got = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, List<RawImage>> waiting = new Dictionary<string, List<RawImage>>();

    /// Puts the photo at `url` into `img` when it's ready (straight away if already downloaded). Empty: nothing.
    public static void Into(RawImage img, string url)
    {
        if (img == null || string.IsNullOrEmpty(url) || !url.StartsWith("https://")) return;
        url = Sized(url);
        if (got.TryGetValue(url, out var tex)) { Show(img, tex); return; }
        if (waiting.TryGetValue(url, out var list)) { list.Add(img); return; }
        waiting[url] = new List<RawImage> { img };
        if (App.I != null) App.I.StartCoroutine(Load(url));
    }

    /// Google's addresses carry their size ("=s96-c"): ask for a sharper one.
    static string Sized(string url)
    {
        if (!url.Contains("googleusercontent.com")) return url;
        int i = url.LastIndexOf('=');
        return (i > url.LastIndexOf('/') ? url.Substring(0, i) : url) + "=s256-c";
    }

    static IEnumerator Load(string url)
    {
        using (var r = UnityWebRequestTexture.GetTexture(url))
        {
            r.timeout = 15;
            yield return r.SendWebRequest();
            var list = waiting.TryGetValue(url, out var l) ? l : null;
            waiting.Remove(url);
            if (r.result != UnityWebRequest.Result.Success) { Debug.Log("[photo] " + r.error); yield break; }
            var tex = DownloadHandlerTexture.GetContent(r);
            tex.wrapMode = TextureWrapMode.Clamp;
            got[url] = tex;
            if (list != null) foreach (var img in list) Show(img, tex);
        }
    }

    /// Fill the frame without stretching: crop the middle of the photo to the frame's shape.
    static void Show(RawImage img, Texture2D tex)
    {
        if (img == null) return;
        img.texture = tex; img.color = Color.white;
        var size = img.rectTransform.rect.size;
        float frame = size.y > 0 ? size.x / size.y : 1f, pic = (float)tex.width / Mathf.Max(1, tex.height);
        if (pic > frame) { float w = frame / pic; img.uvRect = new Rect((1 - w) / 2, 0, w, 1); }
        else { float h = pic / frame; img.uvRect = new Rect(0, (1 - h) / 2, 1, h); }
    }
}
