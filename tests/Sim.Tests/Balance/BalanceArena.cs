// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Balance;

/// <summary>
/// M4-16 scenario harness: the shipped <c>data/rules</c> numbers on a flat 96x96 map, two players, Debug commands allowed. Each
/// side gets a far-away keep-alive building so losing the fight does not defeat the player (defeated players' commands are
/// ignored). Set the environment variable <c>RTS_RULES_DIR</c> to measure a scratch copy of the rules while tuning.
/// </summary>
internal sealed class BalanceArena
{
    private static ushort _seq;
    public readonly SimRules Rules;
    public readonly Simulation Sim;

    public BalanceArena(ulong seed = 7)
    {
        Rules = LoadRules();
        Sim = new Simulation(new SimSetup(seed, MapData.CreateFlat(96, 96), 2, Rules, 0xBA1AUL) { AllowDebug = true });
        Assert.False(Sim.PlaceBuilding(0, Building("auth_power"), new CellCoord(4, 4)).IsNone);    // keep-alive, out of every fight
        Assert.False(Sim.PlaceBuilding(1, Building("auth_power"), new CellCoord(90, 90)).IsNone);
    }

    /// <summary>The rules from <c>RTS_RULES_DIR</c> if set, else the shipped <c>data/rules</c>.</summary>
    public static SimRules LoadRules()
    {
        string? dir = Environment.GetEnvironmentVariable("RTS_RULES_DIR");
        if (string.IsNullOrEmpty(dir)) dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!).Sim;
    }

    public int Building(string id) { int i = SimRules.IndexOf(Rules.Buildings, id, b => b.Id); Assert.True(i >= 0, id); return i; }
    public int Inf(string id) { int i = SimRules.IndexOf(Rules.Infantry!, id, t => t.Id); Assert.True(i >= 0, id); return i; }
    public int Veh(string id) { int i = SimRules.IndexOf(Rules.Vehicles, id, t => t.Id); Assert.True(i >= 0, id); return i; }

    public int InfCost(string id) => Rules.Infantry![Inf(id)].Cost;
    public int VehCost(string id) => Rules.Vehicles[Veh(id)].Cost;
    public int BuildingCost(string id) => Rules.Buildings[Building(id)].Cost;

    public Command Cmd(byte player, CommandKind kind, EntityId subject = default, EntityId target = default, CellCoord cell = default,
        int arg = 0, int arg2 = 0) => new(Sim.Tick, player, _seq++, kind, subject, target, cell, arg, arg2);

    public void Run(int ticks, params Command[] first)
    {
        for (int i = 0; i < ticks; i++) Sim.Step(i == 0 ? first : Array.Empty<Command>());
    }

    /// <summary>Setup phase: the building at <paramref name="topLeft"/> plus <paramref name="count"/> soldiers beside it. Call
    /// <see cref="FillGarrison"/> after spawning everything else (spawns are only allowed before the first Step).</summary>
    public (EntityId Building, List<EntityId> Men) PlaceGarrison(byte player, string building, string soldier, CellCoord topLeft, int count)
    {
        EntityId b = Sim.PlaceBuilding(player, Building(building), topLeft);
        Assert.False(b.IsNone);
        var men = new List<EntityId>();
        for (int k = 0; k < count; k++) men.Add(Sim.SpawnInfantry(player, Inf(soldier), new CellCoord(topLeft.X + 2, topLeft.Y + (k % 2)), k % 3));
        return (b, men);
    }

    /// <summary>Orders the soldiers in and steps until the building is full.</summary>
    public void FillGarrison(byte player, EntityId b, List<EntityId> men)
    {
        Run(1, men.Select(m => Cmd(player, CommandKind.Enter, m, b, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8))).ToArray());
        for (int i = 0; i < 400 && Sim.OccupantsIn(b) < men.Count; i++) Sim.Step(Array.Empty<Command>());
        Assert.Equal(men.Count, Sim.OccupantsIn(b));
    }

    public bool Alive(ActorKind kind, EntityId id) => Sim.TryResolve(new ActorRef(kind, id));

    /// <summary>Spawns <paramref name="count"/> infantry of a type in a block starting at <paramref name="origin"/> (five per cell).</summary>
    public List<ActorRef> SpawnInfantry(byte player, string type, int count, CellCoord origin)
    {
        var list = new List<ActorRef>();
        for (int k = 0; k < count; k++)
            list.Add(new ActorRef(ActorKind.Infantry, Sim.SpawnInfantry(player, Inf(type), new CellCoord(origin.X + (k / 5) % 4, origin.Y + k / 20), k % 5)));
        return list;
    }

    /// <summary>Spawns <paramref name="count"/> vehicles of a type in a column starting at <paramref name="origin"/>.</summary>
    public List<ActorRef> SpawnVehicles(byte player, string type, int count, CellCoord origin)
    {
        var list = new List<ActorRef>();
        for (int k = 0; k < count; k++)
            list.Add(new ActorRef(ActorKind.Vehicle, Sim.SpawnVehicle(player, Veh(type), new CellCoord(origin.X + (k % 2) * 2, origin.Y + (k / 2) * 2))));
        return list;
    }

    /// <summary>Orders every unit to attack the building.</summary>
    public Command[] AttackBuilding(byte player, IEnumerable<ActorRef> units, EntityId building) =>
        units.Select(u => Cmd(player, CommandKind.Attack, u.Id, building, default, (int)u.Kind | ((int)ActorKind.Building << 8))).ToArray();

    /// <summary>Orders every unit to attack-move to the cell.</summary>
    public Command[] AttackMove(byte player, IEnumerable<ActorRef> units, CellCoord cell) =>
        units.Select(u => Cmd(player, CommandKind.AttackMove, u.Id, default, cell, (int)u.Kind)).ToArray();

    /// <summary>Total cost of the units that are still alive.</summary>
    public int AliveValue(IEnumerable<ActorRef> units) => units.Where(u => Sim.TryResolve(u)).Sum(u =>
        u.Kind == ActorKind.Vehicle ? Rules.Vehicles[Sim.Units.TypeIndex[u.Id.Index]].Cost : Rules.Infantry![Sim.Infantry.TypeIndex[u.Id.Index]].Cost);

    public int AliveCount(IEnumerable<ActorRef> units) => units.Count(u => Sim.TryResolve(u));
}
