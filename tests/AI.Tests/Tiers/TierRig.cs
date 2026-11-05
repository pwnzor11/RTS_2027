// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace AI.Tests.Tiers;

/// <summary>A one-AI test game: slot 0 plays <see cref="Ai"/> on a flat map, slot 1 has no AI (it only holds what the test gives it).
/// Setup callbacks run before the first Step (spawns and structures are allowed then); Debug commands work afterwards.</summary>
internal sealed class TierRig
{
    /// <summary>Counts what an AI issued, by command kind (Build / Place / Rally included).</summary>
    private sealed class Recorder(IAiPlayer inner, Dictionary<CommandKind, int> counts) : IAiPlayer, IAiSnapshotable
    {
        public void SaveState(BinaryWriter w) => ((IAiSnapshotable)inner).SaveState(w);
        public void LoadState(BinaryReader r) => ((IAiSnapshotable)inner).LoadState(r);
        public string Id => inner.Id;
        public void Start(AiContext ctx) => inner.Start(ctx);
        public void Think(PlayerView view, ICommandSink sink) => inner.Think(view, new Sink(counts, sink));

        private sealed class Sink(Dictionary<CommandKind, int> counts, ICommandSink real) : ICommandSink
        {
            private void Count(CommandKind k) => counts[k] = counts.GetValueOrDefault(k) + 1;
            public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default, ActorRef target = default, int arg2 = 0)
            { Count(kind); real.Issue(kind, subject, subjectKind, cell, target, arg2); }
            public void Build(ProductionCategory category, int typeIndex) { Count(CommandKind.Build); real.Build(category, typeIndex); }
            public void Place(int buildingType, CellCoord topLeft) { Count(CommandKind.Place); real.Place(buildingType, topLeft); }
            public void Rally(EntityId factory, CellCoord cell) { Count(CommandKind.Rally); real.Rally(factory, cell); }
        }
    }

    private static ushort _seq = 50000;
    private readonly List<Command> _pending = new();
    private AiDriver _driver;
    private readonly DifficultyProfile _profile;

    public Simulation Sim { get; }
    public RecruitAi Ai { get; private set; }
    public Dictionary<CommandKind, int> Issued { get; } = new();
    public RuleSet World => TestWorld.Rules;

    public TierRig(Faction faction, DifficultyProfile profile, Action<TierRig>? setup = null, Faction enemy = Faction.Authority, int size = 64, ulong seed = 1)
    {
        Sim = new Simulation(new SimSetup(seed, MapGenerator.Generate(size, seed), 2, World.Sim, World.RulesHash)
        { Factions = [faction, enemy], AllowDebug = true });
        SkirmishStart.Apply(Sim);
        setup?.Invoke(this);
        _profile = profile;
        _driver = new AiDriver(seed);
        Ai = new RecruitAi(profile);
        _driver.Add(0, new Recorder(Ai, Issued), profile);
    }

    /// <summary>M6-10: the AI memory of slot 0 as a save would hold it.</summary>
    public byte[]? CaptureAi() => _driver.CaptureState(0);

    /// <summary>M6-10: replaces the AI by a fresh one that starts from <paramref name="blob"/> (the sim keeps running), runs <paramref name="ticks"/> more and returns the state hash.</summary>
    public ulong ResumeWithBlob(byte[] blob, int ticks)
    {
        _driver = new AiDriver(Sim.Setup.Seed);
        Ai = new RecruitAi(_profile);
        _driver.Add(0, new Recorder(Ai, Issued), _profile);
        _driver.RestoreState(0, blob);
        Run(ticks);
        return Sim.ComputeStateHash();
    }

    public int Issue(CommandKind k) => Issued.GetValueOrDefault(k);

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

    /// <summary>Queues a Debug kill for the next tick.</summary>
    public void Kill(EntityId id, ActorKind kind) =>
        _pending.Add(new Command(Sim.Tick, 1, _seq++, CommandKind.Debug, id, default, default, (int)DebugOp.Kill, (int)kind));

    // ---- queries ----------------------------------------------------------------------------------

    public int VehicleType(string id) => SimRules.IndexOf(World.Sim.Vehicles, id, v => v.Id);
    public int InfantryType(string id) => SimRules.IndexOf(World.Sim.Infantry, id, v => v.Id);
    public int BuildingType(string id) => SimRules.IndexOf(World.Sim.Buildings, id, v => v.Id);

    public List<EntityId> Harvesters(byte player)
    {
        var list = new List<EntityId>();
        for (int i = 1; i < Sim.Units.Capacity; i++)
            if (Sim.Units.Slots.IsIndexAlive(i) && Sim.Units.Player[i] == player && World.Sim.Vehicles[Sim.Units.TypeIndex[i]].HarvestCapacity > 0)
                list.Add(Sim.Units.Slots.IdOf(i));
        return list;
    }

    public int Count(byte player, string buildingId)
    {
        int type = BuildingType(buildingId), n = 0;
        for (int i = 1; i < Sim.Buildings.Capacity; i++)
            if (Sim.Buildings.Slots.IsIndexAlive(i) && Sim.Buildings.Player[i] == player && Sim.Buildings.TypeIndex[i] == type) n++;
        return n;
    }

    public CellCoord Yard(byte player)
    {
        for (int i = 1; i < Sim.Buildings.Capacity; i++)
            if (Sim.Buildings.Slots.IsIndexAlive(i) && Sim.Buildings.Player[i] == player) return Sim.Buildings.Cell[i];
        throw new InvalidOperationException("player has no building");
    }

    public static string HarvesterId(Faction f) => f == Faction.Directorate ? "dir_scraper" : "auth_scraper";
    public static string FootId(Faction f) => f == Faction.Directorate ? "dir_conscript" : "auth_rifleman";
    public static string RefineryId(Faction f) => f == Faction.Directorate ? "dir_smelter_vent" : "auth_refinery";

    // ---- setup helpers (before the first Step) ------------------------------------------------------

    /// <summary>Places a structure on the first legal cell found in rings around <paramref name="near"/>; fails the test when none is.</summary>
    public EntityId PlaceNear(byte player, string buildingId, CellCoord near)
    {
        int type = BuildingType(buildingId);
        for (int r = 0; r <= 14; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(near.X + dx, near.Y + dy);
                    if (c.X < 1 || c.Y < 1 || c.X > Sim.Setup.Map.Width - 5 || c.Y > Sim.Setup.Map.Height - 5) continue;
                    EntityId id = Sim.PlaceBuilding(player, type, c);
                    if (!id.IsNone) return id;
                }
        throw new InvalidOperationException("no room for " + buildingId);
    }

    /// <summary>Spawns <paramref name="count"/> foot soldiers of the faction on free cells in rings around <paramref name="near"/>.</summary>
    public void SpawnFoot(byte player, Faction faction, int count, CellCoord near)
    {
        int type = InfantryType(FootId(faction));
        int spawned = 0;
        for (int r = 0; r <= 12 && spawned < count; r++)
            for (int dy = -r; dy <= r && spawned < count; dy++)
                for (int dx = -r; dx <= r && spawned < count; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(near.X + dx, near.Y + dy);
                    if (!Sim.Setup.Map.Contains(c) || !Sim.Map.BuildingAt(c).IsNone) continue;
                    if (!Sim.SpawnInfantry(player, type, c).IsNone) spawned++;
                }
        Assert.Equal(count, spawned);
    }

    public void SpawnHarvester(byte player, Faction faction, CellCoord near)
    {
        int type = VehicleType(HarvesterId(faction));
        for (int r = 4; r <= 12; r++)
            for (int dx = -r; dx <= r; dx++)
            {
                var c = new CellCoord(near.X + dx, near.Y + r);
                if (Sim.Setup.Map.Contains(c) && Sim.Map.BuildingAt(c).IsNone && !Sim.SpawnVehicle(player, type, c).IsNone) return;
            }
        throw new InvalidOperationException("no room for a harvester");
    }
}
