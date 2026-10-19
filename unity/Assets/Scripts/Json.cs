// A small JSON reader and writer for the game server's messages.
// Objects become Dictionary<string, object>, arrays List<object>, numbers double, plus string, bool and null.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class Json
{
    public static object Parse(string s)
    {
        int i = 0;
        var v = Value(s, ref i);
        return v;
    }

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length) throw new FormatException("json: end");
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>();
            i++; Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                var k = Str(s, ref i);
                Ws(s, ref i);
                if (s[i] != ':') throw new FormatException("json: colon");
                i++;
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("json: object");
            }
        }
        if (c == '[')
        {
            var l = new List<object>();
            i++; Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new FormatException("json: array");
            }
        }
        if (c == '"') return Str(s, ref i);
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int st = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
    }

    static string Str(string s, ref int i)
    {
        if (s[i] != '"') throw new FormatException("json: string");
        i++;
        var sb = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
        throw new FormatException("json: unterminated string");
    }

    public static string Write(object v)
    {
        var sb = new StringBuilder();
        W(sb, v);
        return sb.ToString();
    }

    static void W(StringBuilder sb, object v)
    {
        switch (v)
        {
            case null: sb.Append("null"); break;
            case string s: Quote(sb, s); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case IDictionary d:
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in d)
                {
                    if (e.Value == null) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    Quote(sb, e.Key.ToString());
                    sb.Append(':');
                    W(sb, e.Value);
                }
                sb.Append('}');
                break;
            case IList l:
                sb.Append('[');
                for (int k = 0; k < l.Count; k++) { if (k > 0) sb.Append(','); W(sb, l[k]); }
                sb.Append(']');
                break;
            case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
            case double dd: sb.Append(dd.ToString("R", CultureInfo.InvariantCulture)); break;
            case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case long n2: sb.Append(n2.ToString(CultureInfo.InvariantCulture)); break;
            default: Quote(sb, v.ToString()); break;
        }
    }

    static void Quote(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    // handy readers for a parsed object
    public static Dictionary<string, object> Obj(this object o, string k) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
    public static string Str(this object o, string k, string def = "") => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) && v is string s ? s : def;
    public static double Num(this object o, string k, double def = 0) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) && v is double d ? d : def;
    public static bool Bool(this object o, string k) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) && v is bool b && b;
    public static List<object> Arr(this object o, string k) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v as List<object> : null;
}
