// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Garrison;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Rules;

namespace Rts.Sim.Tests.M4;

/// <summary>Q-37: Faction.Neutral is the passive owner of ruins and map features. Parse, validation, nobody auto-targets a
/// neutral-owned building, and the garrison cycle keeps the type faction Neutral while the owner slot changes.</summary>
public sealed class NeutralFactionTests
{
    private const byte NeutralSlot = 2;
    private static readonly Command[] None = Array.Empty<Command>();
    private static ushort _seq;

    // ---- enum, parse, data ---------------------------------------------------------------------

    [Fact]
    public void Enum_IsAppendedAsTwo()
    {
        Assert.Equal(0, (int)Faction.Authority);
        Assert.Equal(1, (int)Faction.Directorate);
        Assert.Equal(2, (int)Faction.Neutral);
        Assert.Equal(RuleCounts.Factions, Enum.GetValues<Faction>().Length);
        Assert.Equal(2, RuleCounts.PlayableFactions);
        Assert.Equal(RuleCounts.PlayableFactions, RulesLoader.Load(TestData.RulesDir).Sim.StartKits.Count);
    }

    [Fact]
    public void ShippedRuins_AreNeutral_AndGiveNothing()
    {
        RuleSet rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        BuildingType[] neu = rules.Sim.Buildings.Where(b => b.Id.StartsWith("neu_", StringComparison.Ordinal)).ToArray();
        Assert.True(neu.Length >= 2);
        foreach (BuildingType b in neu)
        {
            Assert.Equal(Faction.Neutral, b.Faction);
            Assert.False(b.Buildable);
            Assert.Empty(b.Prerequisites);
            Assert.Empty(b.Provides);
            Assert.Null(b.Produces);
            Assert.Equal(0, b.Cost);
        }
        Assert.DoesNotContain(rules.Sim.Buildings, b => b.Faction == Faction.Neutral && !b.Id.StartsWith("neu_", StringComparison.Ordinal));
    }

    // ---- validation ----------------------------------------------------------------------------

    private static string Block(string extra = "", string buildable = "false", string prerequisites = "[]", string provides = "[]", string cost = "0") =>
        "\n[test_neutral_ruin]\nfaction = \"neutral\"\nfootprint_w = 2\nfootprint_h = 2\nmax_health = 400\narmor = \"structure\"\nsight = 3\n" +
        $"primary_weapon = \"\"\ncost = {cost}\npower = 0\nprerequisites = {prerequisites}\nprovides = {provides}\nbuildable = {buildable}\n{extra}\n";

    private static RuleSet LoadWith(string buildingsAppend)
    {
        using var temp = new TempRules(("buildings.toml", TempRules.Real("buildings.toml") + buildingsAppend));
        return RulesLoader.Load(temp.Dir);
    }

    [Fact]
    public void ValidNeutralBuilding_Loads_AndFactionNeutralParses()
    {
        RuleSet r = LoadWith(Block());
        Assert.Equal(Faction.Neutral, r.Sim.Buildings.Single(b => b.Id == "test_neutral_ruin").Faction);
    }

    [Theory]
    [InlineData("buildable")]
    [InlineData("prerequisites")]
    [InlineData("provides")]
    [InlineData("cost")]
    [InlineData("produces")]
    public void NeutralBuilding_ThatCanBeBuiltOrGivesSomething_IsRejected(string what)
    {
        string block = what switch
        {
            "buildable" => Block(buildable: "true"),
            "prerequisites" => Block(prerequisites: "[\"test_hq\"]"),
            "provides" => Block(provides: "[\"test_hq\"]"),
            "cost" => Block(cost: "100"),
            _ => Block("produces = \"infantry\""),
        };
        var ex = Assert.Throws<RulesException>(() => LoadWith(block));
        Assert.Contains(ex.Problems, p => p.Key.StartsWith("test_neutral_ruin", StringComparison.Ordinal));
    }

    [Fact]
    public void NeutralVehicle_IsRejected()
    {
        string add = "\n[test_neutral_tank]\nfaction = \"neutral\"\nspeed = 10\nmax_health = 100\narmor = \"light\"\n";
        using var temp = new TempRules(("vehicles.toml", TempRules.Real("vehicles.toml") + add));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "test_neutral_tank.faction" || p.Key.StartsWith("test_neutral_tank", StringComparison.Ordinal));
    }

    [Fact]
    public void NeutralStartKit_IsRejected()
    {
        using var temp = new TempRules(("game.toml", TempRules.Real("game.toml") + "\n[faction.neutral]\nconyard = \"test_hq\"\n"));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "faction.neutral");
    }

    [Fact]
    public void ResolveStartKit_ForNeutral_Throws()
    {
        SimRules rules = RulesLoader.Load(TestData.RulesDir).Sim;
        Assert.Throws<InvalidOperationException>(() => rules.ResolveStartKit(Faction.Neutral));
    }

    [Fact]
    public void SetupHeader_RejectsNeutralAsAPlayerFaction()
    {
        RuleSet rules = RulesLoader.Load(TestData.RulesDir);
        MapData map = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 1)).Item1;
        var files = new[] { ("game.toml", "[game]\ntick_rate = 20\n") };
        Faction[] ok = { Faction.Authority, Faction.Directorate };
        Assert.Equal(ok, RoundTrip(ok));
        Assert.Throws<InvalidDataException>(() => RoundTrip(new[] { Faction.Authority, Faction.Neutral }));

        IReadOnlyList<Faction>? RoundTrip(Faction[] f)
        {
            var sim = new Simulation(new SimSetup(1, map, 2, rules.Sim, rules.RulesHash) { Factions = f });
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) Rts.Sim.Serialization.SetupHeader.From(sim, "t", files).Write(w);
            ms.Position = 0;
            using var r = new BinaryReader(ms);
            return Rts.Sim.Serialization.SetupHeader.Read(r).Factions;
        }
    }

    // ---- targeting and garrison cycle ------------------------------------------------------------

    private static (RuleSet Rules, MapData Map, MapMeta Meta) Generated()
    {
        RuleSet rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        for (ulong seed = 1; seed < 40; seed++)
        {
            var (map, meta) = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.RustBelt, MapSize.Small, 2, seed));
            if (meta.Features.Count >= 2) return (rules, map, meta);
        }
        throw new InvalidOperationException("no generated map with 2 ruins");
    }

    private static (Simulation Sim, EntityId[] Ruins, int Armed) Scene()
    {
        var (rules, map, meta) = Generated();
        var sim = new Simulation(new SimSetup(7, map, 3, rules.Sim, rules.RulesHash) { AllowDebug = true, Teams = new byte[] { 0, 1, Teams.Neutral } });
        EntityId[] ruins = MapFeatures.Place(sim, meta, NeutralSlot);
        int armed = -1;
        for (int i = 0; i < rules.Sim.Infantry.Count && armed < 0; i++)
            if (rules.Sim.Infantry[i].CanGarrison && rules.Sim.Infantry[i].PrimaryWeapon >= 0) armed = i;
        Assert.True(armed >= 0);
        foreach (byte p in new byte[] { 0, 1 })   // a real base each, so neither side is defeated
        {
            int cy = SimRules.IndexOf(rules.Sim.Buildings, "auth_conyard", b => b.Id);
            CellCoord st = map.StartPositions[p];
            for (int r = 0; r <= 8 && sim.PlaceBuilding(p, cy, new CellCoord(st.X - 1, st.Y - 1)).IsNone; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (!sim.PlaceBuilding(p, cy, new CellCoord(st.X + dx - 1, st.Y + dy - 1)).IsNone) goto next;
            next:;
        }
        return (sim, ruins, armed);
    }

    private static EntityId SpawnNear(Simulation sim, byte player, int type, CellCoord around)
    {
        for (int r = 1; r <= 6; r++)
            for (int dx = -r; dx <= r + 2; dx++)
                foreach (int dy in new[] { -r, r + 1 })
                {
                    EntityId id = sim.SpawnInfantry(player, type, new CellCoord(around.X + dx, around.Y + dy));
                    if (!id.IsNone) return id;
                }
        throw new InvalidOperationException("no free cell");
    }

    private static void Run(Simulation sim, int ticks, params Command[] first)
    {
        for (int i = 0; i < ticks; i++) sim.Step(i == 0 ? first : None);
    }

    [Fact]
    public void IdleArmedUnits_NextToANeutralRuin_NeverAttackIt_ButDoAttackAnEnemy()
    {
        var (sim, ruins, armed) = Scene();
        EntityId ruin = ruins[0];
        int ruinHp = sim.Buildings.Health[ruin.Index];
        EntityId shooter = SpawnNear(sim, 0, armed, sim.Buildings.Cell[ruin.Index]);
        EntityId foe = SpawnNear(sim, 1, armed, sim.Infantry.Position[shooter.Index].ToCell());   // control: proves the shooter does auto-fire
        int shooterHp = sim.Infantry.Health[shooter.Index], foeHp = sim.Infantry.Health[foe.Index];
        CellCoord sc = sim.Infantry.Position[shooter.Index].ToCell(), fc = sim.Infantry.Position[foe.Index].ToCell();
        Run(sim, 200,   // Guard = the idle stance that auto-acquires targets
            new Command(sim.Tick, 0, _seq++, CommandKind.Guard, shooter, default, sc, (int)ActorKind.Infantry),
            new Command(sim.Tick, 1, _seq++, CommandKind.Guard, foe, default, fc, (int)ActorKind.Infantry));
        Assert.Equal(ruinHp, sim.Buildings.Health[ruin.Index]);
        Assert.Equal(NeutralSlot, sim.Buildings.Player[ruin.Index]);
        bool fought = !sim.Infantry.Slots.IsAlive(foe) || sim.Infantry.Health[foe.Index] < foeHp
                      || !sim.Infantry.Slots.IsAlive(shooter) || sim.Infantry.Health[shooter.Index] < shooterHp;
        Assert.True(fought, "control failed: idle enemies next to each other must fight");
        Assert.True(ruins.All(r => sim.Buildings.Slots.IsAlive(r) && sim.Buildings.Player[r.Index] == NeutralSlot));
        Assert.NotEqual(Relation.Enemy, sim.RelationOf(0, NeutralSlot));
        Assert.NotEqual(Relation.Enemy, sim.RelationOf(1, NeutralSlot));
    }

    [Fact]
    public void GarrisonCycle_KeepsTheTypeNeutral_WhileTheOwnerChanges()
    {
        var (sim, ruins, armed) = Scene();
        EntityId ruin = ruins[0];
        Faction TypeFaction() => sim.Setup.Rules.Buildings[sim.Buildings.TypeIndex[ruin.Index]].Faction;
        Assert.Equal(Faction.Neutral, TypeFaction());
        Assert.True(sim.Players.IsNeutral(sim.Buildings.Player[ruin.Index]));

        EntityId a = SpawnNear(sim, 0, armed, sim.Buildings.Cell[ruin.Index]);
        Run(sim, 1, new Command(sim.Tick, 0, _seq++, CommandKind.Enter, a, ruin, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8), 0));
        Assert.Equal((byte)0, sim.Buildings.Player[ruin.Index]);                 // taken
        Assert.Equal(Faction.Neutral, TypeFaction());                            // the type is still a neutral feature
        Assert.NotEqual(Faction.Neutral, sim.Players.Faction[sim.Buildings.Player[ruin.Index]]);
        Assert.Equal(NeutralSlot, sim.Garrison.HomeOwner[ruin.Index]);

        Run(sim, 1, new Command(sim.Tick, 0, _seq++, CommandKind.Unload, ruin, default, default, (int)ActorKind.Building, 0));
        Run(sim, 10);
        Assert.Equal(0, sim.OccupantsIn(ruin));                                  // emptied
        Assert.Equal(NeutralSlot, sim.Buildings.Player[ruin.Index]);             // reverted
        Assert.Equal(GarrisonState.NoOwner, sim.Garrison.HomeOwner[ruin.Index]);
        Assert.Equal(Faction.Neutral, TypeFaction());
    }
}
