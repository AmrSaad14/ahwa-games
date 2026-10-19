// Arabic for Unity's text: letters joined into their connected shapes (presentation forms) and the line put in
// display order (right to left, with numbers and Latin words kept left to right), since Unity's text draws
// characters one by one from left to right.
using System.Collections.Generic;
using System.Text;

public static class Arabic
{
    // isolated, final, initial, medial (0 = the letter has no such form: it does not join to the next letter)
    static readonly Dictionary<char, int[]> Forms = new Dictionary<char, int[]>
    {
        ['ء'] = new[] { 0xFE80, 0, 0, 0 },
        ['آ'] = new[] { 0xFE81, 0xFE82, 0, 0 },
        ['أ'] = new[] { 0xFE83, 0xFE84, 0, 0 },
        ['ؤ'] = new[] { 0xFE85, 0xFE86, 0, 0 },
        ['إ'] = new[] { 0xFE87, 0xFE88, 0, 0 },
        ['ئ'] = new[] { 0xFE89, 0xFE8A, 0xFE8B, 0xFE8C },
        ['ا'] = new[] { 0xFE8D, 0xFE8E, 0, 0 },
        ['ب'] = new[] { 0xFE8F, 0xFE90, 0xFE91, 0xFE92 },
        ['ة'] = new[] { 0xFE93, 0xFE94, 0, 0 },
        ['ت'] = new[] { 0xFE95, 0xFE96, 0xFE97, 0xFE98 },
        ['ث'] = new[] { 0xFE99, 0xFE9A, 0xFE9B, 0xFE9C },
        ['ج'] = new[] { 0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0 },
        ['ح'] = new[] { 0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4 },
        ['خ'] = new[] { 0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8 },
        ['د'] = new[] { 0xFEA9, 0xFEAA, 0, 0 },
        ['ذ'] = new[] { 0xFEAB, 0xFEAC, 0, 0 },
        ['ر'] = new[] { 0xFEAD, 0xFEAE, 0, 0 },
        ['ز'] = new[] { 0xFEAF, 0xFEB0, 0, 0 },
        ['س'] = new[] { 0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4 },
        ['ش'] = new[] { 0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8 },
        ['ص'] = new[] { 0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC },
        ['ض'] = new[] { 0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0 },
        ['ط'] = new[] { 0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4 },
        ['ظ'] = new[] { 0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8 },
        ['ع'] = new[] { 0xFEC9, 0xFECA, 0xFECB, 0xFECC },
        ['غ'] = new[] { 0xFECD, 0xFECE, 0xFECF, 0xFED0 },
        ['ف'] = new[] { 0xFED1, 0xFED2, 0xFED3, 0xFED4 },
        ['ق'] = new[] { 0xFED5, 0xFED6, 0xFED7, 0xFED8 },
        ['ك'] = new[] { 0xFED9, 0xFEDA, 0xFEDB, 0xFEDC },
        ['ل'] = new[] { 0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0 },
        ['م'] = new[] { 0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4 },
        ['ن'] = new[] { 0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8 },
        ['ه'] = new[] { 0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC },
        ['و'] = new[] { 0xFEED, 0xFEEE, 0, 0 },
        ['ى'] = new[] { 0xFEEF, 0xFEF0, 0, 0 },
        ['ي'] = new[] { 0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4 },
    };

    // lam followed by an alef becomes one ligature (isolated, final)
    static readonly Dictionary<char, int[]> LamAlef = new Dictionary<char, int[]>
    {
        ['آ'] = new[] { 0xFEF5, 0xFEF6 },
        ['أ'] = new[] { 0xFEF7, 0xFEF8 },
        ['إ'] = new[] { 0xFEF9, 0xFEFA },
        ['ا'] = new[] { 0xFEFB, 0xFEFC },
    };

    static bool IsMark(char c) => (c >= 'ً' && c <= 'ٟ') || c == 'ٰ';
    static bool IsLetter(char c) => Forms.ContainsKey(c) || c == 'ـ';
    static bool JoinsNext(char c) => c == 'ـ' || (Forms.TryGetValue(c, out var f) && f[2] != 0);
    public static bool HasArabic(string s) { foreach (var c in s) if (c >= '؀' && c <= 'ۿ') return true; return false; }

    /// Text ready for Unity's left-to-right drawing.
    public static string Fix(string s)
    {
        if (string.IsNullOrEmpty(s) || !HasArabic(s)) return s;
        return Visual(Shape(s));
    }

    static string Shape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (!IsLetter(c)) { sb.Append(c); continue; }
            // the letters around this one (marks don't break joining)
            int p = i - 1; while (p >= 0 && IsMark(s[p])) p--;
            int n = i + 1; while (n < s.Length && IsMark(s[n])) n++;
            bool prevJoins = p >= 0 && JoinsNext(s[p]);
            if (c == 'ل' && n < s.Length && LamAlef.TryGetValue(s[n], out var la))
            {
                sb.Append((char)(prevJoins ? la[1] : la[0]));
                i = n;
                continue;
            }
            if (c == 'ـ') { sb.Append(c); continue; }
            var f = Forms[c];
            bool nextJoins = f[2] != 0 && n < s.Length && IsLetter(s[n]);
            int form = prevJoins && nextJoins ? f[3] : prevJoins ? f[1] : nextJoins ? f[2] : f[0];
            if (form == 0) form = prevJoins && f[1] != 0 ? f[1] : f[0];
            sb.Append((char)form);
        }
        return sb.ToString();
    }

    static bool IsLtr(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '٠' && c <= '٩');

    // display order: the runs right to left, Latin words and numbers kept as they are
    static string Visual(string s)
    {
        var runs = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            int st = i;
            if (IsLtr(s[i]))
            {
                // a number or a Latin word, with the punctuation inside it (1,000  3.5  Kings Battle)
                while (i < s.Length && (IsLtr(s[i]) || ((s[i] == ',' || s[i] == '.' || s[i] == ':' || s[i] == ' ' || s[i] == '-' || s[i] == '/') && i + 1 < s.Length && IsLtr(s[i + 1])))) i++;
                runs.Add(s.Substring(st, i - st));
            }
            else
            {
                while (i < s.Length && !IsLtr(s[i])) i++;
                var arr = s.Substring(st, i - st).ToCharArray();
                System.Array.Reverse(arr);
                for (int k = 0; k < arr.Length; k++) arr[k] = Mirror(arr[k]);
                runs.Add(new string(arr));
            }
        }
        runs.Reverse();
        return string.Concat(runs);
    }

    static char Mirror(char c)
    {
        switch (c)
        {
            case '(': return ')';
            case ')': return '(';
            case '[': return ']';
            case ']': return '[';
            case '«': return '»';
            case '»': return '«';
            case '<': return '>';
            case '>': return '<';
            default: return c;
        }
    }
}
