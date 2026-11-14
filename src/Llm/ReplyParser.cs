// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using System.Text.Json;
using Rts.Sim.Core;

namespace Rts.Llm;

/// <summary>Turns a model's text into a <see cref="CommanderReply"/>. Lenient about wrapping, strict about ranges.
/// Never throws. Defaults for unknown values: posture 1, focus 2, target 255, mix 0, expand false, no focus cell.</summary>
public static class ReplyParser
{
    private const int MaxSay = 200;
    private const int MaxCell = 4095;
    private const int MaxOrders = 4;
    private const int MaxMinute = 600;
    private const int MinTechTier = 2, MaxTechTier = 5;

    private static readonly string[] Postures = { "turtle", "balanced", "aggressive", "allin" };
    private static readonly string[] Focuses = { "economy", "tech", "army", "air", "superweapon" };
    private static readonly string[] Mixes = { "auto", "infantry", "vehicles", "antiair", "antistructure", "antiarmour", "antiinfantry" };

    /// <summary>Parses <paramref name="text"/>; any failure yields <c>new CommanderReply(null, null)</c>.
    /// S24 (M5-12): also reads the optional <c>orders</c> list (at most four valid entries) and <c>strong_mix</c>.</summary>
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
        return new CommanderReply(draft, say, ReadOrders(root));
    }

    private static DirectiveDraft ReadDirective(JsonElement d)
    {
        byte posture = (byte)Enum(d, "posture", Postures, 1, 3);
        byte focus = (byte)Enum(d, "focus", Focuses, 2, 4);
        byte mix = (byte)Enum(d, "mix", Mixes, 0, 6);
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
        return new DirectiveDraft(posture, focus, target, mix, expand, fx, fy, ReadBool(d, "strong_mix"));
    }

    private static bool ReadBool(JsonElement d, string name)
    {
        if (!d.TryGetProperty(name, out var e)) return false;
        if (e.ValueKind == JsonValueKind.True) return true;
        if (e.ValueKind == JsonValueKind.String)
        {
            string v = Norm(e.GetString());
            return v == "true" || v == "yes" || v == "1";
        }
        return TryInt(e, out long n) && n > 0;
    }

    /// <summary>S24 (M5-12): the <c>orders</c> array. Bad entries are dropped one by one; the first four valid ones are kept.</summary>
    private static IReadOnlyList<OrderDraft> ReadOrders(JsonElement root)
    {
        var list = new List<OrderDraft>();
        if (!root.TryGetProperty("orders", out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (JsonElement o in arr.EnumerateArray())
        {
            if (list.Count >= MaxOrders) break;
            if (o.ValueKind == JsonValueKind.Object && ReadOrder(o, out OrderDraft order)) list.Add(order);
        }
        return list;
    }

    private static bool ReadOrder(JsonElement o, out OrderDraft order)
    {
        order = default;
        if (!Loose(o, "slot", out long slot) || slot < 0 || slot >= CommanderOrderCodec.Slots) return false;
        if (!o.TryGetProperty("kind", out var kv) || !OrderKind(kv, out CommanderOrderKind kind)) return false;

        bool needsCell = kind is CommanderOrderKind.AttackAt or CommanderOrderKind.HoldCell or CommanderOrderKind.Raid or CommanderOrderKind.ExpandTo;
        short cx = -1, cy = -1;
        if (o.TryGetProperty("cell", out var c) && c.ValueKind == JsonValueKind.Array && c.GetArrayLength() == 2
            && TryInt(c[0], out long x) && TryInt(c[1], out long y) && x >= 0 && y >= 0 && x <= MaxCell && y <= MaxCell)
        {
            cx = (short)x;
            cy = (short)y;
        }
        if (needsCell && cx < 0) return false;
        if (!needsCell) { cx = -1; cy = -1; }

        long amount = 0;
        Loose(o, "amount", out amount);
        amount = kind == CommanderOrderKind.TechTo
            ? Math.Clamp(amount, MinTechTier, MaxTechTier)
            : Math.Clamp(amount, 0, CommanderOrderCodec.MaxAmount);

        long minute = 0;
        if (kind == CommanderOrderKind.AttackAt && Loose(o, "minute", out minute)) minute = Math.Clamp(minute, 0, MaxMinute);

        order = new OrderDraft((byte)slot, kind, cx, cy, (int)amount, (int)minute);
        return true;
    }

    private static bool OrderKind(JsonElement v, out CommanderOrderKind kind)
    {
        kind = CommanderOrderKind.None;
        if (v.ValueKind == JsonValueKind.String)
        {
            switch (Norm(v.GetString()))
            {
                case "none": case "clear": kind = CommanderOrderKind.None; return true;
                case "attack": case "attackat": kind = CommanderOrderKind.AttackAt; return true;
                case "hold": case "holdcell": kind = CommanderOrderKind.HoldCell; return true;
                case "raid": kind = CommanderOrderKind.Raid; return true;
                case "expand": case "expandto": kind = CommanderOrderKind.ExpandTo; return true;
                case "tech": case "techto": kind = CommanderOrderKind.TechTo; return true;
                case "reserve": kind = CommanderOrderKind.Reserve; return true;
                default: return false;
            }
        }
        if (TryInt(v, out long n) && n >= 0 && n <= (long)CommanderOrderKind.Reserve)
        {
            kind = (CommanderOrderKind)n;
            return true;
        }
        return false;
    }

    /// <summary>An integer property given as a JSON number or as a numeric string.</summary>
    private static bool Loose(JsonElement o, string name, out long value)
    {
        value = 0;
        if (!o.TryGetProperty(name, out var e)) return false;
        if (e.ValueKind == JsonValueKind.String) return long.TryParse(e.GetString()?.Trim(), out value);
        return TryInt(e, out value);
    }

    private static int Enum(JsonElement d, string name, string[] names, int dflt, int max)
    {
        if (!d.TryGetProperty(name, out var v)) return dflt;
        if (v.ValueKind == JsonValueKind.String)
        {
            string raw = v.GetString() ?? "";
            string n = Norm(raw);
            if (n == "antiarmor") n = "antiarmour";   // S24: American spelling of the anti-armour mix
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
