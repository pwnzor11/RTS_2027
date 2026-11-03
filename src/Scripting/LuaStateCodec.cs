// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;

namespace Rts.Scripting;

/// <summary>
/// Saves and loads the Lua <c>state</c> table as plain values (nil never stored, booleans, integers, strings, nested tables with
/// integer or string keys), keys in canonical order so the bytes are identical for identical contents. Anything else
/// (floats with a fraction, functions, userdata, cycles, other key types) is invalid: strict mode throws, lenient mode skips the entry.
/// </summary>
internal static class LuaStateCodec
{
    private const int MaxDepth = 12;
    private const double MaxSafe = 9007199254740992.0;   // 2^53: every integer below is an exact double

    private enum Tag : byte { False = 1, True = 2, Int = 3, Str = 4, Table = 5 }

    public static bool IsInteger(double d) => d == Math.Floor(d) && Math.Abs(d) < MaxSafe;

    /// <summary>Canonical order of keys: integers ascending, then strings ordinal. Returns false for a key type that may not appear.</summary>
    public static bool TryOrderedKeys(Table t, out List<DynValue> keys)
    {
        keys = new List<DynValue>();
        foreach (DynValue k in t.Keys)
        {
            if (k.Type == DataType.Number || k.Type == DataType.String) keys.Add(k);
            else return false;
        }
        keys.Sort(CompareKeys);
        return true;
    }

    private static int CompareKeys(DynValue a, DynValue b)
    {
        if (a.Type != b.Type) return a.Type == DataType.Number ? -1 : 1;
        return a.Type == DataType.Number ? a.Number.CompareTo(b.Number) : string.CompareOrdinal(a.String, b.String);
    }

    public static void Write(BinaryWriter w, Table t, bool strict) => WriteTable(w, t, strict, 0);

    private static void WriteTable(BinaryWriter w, Table t, bool strict, int depth)
    {
        if (depth > MaxDepth)
        {
            if (strict) throw new ScriptRuntimeException("state: nested deeper than " + MaxDepth + " (or a cycle)");
            w.Write(0);
            return;
        }

        if (!TryOrderedKeys(t, out List<DynValue> keys))
        {
            if (strict) throw new ScriptRuntimeException("state: table keys must be integers or strings");
            keys = new List<DynValue>();
            foreach (DynValue k in t.Keys) if (k.Type is DataType.Number or DataType.String) keys.Add(k);
            keys.Sort(CompareKeys);
        }

        // First pass decides which entries are writable so the count can be written first.
        var entries = new List<(DynValue Key, DynValue Value)>(keys.Count);
        foreach (DynValue k in keys)
        {
            DynValue v = t.Get(k);
            if (v.Type == DataType.Nil || v.Type == DataType.Void) continue;
            string? problem = Problem(k, v);
            if (problem != null)
            {
                if (strict) throw new ScriptRuntimeException($"state[{Describe(k)}]: {problem}");
                continue;
            }
            entries.Add((k, v));
        }

        w.Write(entries.Count);
        foreach ((DynValue k, DynValue v) in entries)
        {
            WriteScalar(w, k);
            if (v.Type == DataType.Table) { w.Write((byte)Tag.Table); WriteTable(w, v.Table, strict, depth + 1); }
            else WriteScalar(w, v);
        }
    }

    private static string Describe(DynValue k) => k.Type == DataType.String ? "\"" + k.String + "\"" : k.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string? Problem(DynValue k, DynValue v)
    {
        if (k.Type == DataType.Number && !IsInteger(k.Number)) return "key is not an integer";
        switch (v.Type)
        {
            case DataType.Boolean: case DataType.String: case DataType.Table: return null;
            case DataType.Number: return IsInteger(v.Number) ? null : "value is not an integer (" + v.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            default: return "a " + v.Type.ToLuaTypeString() + " cannot be saved";
        }
    }

    private static void WriteScalar(BinaryWriter w, DynValue v)
    {
        switch (v.Type)
        {
            case DataType.Boolean: w.Write((byte)(v.Boolean ? Tag.True : Tag.False)); break;
            case DataType.Number: w.Write((byte)Tag.Int); w.Write((long)v.Number); break;
            case DataType.String: w.Write((byte)Tag.Str); w.Write(v.String); break;
        }
    }

    public static Table Read(BinaryReader r, Script script) => ReadTable(r, script, 0);

    private static Table ReadTable(BinaryReader r, Script script, int depth)
    {
        if (depth > MaxDepth) throw new InvalidDataException("mission state nested too deeply");
        var t = new Table(script);
        int n = r.ReadInt32();
        if (n < 0) throw new InvalidDataException("bad mission state");
        for (int i = 0; i < n; i++)
        {
            DynValue k = ReadScalar(r);
            var tag = (Tag)r.ReadByte();
            DynValue v = tag == Tag.Table ? DynValue.NewTable(ReadTable(r, script, depth + 1)) : ScalarOf(tag, r);
            t.Set(k, v);
        }
        return t;
    }

    private static DynValue ReadScalar(BinaryReader r) => ScalarOf((Tag)r.ReadByte(), r);

    private static DynValue ScalarOf(Tag tag, BinaryReader r) => tag switch
    {
        Tag.False => DynValue.False,
        Tag.True => DynValue.True,
        Tag.Int => DynValue.NewNumber(r.ReadInt64()),
        Tag.Str => DynValue.NewString(r.ReadString()),
        _ => throw new InvalidDataException("bad mission state tag"),
    };
}
