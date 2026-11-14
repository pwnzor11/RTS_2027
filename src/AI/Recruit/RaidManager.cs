// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// S24 (M5-12, D-241): carries out a commander's <see cref="CommanderOrderKind.Raid"/> order. A separate group of Amount units (the fastest idle
/// army units first) peels off the army and harasses the area around the order's cell: enemy harvesters first, then the most lightly defended
/// structure, else it attack-moves to the cell. When half of the group is lost it retreats to the rally cell; the survivors rejoin the army and one
/// refill group is sent after <see cref="RefillTicks"/>. After the refill raid retreats the order is done until a new IssuedTick. The main army's
/// logic is untouched: the group is taken out of <see cref="RecruitKnowledge.Army"/> each decision (<see cref="Detach"/>) and put back when it ends.
/// </summary>
internal sealed class RaidManager
{
    /// <summary>Most units one raid group may have, whatever the order asks.</summary>
    public const int MaxGroup = 10;   // (tune)
    /// <summary>Cells around the order's cell in which targets are looked for.</summary>
    public const int AreaCells = 18;   // (tune)
    /// <summary>Ticks between a retreat and the refill group.</summary>
    public const int RefillTicks = 600;   // (tune)
    /// <summary>Retreat when this percent of the launched units is lost.</summary>
    public const int RetreatLossPct = 50;
    /// <summary>Refill groups after the first one.</summary>
    public const int MaxRefills = 1;

    private readonly HashSet<ulong> _group = new();
    private readonly List<ulong> _gone = new();
    private int _slot = -1, _issued = -1, _launched, _refills, _nextForm;
    private bool _out;
    private readonly int[] _doneIssued = { -1, -1, -1, -1 };

    /// <summary>Groups formed.</summary>
    public int Groups { get; private set; }
    /// <summary>Orders (attack / move) given to raiders.</summary>
    public int Strikes { get; private set; }
    /// <summary>Retreats called.</summary>
    public int Retreats { get; private set; }
    /// <summary>Units in the group now.</summary>
    public int Size => _group.Count;

    /// <summary>True when the manager holds anything a fresh one does not (decides whether a save carries the order block).</summary>
    internal bool Dirty => _slot >= 0 || Groups > 0 || _doneIssued[0] >= 0 || _doneIssued[1] >= 0 || _doneIssued[2] >= 0 || _doneIssued[3] >= 0;

    /// <summary>Takes the group out of the army list (call right after <see cref="Squads.Apply"/>); forgets members that died.</summary>
    public void Detach(RecruitKnowledge k)
    {
        if (_group.Count == 0) return;
        _gone.Clear();
        foreach (ulong key in _group)
        {
            bool alive = false;
            foreach (ViewActor a in k.Own) if (RecruitKnowledge.KeyOf(a) == key) { alive = true; break; }
            if (!alive) _gone.Add(key);
        }
        foreach (ulong key in _gone) _group.Remove(key);
        for (int i = k.Army.Count - 1; i >= 0; i--)
        {
            ViewActor a = k.Army[i];
            if (!_group.Contains(RecruitKnowledge.KeyOf(a))) continue;
            k.Army.RemoveAt(i);
            k.Detached.Add(a);
        }
    }

    /// <summary>One decision. <paramref name="known"/> are the known enemy buildings; <paramref name="rally"/> is where a retreating group goes.</summary>
    public void Think(RecruitKnowledge k, ICommandSink sink, IReadOnlyList<CommanderOrder>? orders, List<ViewActor> known, CellCoord rally)
    {
        CommanderOrder order = default;
        int slot = -1;
        if (orders is not null)
            for (int i = 0; i < orders.Count && i < CommanderOrderCodec.Slots; i++)
                if (orders[i].Kind == CommanderOrderKind.Raid && orders[i].Cell.X >= 0 && _doneIssued[i] != orders[i].IssuedTick) { slot = i; order = orders[i]; break; }
        if (slot < 0 || slot != _slot || order.IssuedTick != _issued)
        {
            if (_group.Count > 0 || _slot >= 0) Reset();   // the order was cleared or replaced: the group rejoins the army
            if (slot < 0) return;
            _slot = slot; _issued = order.IssuedTick;
        }
        int tick = k.View.Tick;
        int want = Math.Clamp(order.Amount, 1, MaxGroup);
        if (!_out)
        {
            if (tick < _nextForm || k.Army.Count < want) return;
            Form(k, want);
            if (_group.Count == 0) return;
            _out = true; _launched = _group.Count; Groups++;
        }
        // retreat at the loss share (also when the group is wiped)
        if ((_launched - _group.Count) * 100 >= _launched * RetreatLossPct)
        {
            Retreats++;
            foreach (ViewActor a in k.Detached)
                if (_group.Contains(RecruitKnowledge.KeyOf(a))) sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
            _group.Clear();   // the survivors are army units again from the next decision
            _out = false;
            if (_refills >= MaxRefills) { _doneIssued[_slot] = _issued; _slot = -1; _issued = -1; _refills = 0; }
            else { _refills++; _nextForm = tick + RefillTicks; }
            return;
        }
        Strike(k, sink, order.Cell, known);
    }

    private void Reset()
    {
        _group.Clear(); _slot = -1; _issued = -1; _launched = 0; _refills = 0; _nextForm = 0; _out = false;
    }

    private void Form(RecruitKnowledge k, int want)
    {
        SimRules rules = k.Rules;
        for (int n = 0; n < want; n++)
        {
            int best = -1, bestSpeed = -1;
            for (int i = 0; i < k.Army.Count; i++)
            {
                ViewActor a = k.Army[i];
                if (!a.Idle || a.Underground || a.Rank >= MilitaryManager.RankedTogether || _group.Contains(RecruitKnowledge.KeyOf(a))) continue;
                int speed;
                if (a.Kind == ActorKind.Vehicle)
                {
                    VehicleType v = rules.Vehicles[a.TypeIndex];
                    if (v.Locomotion == Locomotion.Air) continue;
                    speed = v.Speed;
                }
                else speed = rules.Infantry[a.TypeIndex].Speed;
                if (speed > bestSpeed) { bestSpeed = speed; best = i; }   // first of equals wins: the army list is in slot order
            }
            if (best < 0) break;
            ViewActor pick = k.Army[best];
            _group.Add(RecruitKnowledge.KeyOf(pick));
            k.Army.RemoveAt(best);
            k.Detached.Add(pick);
        }
    }

    private void Strike(RecruitKnowledge k, ICommandSink sink, CellCoord cell, List<ViewActor> known)
    {
        SimRules rules = k.Rules;
        // 1) a visible enemy harvester near the cell (nearest to the cell)
        ViewActor? target = null;
        long best = long.MaxValue;
        foreach (ViewActor e in k.Visible)
        {
            if (e.Relation != Relation.Enemy || e.Ghost || e.Kind != ActorKind.Vehicle || rules.Vehicles[e.TypeIndex].HarvestCapacity <= 0) continue;
            if (Cheb(e.Cell, cell) > AreaCells) continue;
            long d = Dist2(e.Cell, cell);
            if (d < best) { best = d; target = e; }
        }
        // 2) else the most lightly defended known structure near the cell (fewest defences covering it, then nearest)
        if (target is null)
        {
            int bestCover = int.MaxValue;
            foreach (ViewActor b in known)
            {
                BuildingType bt = rules.Buildings[b.TypeIndex];
                if (bt.Wall || bt.Gate || Cheb(b.Cell, cell) > AreaCells || SiegePlan.IsDefence(rules, bt)) continue;
                int cover = SiegePlan.Cover(rules, known, b.Cell);
                long d = Dist2(b.Cell, cell);
                if (cover < bestCover || cover == bestCover && d < best) { bestCover = cover; best = d; target = b; }
            }
        }
        foreach (ViewActor a in k.Detached)
        {
            if (!_group.Contains(RecruitKnowledge.KeyOf(a)) || !a.Idle) continue;
            Strikes++;
            if (target is ViewActor t) sink.Issue(CommandKind.Attack, a.Id, a.Kind, default, t.Ref);
            else if (Cheb(a.Cell, cell) > 2) sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, cell);
        }
    }

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    private static long Dist2(CellCoord a, CellCoord b) { long dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }

    // ---- M3-21: AI save/restore of the long-lived fields (written only when <see cref="Dirty"/>). ----

    /// <summary>M3-21: writes the group, the order being served and the finished orders.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        AiSnapshot.WriteKeys(w, _group);
        w.Write(_slot); w.Write(_issued); w.Write(_launched); w.Write(_refills); w.Write(_nextForm); w.Write(_out);
        foreach (int v in _doneIssued) w.Write(v);
        w.Write(Groups); w.Write(Strikes); w.Write(Retreats);
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r)
    {
        AiSnapshot.ReadKeys(r, _group);
        _slot = r.ReadInt32(); _issued = r.ReadInt32(); _launched = r.ReadInt32(); _refills = r.ReadInt32(); _nextForm = r.ReadInt32(); _out = r.ReadBoolean();
        for (int i = 0; i < _doneIssued.Length; i++) _doneIssued[i] = r.ReadInt32();
        Groups = r.ReadInt32(); Strikes = r.ReadInt32(); Retreats = r.ReadInt32();
    }
}
