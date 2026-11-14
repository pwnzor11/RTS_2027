// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.M4;
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace AI.Tests.S24;

/// <summary>S24 (M5-12): a three-player game on the M4 quad map. Slot 0 plays the tiered AI; slots 1 and 2 only hold their bases. Commander orders are
/// put in the command stream by the test (or, in one test, by an <see cref="IOrderSource"/>); every command the driver collects is recorded.</summary>
internal sealed class OrderRig
{
    private readonly List<Command> _pending = new();
    private readonly DifficultyProfile _profile;
    private AiDriver _driver;
    private ushort _seq = 40000;

    public Simulation Sim { get; }
    public RecruitAi Ai { get; private set; }
    /// <summary>Every command the driver produced, in order (the AI's own, plus any from an order source).</summary>
    public List<Command> Collected { get; } = new();

    public OrderRig(int tier = 3, Faction faction = Faction.Authority, ulong seed = 3)
    {
        _profile = TestWorld.Tiers[tier - 1];
        MapData map = M4AiTests.QuadMap(48, seed);
        Sim = new Simulation(new SimSetup(seed, map, 3, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash)
        { Factions = [faction, Faction.Authority, Faction.Authority], AllowDebug = true });
        SkirmishStart.Apply(Sim);
        _driver = new AiDriver(seed);
        Ai = new RecruitAi(_profile);
        _driver.Add(0, Ai, _profile);
        _driver.Add(1, new IdleAi(), _profile);
        _driver.Add(2, new IdleAi(), _profile);
        Run(3);   // vision exists from the first Step
    }

    public AiDriver Driver => _driver;

    public void Run(int ticks, Action<OrderRig>? each = null)
    {
        for (int t = 0; t < ticks; t++)
        {
            int before = _pending.Count;
            _driver.Collect(Sim, _pending);
            for (int i = before; i < _pending.Count; i++) Collected.Add(_pending[i]);
            Sim.Step(_pending);
            _pending.RemoveAll(c => c.ExecTick < Sim.Tick);
            each?.Invoke(this);
        }
    }

    /// <summary>Puts one order for player 0 into the command stream for the next tick.</summary>
    public void Order(int slot, CommanderOrderKind kind, CellCoord cell, int amount = 0, int dueTick = 0) =>
        _pending.Add(CommanderOrderCodec.ToCommand(Sim.Tick, 0, _seq++, kind, slot, cell, amount, dueTick));

    /// <summary>The farthest visible cell (up to <paramref name="steps"/> diagonal steps) from the first building of player 0 toward the map centre; call after the first Step.</summary>
    public CellCoord NearYard(int steps = 8)
    {
        CellCoord y = default;
        for (int i = 1; i < Sim.Buildings.Capacity; i++)
            if (Sim.Buildings.Slots.IsIndexAlive(i) && Sim.Buildings.Player[i] == 0) { y = Sim.Buildings.Cell[i]; break; }
        int cx = Sim.Setup.Map.Width / 2, cy = Sim.Setup.Map.Height / 2;
        var view = Sim.ViewFor(0);
        for (int s = steps; s >= 1; s--)
        {
            var c = new CellCoord(y.X + Math.Sign(cx - y.X) * s, y.Y + Math.Sign(cy - y.Y) * s);
            if (view.IsVisible(c)) return c;
        }
        return y;
    }

    /// <summary>Kills every non-harvester unit of player 0 (a Debug command, effective next tick).</summary>
    public void KillArmy()
    {
        for (int i = 1; i < Sim.Units.Capacity; i++)
            if (Sim.Units.Slots.IsIndexAlive(i) && Sim.Units.Player[i] == 0 && TestWorld.Rules.Sim.Vehicles[Sim.Units.TypeIndex[i]].HarvestCapacity <= 0)
                _pending.Add(new Command(Sim.Tick, 0, _seq++, CommandKind.Debug, Sim.Units.Slots.IdOf(i), default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle));
        for (int i = 1; i < Sim.Infantry.Capacity; i++)
            if (Sim.Infantry.Slots.IsIndexAlive(i) && Sim.Infantry.Player[i] == 0)
                _pending.Add(new Command(Sim.Tick, 0, _seq++, CommandKind.Debug, Sim.Infantry.Slots.IdOf(i), default, default, (int)DebugOp.Kill, (int)ActorKind.Infantry));
    }

    /// <summary>The AI memory of slot 0 as a save would hold it.</summary>
    public byte[]? CaptureAi() => _driver.CaptureState(0);

    /// <summary>Replaces the AI by a fresh one that starts from <paramref name="blob"/> (the sim keeps running).</summary>
    public void ResumeWithBlob(byte[] blob)
    {
        _driver = new AiDriver(Sim.Setup.Seed);
        Ai = new RecruitAi(_profile);
        _driver.Add(0, Ai, _profile);
        _driver.Add(1, new IdleAi(), _profile);
        _driver.Add(2, new IdleAi(), _profile);
        _driver.RestoreState(0, blob);
    }
}
