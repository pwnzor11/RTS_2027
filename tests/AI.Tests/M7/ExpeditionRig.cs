// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace AI.Tests.M7;

/// <summary>M7-10: a two-AI game on a generated ocean-edge map; slot 0 plays the Expedition, slot 1 plays <c>enemy</c>.</summary>
internal sealed class ExpeditionRig
{
    /// <summary>Counts ship orders (Move / AttackMove) and where they point.</summary>
    private sealed class Recorder(IAiPlayer inner, ExpeditionRig rig) : IAiPlayer, IAiSnapshotable
    {
        public void SaveState(BinaryWriter w) => ((IAiSnapshotable)inner).SaveState(w);
        public void LoadState(BinaryReader r) => ((IAiSnapshotable)inner).LoadState(r);
        public string Id => inner.Id;
        public void Start(AiContext ctx) => inner.Start(ctx);
        public void Think(PlayerView view, ICommandSink sink) { rig._view = view; inner.Think(view, new Sink(rig, view, sink)); }

        private sealed class Sink(ExpeditionRig rig, PlayerView view, ICommandSink real) : ICommandSink
        {
            public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default, ActorRef target = default, int arg2 = 0)
            {
                if (kind is CommandKind.Move or CommandKind.AttackMove && subjectKind == ActorKind.Vehicle && rig.IsShip(subject))
                {
                    rig.ShipOrders++;
                    if (kind == CommandKind.AttackMove) { rig.ShipAttackMoves++; rig.FirstAttackMoveTick ??= view.Tick; }
                    if (view.NavalRegionAt(cell) <= 0) rig.ShipOrdersToLand++;
                }
                if (kind == CommandKind.Attack) rig.Attacks++;
                real.Issue(kind, subject, subjectKind, cell, target, arg2);
            }
            public void Build(ProductionCategory category, int typeIndex) => real.Build(category, typeIndex);
            public void Place(int buildingType, CellCoord topLeft) => real.Place(buildingType, topLeft);
            public void Rally(EntityId factory, CellCoord cell) => real.Rally(factory, cell);
        }
    }

    private PlayerView? _view;
    private AiDriver _driver;
    private readonly List<Command> _pending = new();
    private readonly DifficultyProfile _profile;
    private readonly DifficultyProfile? _enemyProfile;
    public Simulation Sim { get; }
    public RecruitAi Ai { get; private set; }
    public RecruitAi? Enemy { get; private set; }
    public int ShipOrders, ShipOrdersToLand, Attacks, ShipAttackMoves;
    public int? FirstAttackMoveTick;
    public MapMeta Meta { get; }

    public static (MapData Map, MapMeta Meta) OceanMap(ulong seed = 3, MapSize size = MapSize.Medium, OceanEdges edges = OceanEdges.North | OceanEdges.South) =>
        MapGenerator.GenerateWithMeta(new MapGenParams(Biome.RustBelt, size, 2, seed, null, edges));

    public ExpeditionRig(DifficultyProfile profile, Faction enemy = Faction.Authority, bool enemyAi = true, ulong seed = 1, MapSize size = MapSize.Medium, DifficultyProfile? enemyProfile = null, ulong? mapSeed = null, OceanEdges edges = OceanEdges.North | OceanEdges.South)
    {
        (MapData map, MapMeta meta) = OceanMap(mapSeed ?? seed + 2, size, edges);
        Meta = meta;
        Sim = new Simulation(new SimSetup(seed, map, 2, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash) { Factions = [Faction.Expedition, enemy], AllowDebug = true, LandingMarkers = meta.Landing });
        SkirmishStart.Apply(Sim);
        _profile = profile;
        _enemyProfile = enemyProfile ?? profile;
        _driver = new AiDriver(seed);
        Ai = new RecruitAi(profile);
        _driver.Add(0, new Recorder(Ai, this), profile);
        if (enemyAi)
        {
            Enemy = new RecruitAi(_enemyProfile);
            _driver.Add(1, Enemy, _enemyProfile);
        }
    }

    private bool IsShip(EntityId id) => Sim.Units.Slots.IsAlive(id) && TestWorld.Rules.Sim.Vehicles[Sim.Units.TypeIndex[id.Index]].Locomotion == Locomotion.Naval;

    public void Run(int ticks, Func<bool>? until = null)
    {
        for (int t = 0; t < ticks; t++)
        {
            _driver.Collect(Sim, _pending);
            Sim.Step(_pending);
            _pending.RemoveAll(c => c.ExecTick < Sim.Tick);
            if (until != null && until()) return;
        }
    }

    public byte[]? CaptureAi() => _driver.CaptureState(0);
    public byte[]? CaptureEnemy() => _driver.CaptureState(1);

    public ulong ResumeWithBlob(byte[] blob, int ticks, byte[]? enemyBlob = null)
    {
        _driver = new AiDriver(Sim.Setup.Seed);
        Ai = new RecruitAi(_profile);
        _driver.Add(0, new Recorder(Ai, this), _profile);
        if (Enemy != null) { Enemy = new RecruitAi(_enemyProfile!); _driver.Add(1, Enemy, _enemyProfile!); }
        _driver.RestoreState(0, blob);
        if (Enemy != null && enemyBlob != null) _driver.RestoreState(1, enemyBlob);
        Run(ticks);
        return Sim.ComputeStateHash();
    }

    public int Count(byte player, string buildingId)
    {
        int type = SimRules.IndexOf(TestWorld.Rules.Sim.Buildings, buildingId, b => b.Id), n = 0;
        for (int i = 1; i < Sim.Buildings.Capacity; i++)
            if (Sim.Buildings.Slots.IsIndexAlive(i) && Sim.Buildings.Player[i] == player && Sim.Buildings.TypeIndex[i] == type) n++;
        return n;
    }

    public int Buildings(byte player)
    {
        int n = 0;
        for (int i = 1; i < Sim.Buildings.Capacity; i++)
            if (Sim.Buildings.Slots.IsIndexAlive(i) && Sim.Buildings.Player[i] == player) n++;
        return n;
    }
}
