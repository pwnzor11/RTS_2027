// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;

namespace Rts.Scripting;

// M8-03 owns this file: objectives, timers, area triggers, win/lose helpers, tech helpers (docs/missions/api.md).
// M8-02 contract: every function below validates its arguments the final way and then throws "not implemented (M8-03)".
// M8-03 replaces the NotImplemented lines with the behaviour; it may add TriggerRecord / MissionTimer fields only by appending to the
// version-2 tail in MissionHost.WriteState / ReadState (see HasVersion2State) and extending MissionStateContractTests.
public sealed partial class MissionHost
{
    private void RegisterObjectivesApi(Script s)
    {
        NewApiTable(s, "Trigger",
            ("OnLeave", a => { ZoneName(a, 0); PlayerArg(a, 1); CallbackName(a, 2); throw NotImplemented("M8-03"); }),
            ("OnHold", a => { ZoneName(a, 0); PlayerArg(a, 1); Int(a, 2, 1, 1_000_000_000); CallbackName(a, 3); throw NotImplemented("M8-03"); }),
            ("OnSeen", a => { ZoneOrTagArg(a, 0); PlayerArg(a, 1); CallbackName(a, 2); throw NotImplemented("M8-03"); }),
            ("OnIdle", a => { KnownTag(a, 0); Int(a, 1, 1, 1_000_000_000); CallbackName(a, 2); throw NotImplemented("M8-03"); }));

        NewApiTable(s, "Objective",
            ("AddHidden", a => { IdArg(a, 0, "id"); Str(a, 1); BoolArg(a, 2, "primary"); throw NotImplemented("M8-03"); }),
            ("Reveal", a => { KnownObjective(a, 0); throw NotImplemented("M8-03"); }),
            ("Status", a => { KnownObjective(a, 0); throw NotImplemented("M8-03"); }));

        NewApiTable(s, "Mission",
            ("Timer", a =>
            {
                IdArg(a, 0, "id"); Int(a, 1, 1, 1_000_000_000); CallbackName(a, 2);
                if (!Absent(a, 3)) Str(a, 3);
                throw NotImplemented("M8-03");
            }),
            ("TimerCancel", a => { IdArg(a, 0, "id"); throw NotImplemented("M8-03"); }),
            ("TimerLeft", a => { IdArg(a, 0, "id"); throw NotImplemented("M8-03"); }),
            ("AllPrimaryDone", _ => throw NotImplemented("M8-03")),
            ("AnyPrimaryFailed", _ => throw NotImplemented("M8-03")),
            ("Difficulty", _ => throw NotImplemented("M8-03")));   // value supplied by M8-07 through the campaign start

        NewApiTable(s, "Tech",
            ("GrantMany", a => { PlayerArg(a, 0); StrList(a, 1, "type ids", 64, types: true); throw NotImplemented("M8-03"); }),
            ("List", a => { PlayerArg(a, 0); throw NotImplemented("M8-03"); }));
    }

    private string KnownObjective(CallbackArguments a, int i)
    {
        string id = Str(a, i);
        if (!_objectives.Exists(o => o.Id == id)) throw Err("unknown objective '" + id + "'");
        return id;
    }
}
