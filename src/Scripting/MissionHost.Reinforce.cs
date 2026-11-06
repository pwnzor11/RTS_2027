// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;

namespace Rts.Scripting;

// M8-04 owns this file: reinforcement variants and spawn groups (docs/missions/api.md).
// M8-02 contract: every function validates its arguments the final way and then throws "not implemented (M8-04)".
// Reinforce.Edge (pilot) stays in MissionHost.Api.cs and must not change.
public sealed partial class MissionHost
{
    private static readonly string[] Formations = { "line", "column", "box", "scatter" };

    private void RegisterReinforceApi(Script s)
    {
        NewApiTable(s, "Reinforce",
            ("Zone", a => { PlayerArg(a, 0); StrList(a, 1, "type ids", 32, types: true); ZoneName(a, 2); CellArg(a, 3); throw NotImplemented("M8-04"); }),
            ("Transport", a => { PlayerArg(a, 0); StrList(a, 1, "type ids", 32, types: true); EdgeCellArg(a, 2); CellArg(a, 3); throw NotImplemented("M8-04"); }),
            ("Delayed", a => { Int(a, 0, 1, 1_000_000_000); PlayerArg(a, 1); StrList(a, 2, "type ids", 32, types: true); EdgeCellArg(a, 3); CellArg(a, 4); throw NotImplemented("M8-04"); }),
            ("Naval", a => { PlayerArg(a, 0); StrList(a, 1, "type ids", 32, types: true); EdgeCellArg(a, 2); CellArg(a, 3); throw NotImplemented("M8-04"); }),
            ("Rig", a => { PlayerArg(a, 0); EdgeCellArg(a, 1); CellArg(a, 2); throw NotImplemented("M8-04"); }));

        NewApiTable(s, "Actor",
            ("CreateGroup", a =>
            {
                StrList(a, 0, "type ids", 32, types: true);
                PlayerArg(a, 1);
                CellArg(a, 2);
                if (!Absent(a, 3)) GroupOptions(a, 3);
                throw NotImplemented("M8-04");
            }));
    }

    /// <summary>The optional options table of Actor.CreateGroup: {formation = "line"|"column"|"box"|"scatter", rank = 0..3, tagPrefix = "x"}.</summary>
    private void GroupOptions(CallbackArguments a, int i)
    {
        DynValue o = a[i];
        if (o.Type != DataType.Table) throw Err("argument " + (i + 1) + " (options) must be a table, got " + o.Type.ToLuaTypeString());
        DynValue f = o.Table.Get("formation");
        if (f.Type != DataType.Nil && (f.Type != DataType.String || Array.IndexOf(Formations, f.String) < 0))
            throw Err("options.formation must be one of " + string.Join(", ", Formations));
        DynValue r = o.Table.Get("rank");
        if (r.Type != DataType.Nil) IntOf(r, "options.rank", 0, Rts.Sim.Rules.VeterancyRules.Ranks);
        DynValue t = o.Table.Get("tagPrefix");
        if (t.Type != DataType.Nil && (t.Type != DataType.String || t.String.Length is 0 or > 32)) throw Err("options.tagPrefix must be a string of 1-32 characters");
    }
}
