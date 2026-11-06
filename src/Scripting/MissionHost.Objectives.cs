// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting;

// M8-03 owns this file: objectives, timers, area triggers, win/lose helpers, tech helpers (docs/missions/api.md).
// State added by M8-03 lives in fields that already exist (timers, hidden objectives, trigger B / Counter) plus the difficulty tier,
// which is appended after the version-2 tail and written as part format 3 (see MissionHost.WriteState).
public sealed partial class MissionHost
{
    /// <summary>Difficulty tier a mission runs at when nothing set it (the middle skirmish tier).</summary>
    public const int DefaultDifficulty = 3;

    /// <summary>
    /// Sets the tier (1..5) <c>Mission.Difficulty()</c> returns. M8-07 (campaign start) calls this once, after constructing the host and
    /// before the first <c>Step</c> (also before <c>Simulation.Load</c>; a loaded save restores the tier it was made with). The value is
    /// part of the saved and hashed "mission" state, so a run is reproducible from (seed, tier, commands).
    /// </summary>
    public void SetDifficulty(int tier)
    {
        if (tier < 1 || tier > 5) throw new ArgumentOutOfRangeException(nameof(tier), "difficulty tier must be 1..5");
        _difficulty = tier;
    }

    /// <summary>The tier <c>Mission.Difficulty()</c> returns (1..5, default <see cref="DefaultDifficulty"/>).</summary>
    public int DifficultyTier => _difficulty;

    private void RegisterObjectivesApi(Script s)
    {
        NewApiTable(s, "Trigger",
            ("OnLeave", a => AddTrigger(TriggerKind.Leave, PlayerArg(a, 1), ZoneName(a, 0), a, 2, 0)),
            ("OnHold", a => AddAreaTrigger(TriggerKind.Hold, PlayerArg(a, 1), ZoneName(a, 0), a, 3, Int(a, 2, 1, 1_000_000_000))),
            ("OnSeen", a => AddTrigger(TriggerKind.Seen, PlayerArg(a, 1), ZoneOrTagArg(a, 0), a, 2, 0)),
            ("OnIdle", a => AddAreaTrigger(TriggerKind.Idle, 0, KnownTag(a, 0), a, 2, Int(a, 1, 1, 1_000_000_000))));

        NewApiTable(s, "Objective",
            ("AddHidden", a =>
            {
                string id = IdArg(a, 0, "id"), text = Str(a, 1);
                bool primary = BoolArg(a, 2, "primary");
                if (_objectives.Exists(o => o.Id == id)) throw Err("objective '" + id + "' already exists");
                _objectives.Add(new MissionObjective(id, text, primary, ObjectiveStatus.Active, Hidden: true));
                return DynValue.Nil;
            }),
            ("Reveal", a =>
            {
                int i = _objectives.FindIndex(o => o.Id == KnownObjective(a, 0));
                _objectives[i] = _objectives[i] with { Hidden = false };
                return DynValue.Nil;
            }),
            ("Status", a =>
            {
                MissionObjective o = _objectives.Find(x => x.Id == KnownObjective(a, 0))!;
                return DynValue.NewString(o.Status switch { ObjectiveStatus.Completed => "completed", ObjectiveStatus.Failed => "failed", _ => "active" });
            }));

        NewApiTable(s, "Mission",
            ("Timer", a =>
            {
                string id = IdArg(a, 0, "id");
                int ticks = Int(a, 1, 1, 1_000_000_000);
                string fn = CallbackName(a, 2);
                string label = Absent(a, 3) ? "" : Str(a, 3);
                // The same id again restarts the timer in place (keeps its position in the HUD list).
                var timer = new MissionTimer(id, label, (int)Math.Min(int.MaxValue, (long)_sim!.Tick + ticks), fn, false);
                int at = _timers.FindIndex(t => t.Id == id);
                if (at >= 0) _timers[at] = timer; else _timers.Add(timer);
                return DynValue.Nil;
            }),
            ("TimerCancel", a => { string id = IdArg(a, 0, "id"); _timers.RemoveAll(t => t.Id == id); return DynValue.Nil; }),
            ("TimerLeft", a =>
            {
                string id = IdArg(a, 0, "id");
                int at = _timers.FindIndex(t => t.Id == id);
                if (at < 0) return DynValue.Nil;
                MissionTimer t = _timers[at];
                return Num(t.Paused ? t.EndTick : Math.Max(0, t.EndTick - _sim!.Tick));
            }),
            ("AllPrimaryDone", _ =>
            {
                bool any = false;
                foreach (MissionObjective o in _objectives)
                {
                    if (!o.Primary) continue;
                    any = true;
                    if (o.Status != ObjectiveStatus.Completed) return DynValue.False;
                }

                return DynValue.NewBoolean(any);
            }),
            ("AnyPrimaryFailed", _ => DynValue.NewBoolean(_objectives.Exists(static o => o.Primary && o.Status == ObjectiveStatus.Failed))),
            ("Difficulty", _ => Num(_difficulty)));

        NewApiTable(s, "Tech",
            ("GrantMany", a =>
            {
                int player = PlayerArg(a, 0);
                foreach (string id in StrList(a, 1, "type ids", 64, types: true)) _granted[player].Add(id);
                return DynValue.Nil;
            }),
            ("List", a =>
            {
                int player = PlayerArg(a, 0);
                var list = new Table(_script);
                int k = 0;
                foreach (string id in _granted[player]) list.Set(++k, DynValue.NewString(id));
                return DynValue.NewTable(list);
            }));
    }

    private DynValue AddAreaTrigger(TriggerKind kind, int player, string name, CallbackArguments args, int fnIndex, int ticks)
    {
        _triggers.Add(new TriggerRecord { Kind = kind, A = player, Name = name, Fn = CallbackName(args, fnIndex), B = ticks });
        return DynValue.NewNil();
    }

    // ---- per-tick evaluation (called from MissionHost.OnTick) ----

    /// <summary>Area triggers (Leave, Hold, Seen, Idle). Counters live in the trigger record and are saved with it.</summary>
    private bool ShouldFireArea(TriggerRecord t, Simulation sim)
    {
        switch (t.Kind)
        {
            case TriggerKind.Leave:
            {
                if (Def.ZoneNamed(t.Name) is not { } z) return false;
                if (AnyMobileInZone(sim, (byte)t.A, z)) { t.Counter = 1; return false; }   // armed: the player has been inside
                return t.Counter == 1;
            }
            case TriggerKind.Hold:
            {
                if (Def.ZoneNamed(t.Name) is not { } z) return false;
                t.Counter = AnyMobileInZone(sim, (byte)t.A, z) ? t.Counter + 1 : 0;
                return t.Counter >= t.B;
            }
            case TriggerKind.Seen:
                return IsSeen(sim, t);
            case TriggerKind.Idle:
            {
                if (!TryTag(t.Name, out ActorRef r) || !sim.TryResolve(r)) { t.Done = true; return false; }   // gone: never fires
                t.Counter = IsIdle(sim, r) ? t.Counter + 1 : 0;
                return t.Counter >= t.B;
            }
            default:
                return false;
        }
    }

    /// <summary>A zone (any cell of it) or a live tagged actor (its cell) is visible to the player or a teammate. A zone name wins over a tag.</summary>
    private bool IsSeen(Simulation sim, TriggerRecord t)
    {
        if (Def.ZoneNamed(t.Name) is { } z)
        {
            for (int y = z.Y0; y <= z.Y1; y++)
                for (int x = z.X0; x <= z.X1; x++)
                    if (VisibleToTeam(sim, (byte)t.A, new CellCoord(x, y))) return true;
            return false;
        }

        if (!TryTag(t.Name, out ActorRef r) || !sim.TryResolve(r)) { t.Done = true; return false; }   // destroyed unseen: never fires
        return VisibleToTeam(sim, (byte)t.A, CellOfActor(sim, r));
    }

    private static bool VisibleToTeam(Simulation sim, byte player, CellCoord cell)
    {
        if (!sim.Setup.Map.Contains(cell)) return false;
        byte team = sim.Players.Team[player];
        for (int q = 0; q < sim.Players.Count; q++)
            if (q == player || (!sim.Players.IsNeutral((byte)q) && sim.Players.Team[q] == team))
                if (sim.Vision.IsVisible((byte)q, cell)) return true;
        return false;
    }

    /// <summary>No mission running or queued (None, Guard or Sleep). Buildings have no missions and count as idle.</summary>
    private static bool IsIdle(Simulation sim, ActorRef r)
    {
        Rts.Sim.World.MobileStore? store = r.Kind switch { ActorKind.Vehicle => sim.Units, ActorKind.Infantry => sim.Infantry, _ => null };
        if (store == null) return true;
        int i = r.Id.Index;
        return store.NextMission[i] == Rts.Sim.Missions.MissionKind.None
            && store.Mission[i] is Rts.Sim.Missions.MissionKind.None or Rts.Sim.Missions.MissionKind.Guard or Rts.Sim.Missions.MissionKind.Sleep;
    }

    /// <summary>Runs expired timers in creation order. A timer made by a callback has at least one tick to go, so the loop ends.</summary>
    private void RunTimers(int tick)
    {
        while (!_halted)
        {
            int at = _timers.FindIndex(t => !t.Paused && t.EndTick <= tick);
            if (at < 0) return;
            MissionTimer timer = _timers[at];
            _timers.RemoveAt(at);
            Invoke(timer.Fn, _script.Globals.Get(timer.Fn));
        }
    }

    private string KnownObjective(CallbackArguments a, int i)
    {
        string id = Str(a, i);
        if (!_objectives.Exists(o => o.Id == id)) throw Err("unknown objective '" + id + "'");
        return id;
    }
}
