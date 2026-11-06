// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;
using Rts.Sim.Rules;

namespace Rts.Scripting;

// M8-02: argument validators shared by the M8 API partials (Objectives, Reinforce, Media, Ai). Owned by M8-02; append only.
public sealed partial class MissionHost
{
    // Test seams (Scripting.Tests only): the raw state lists.
    internal List<MissionTimer> TimerStore => _timers;
    internal List<TriggerRecord> TriggerStore => _triggers;
    internal List<MissionObjective> ObjectiveStore => _objectives;

    /// <summary>The error every throwing stub raises after its arguments checked out.</summary>
    private ScriptRuntimeException NotImplemented(string package) => Err("not implemented (" + package + ")");

    private bool BoolArg(CallbackArguments a, int i, string what) =>
        a[i].Type == DataType.Boolean ? a[i].Boolean : throw Err("argument " + (i + 1) + " (" + what + ") must be true or false, got " + a[i].Type.ToLuaTypeString());

    private bool Absent(CallbackArguments a, int i) => a[i].Type is DataType.Nil or DataType.Void;

    /// <summary>A non-empty string with no whitespace or control characters (ids, keys).</summary>
    private string IdArg(CallbackArguments a, int i, string what)
    {
        string s = Str(a, i);
        if (s.Length == 0 || s.Length > 64 || s.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw Err("argument " + (i + 1) + " (" + what + ") must be 1-64 characters without spaces");
        return s;
    }

    /// <summary>A non-empty list {"a", "b"} of at most <paramref name="max"/> strings; each entry must be a known rules type id when <paramref name="types"/>.</summary>
    private List<string> StrList(CallbackArguments a, int i, string what, int max, bool types)
    {
        DynValue v = a[i];
        if (v.Type != DataType.Table || v.Table.Length < 1) throw Err("argument " + (i + 1) + " (" + what + ") must be a non-empty list of strings");
        if (v.Table.Length > max) throw Err("argument " + (i + 1) + " (" + what + ") has more than " + max + " entries");
        var list = new List<string>();
        for (int k = 1; k <= v.Table.Length; k++)
        {
            DynValue e = v.Table.Get(k);
            if (e.Type != DataType.String) throw Err(what + " entries must be strings");
            if (types) ResolveType(e.String);
            list.Add(e.String);
        }

        return list;
    }

    /// <summary>A player slot whose role is "ai" (the built-in AI reads its hooks).</summary>
    private int AiPlayerArg(CallbackArguments a, int i)
    {
        int p = PlayerArg(a, i);
        if (Def.Players[p].Role != "ai") throw Err("player " + p + " is not an AI player (role \"" + Def.Players[p].Role + "\")");
        return p;
    }

    /// <summary>A map-edge cell (x = 0, y = 0, x = width-1 or y = height-1).</summary>
    private Rts.Sim.Core.CellCoord EdgeCellArg(CallbackArguments a, int i)
    {
        Rts.Sim.Core.CellCoord c = CellArg(a, i);
        int w = _sim!.Setup.Map.Width, h = _sim.Setup.Map.Height;
        if (c.X != 0 && c.Y != 0 && c.X != w - 1 && c.Y != h - 1) throw Err("edge cell (" + c.X + "," + c.Y + ") is not on the map edge");
        return c;
    }

    /// <summary>A zone name or a known actor tag (camera targets, sight triggers).</summary>
    private string ZoneOrTagArg(CallbackArguments a, int i)
    {
        string name = Str(a, i);
        if (Def.ZoneNamed(name) == null && !TryTag(name, out _)) throw Err("'" + name + "' is neither a zone nor an actor tag");
        return name;
    }
}
