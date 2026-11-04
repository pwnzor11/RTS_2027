// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using System.Text.Json;

namespace Rts.Llm;

/// <summary>Turns a model's text into a <see cref="CommanderReply"/>. Lenient about wrapping, strict about ranges.
/// Never throws. Defaults for unknown values: posture 1, focus 2, target 255, mix 0, expand false, no focus cell.</summary>
public static class ReplyParser
{
    private const int MaxSay = 200;
    private const int MaxCell = 4095;

    private static readonly string[] Postures = { "turtle", "balanced", "aggressive", "allin" };
    private static readonly string[] Focuses = { "economy", "tech", "army", "air", "superweapon" };
    private static readonly string[] Mixes = { "auto", "infantry", "vehicles", "antiair", "antistructure" };

    /// <summary>Parses <paramref name="text"/>; any failure yields <c>new CommanderReply(null, null)</c>.</summary>
    public static CommanderReply Parse(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return new CommanderReply(null, null);
            int from = 0;
            while (true)
            {
                int start = text.IndexOf('{', from);
                if (start < 0) return new CommanderReply(null, null);
                int end = MatchEnd(text, start);
                if (end > start)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(text.AsMemory(start, end - start + 1));
                        if (doc.RootElement.ValueKind == JsonValueKind.Object) return FromRoot(doc.RootElement);
                    }
                    catch (JsonException) { }
                }
                from = start + 1;
            }
        }
        catch (Exception)
        {
            return new CommanderReply(null, null);
        }
    }

    private static int MatchEnd(string s, int start)
    {
        int depth = 0;
        bool inStr = false, esc = false;
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if (inStr)
            {
                if (esc) esc = false;
                else if (c == '\\') esc = true;
                else if (c == '"') inStr = false;
                continue;
            }
            if (c == '"') inStr = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static CommanderReply FromRoot(JsonElement root)
    {
        DirectiveDraft? draft = null;
        if (root.TryGetProperty("directive", out var d) && d.ValueKind == JsonValueKind.Object)
            draft = ReadDirective(d);
        string? say = null;
        if (root.TryGetProperty("say", out var s) && s.ValueKind == JsonValueKind.String)
            say = CleanSay(s.GetString());
        return new CommanderReply(draft, say);
    }

    private static DirectiveDraft ReadDirective(JsonElement d)
    {
        byte posture = (byte)Enum(d, "posture", Postures, 1, 3);
        byte focus = (byte)Enum(d, "focus", Focuses, 2, 4);
        byte mix = (byte)Enum(d, "mix", Mixes, 0, 4);
        byte target = 255;
        if (d.TryGetProperty("target", out var t) && TryInt(t, out long tv) && tv >= 0 && tv < 255) target = (byte)tv;
        bool expand = false;
        if (d.TryGetProperty("expand", out var e))
        {
            if (e.ValueKind == JsonValueKind.True) expand = true;
            else if (e.ValueKind == JsonValueKind.String)
            {
                string v = Norm(e.GetString());
                expand = v == "true" || v == "yes" || v == "1";
            }
            else if (TryInt(e, out long ev)) expand = ev > 0;
        }
        short fx = -1, fy = -1;
        if (d.TryGetProperty("focus_cell", out var fc) && fc.ValueKind == JsonValueKind.Array && fc.GetArrayLength() == 2
            && TryInt(fc[0], out long x) && TryInt(fc[1], out long y)
            && x >= 0 && y >= 0 && x <= MaxCell && y <= MaxCell)
        {
            fx = (short)x;
            fy = (short)y;
        }
        return new DirectiveDraft(posture, focus, target, mix, expand, fx, fy);
    }

    private static int Enum(JsonElement d, string name, string[] names, int dflt, int max)
    {
        if (!d.TryGetProperty(name, out var v)) return dflt;
        if (v.ValueKind == JsonValueKind.String)
        {
            string raw = v.GetString() ?? "";
            string n = Norm(raw);
            int idx = Array.IndexOf(names, n);
            if (idx >= 0) return idx;
            if (long.TryParse(raw.Trim(), out long pv)) return (int)Math.Clamp(pv, 0, max);
            return dflt;
        }
        if (TryInt(v, out long num)) return (int)Math.Clamp(num, 0, max);
        return dflt;
    }

    private static string Norm(string? s)
    {
        if (s is null) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    private static bool TryInt(JsonElement e, out long value)
    {
        value = 0;
        if (e.ValueKind != JsonValueKind.Number) return false;
        if (e.TryGetInt64(out value)) return true;
        if (e.TryGetDecimal(out decimal m))
        {
            value = (long)decimal.Truncate(m);
            return true;
        }
        // Huge magnitude (beyond decimal): treat by sign so it clamps.
        value = e.GetRawText().StartsWith('-') ? long.MinValue : long.MaxValue;
        return true;
    }

    private static string? CleanSay(string? s)
    {
        if (s is null) return null;
        var sb = new StringBuilder(Math.Min(s.Length, MaxSay + 2));
        foreach (char c in s)
        {
            if (c == '\n' || c == '\r' || c == '\t') sb.Append(' ');
            else if (!char.IsControl(c)) sb.Append(c);
            if (sb.Length > MaxSay + 1) break;
        }
        string r = sb.ToString().Trim();
        if (r.Length > MaxSay)
        {
            int cut = MaxSay;
            if (char.IsHighSurrogate(r[cut - 1])) cut--;
            r = r.Substring(0, cut).TrimEnd();
        }
        return r.Length == 0 ? null : r;
    }
}
