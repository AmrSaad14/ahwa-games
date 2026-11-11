// Keeps a label's text inside its box, on every screen size and in both languages:
//  - too long for one line: a little smaller first, then more lines when the box is tall enough for them
//    (never cutting a word in two);
//  - still too big: the letters get smaller, down to 60% of their size;
//  - Arabic is broken into lines here, word by word from its first word, since Unity's own wrapping would
//    start the top line with the sentence's end (the text is stored right to left for its drawing).
// It works only when the text, its size or its box changes (no work in quiet frames).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FitText : MonoBehaviour
{
    Text t;
    RectTransform rt;
    string src, shown;
    int baseSize, setSize = -1;
    HorizontalWrapMode mode;
    bool touched, busy;
    Vector2 doneSize;
    static readonly List<string> words = new List<string>();
    static readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

    void Awake()
    {
        t = GetComponent<Text>();
        rt = (RectTransform)transform;
        if (t != null) { t.RegisterDirtyLayoutCallback(Wake); t.RegisterDirtyVerticesCallback(Wake); }
    }
    void OnDestroy() { if (t != null) { t.UnregisterDirtyLayoutCallback(Wake); t.UnregisterDirtyVerticesCallback(Wake); } }
    void Wake() { if (!busy) enabled = true; }
    void OnRectTransformDimensionsChange() { if (!busy) enabled = true; }

    void LateUpdate()
    {
        if (t == null || t.font == null || t.resizeTextForBestFit) { enabled = false; return; }
        var size = rt.rect.size;
        string cur = t.text;
        bool sameText = ReferenceEquals(cur, shown);
        if (sameText && size == doneSize && t.fontSize == setSize) { enabled = false; return; }
        busy = true;
        if (!sameText) src = cur;                          // new words from the game
        if (t.fontSize != setSize) baseSize = t.fontSize;  // a new size set by the game: the most it may be
        if (touched) t.horizontalOverflow = mode; else mode = t.horizontalOverflow;
        touched = false;
        doneSize = size;
        t.fontSize = baseSize;
        string outText = src;
        if (size.x >= 4 && !string.IsNullOrEmpty(src)) outText = Fit(size);
        if (!ReferenceEquals(t.text, outText)) t.text = outText;
        shown = t.text; setSize = t.fontSize;
        busy = false;
        enabled = false;
    }

    string Fit(Vector2 size)
    {
        int min = Mathf.Max(8, Mathf.RoundToInt(baseSize * 0.6f));
        bool wrap = mode == HorizontalWrapMode.Wrap;
        if (!wrap)
        {
            if (Width(src) <= size.x + 0.5f) return src;
            // a little smaller first: a short label (a tab, a button) stays on one line
            int soft = Mathf.Max(min, Mathf.RoundToInt(baseSize * 0.85f));
            for (int fs = baseSize; fs > soft && Width(src) > size.x + 0.5f; ) t.fontSize = --fs;
            if (Width(src) <= size.x + 0.5f) return src;
            t.fontSize = baseSize;
            // room for two lines (and no lines of its own): let it wrap
            if (size.y >= baseSize * 2.3f && src.IndexOf('\n') < 0) wrap = true;
            else
            {
                for (int fs = baseSize; fs > min && Width(src) > size.x + 0.5f; ) t.fontSize = --fs;
                return src;
            }
        }
        if (size.y < 4) return src;
        bool arabic = Arabic.IsVisual(src);
        // Arabic: our own lines (no wrapping by Unity); the rest: Unity's wrapping
        var want = arabic ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
        if (t.horizontalOverflow != want) { t.horizontalOverflow = want; touched = true; }
        string text = src;
        for (int fs = baseSize; ; fs--)
        {
            t.fontSize = fs;
            text = arabic ? WrapRtl(src, size.x) : src;
            // a word is never cut in two ("Backgamm / on"): smaller letters until the longest word fits a line
            if (fs <= min || (Height(text, size.x) <= size.y + 0.5f && LongestWord(src) <= size.x + 0.5f)) break;
        }
        return text;
    }

    float LongestWord(string s)
    {
        float m = 0;
        foreach (var w in s.Split(' ', '\n')) if (w.Length > 0) m = Mathf.Max(m, Width(w));
        return m;
    }

    float Width(string s) => t.cachedTextGeneratorForLayout.GetPreferredWidth(s, t.GetGenerationSettings(Vector2.zero)) / t.pixelsPerUnit;
    float Height(string s, float w) => t.cachedTextGeneratorForLayout.GetPreferredHeight(s, t.GetGenerationSettings(new Vector2(w, 0))) / t.pixelsPerUnit;

    /// Lines for text already in display order (right to left): the top line takes the words from the right end.
    string WrapRtl(string visual, float maxW)
    {
        var paras = visual.Split('\n');
        var lines = new List<string>();
        foreach (var p in paras)
        {
            words.Clear(); words.AddRange(p.Split(' '));
            int end = words.Count;
            if (end == 0) { lines.Add(""); continue; }
            while (end > 0)
            {
                int start = end - 1;
                while (start > 0 && Width(Join(start - 1, end)) <= maxW) start--;
                lines.Add(Join(start, end));
                end = start;
            }
        }
        return string.Join("\n", lines);
    }

    static string Join(int a, int b)
    {
        sb.Clear();
        for (int i = a; i < b; i++) { if (i > a) sb.Append(' '); sb.Append(words[i]); }
        return sb.ToString();
    }
}
