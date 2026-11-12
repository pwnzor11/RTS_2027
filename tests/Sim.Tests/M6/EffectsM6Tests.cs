// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M6;

/// <summary>M6-06: Ash stealth, Blackout trail, repair and tow auras, storm slow folding, upgrade hook.</summary>
public sealed class EffectsM6Tests
{
    private const int Hull = 0, Medic = 1, Tow = 2, Sail = 3, Rig = 4;          // vehicles (rules order below)
    private const int Sneak = 0, Plain = 1, Tinker = 2;                          // infantry
    private static readonly Command[] None = Array.Empty<Command>();
    private static ushort _seq;

    private static Simulation NewSim(bool allied = false)
    {
        var none = Array.Empty<string>();
        VehicleType V(string id) => new(id, Locomotion.Tracked, 16, 8, 200, ArmorClass.Light, 5, -1, false, 0, false, 0, none);
        var vehicles = new[]
        {
            V("hull"),
            V("medic") with { AuraRadius = 3, AuraRepairPermille = 100 },
            V("tow") with { AuraRadius = 4, TowBonusPermille = 1500 },
            V("sail") with { DropsBlackoutTicks = 50 },
            V("rig"),
        };
        InfantryType I(string id) => new(id, 9, 50, ArmorClass.Unarmored, 4, -1, 0, none);
        var infantry = new[]
        {
            I("sneak") with { Stealth = true, AshStealthDelayTicks = 5, AshStealthThreshold = 3 },
            I("plain") with { Stealth = true },
            I("tinker") with { Engineer = true },
        };
        var buildings = new[] { new BuildingType("hq", 2, 2, 400, ArmorClass.Structure, 5, -1, 0, 0, none, new[] { "hq" }) };
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        var rules = new SimRules(vehicles, infantry, buildings, Array.Empty<WeaponType>(), Array.Empty<WarheadType>(),
            new TerrainTable(speed, buildable), Array.Empty<EffectDef>())
        {
            Upgrades = new[] { new UpgradeType("patch", Faction.Authority, 100, 10, none, "p") { RepairsVehiclesAtRange = true } },
        };
        return new Simulation(new SimSetup(1, MapData.CreateFlat(40, 40), 3, rules, 0x606UL)
        {
            AllowDebug = true,
            Teams = allied ? new byte[] { 1, 1, 2 } : new byte[] { 1, 2, 3 },
        });
    }

    private static void Run(Simulation sim, int ticks, params Command[] first)
    {
        for (int i = 0; i < ticks; i++) sim.Step(i == 0 ? first : None);
    }

    private static Command Move(Simulation sim, EntityId who, int x, int y) =>
        new(sim.Tick, 0, _seq++, CommandKind.Move, who, default, new CellCoord(x, y), (int)ActorKind.Vehicle);

    private static void Damage(Simulation sim, EntityId v, int health) => sim.Units.Health[v.Index] = health;

    // ---- Ash stealth ----------------------------------------------------------------------------

    [Fact]
    public void AshStealth_UsesShorterDelay_OnlyAboveThreshold()
    {
        Simulation sim = NewSim();
        sim.Map.SetOverlay(new CellCoord(5, 5), OverlayKind.Ashfall, 4);   // above threshold 3
        sim.Map.SetOverlay(new CellCoord(8, 5), OverlayKind.Ashfall, 3);   // equal: not above
        EntityId onAsh = sim.SpawnInfantry(0, Sneak, new CellCoord(5, 5));
        EntityId atThreshold = sim.SpawnInfantry(0, Sneak, new CellCoord(8, 5));
        EntityId offAsh = sim.SpawnInfantry(0, Sneak, new CellCoord(12, 5));
        EntityId noRule = sim.SpawnInfantry(0, Plain, new CellCoord(5, 5), 1);
        sim.Infantry.PathGoal[noRule.Index] = new CellCoord(5, 5);   // S19 M13-12: as if ordered there, so it does not walk off the ash
        sim.Map.SetOverlay(new CellCoord(5, 5), OverlayKind.Ashfall, 4);
        Run(sim, 4);
        Assert.False(sim.IsStealthedNow(ActorKind.Infantry, onAsh.Index));
        Run(sim, 2);   // 6 ticks > 5
        Assert.True(sim.IsStealthedNow(ActorKind.Infantry, onAsh.Index));
        Assert.False(sim.IsStealthedNow(ActorKind.Infantry, atThreshold.Index));
        Assert.False(sim.IsStealthedNow(ActorKind.Infantry, offAsh.Index));
        Assert.False(sim.IsStealthedNow(ActorKind.Infantry, noRule.Index));
        Run(sim, 60);   // global delay (60) reached everywhere
        Assert.True(sim.IsStealthedNow(ActorKind.Infantry, atThreshold.Index));
        Assert.True(sim.IsStealthedNow(ActorKind.Infantry, noRule.Index));
    }

    [Fact]
    public void AshStealth_FriendlyStormCountsAsAsh()
    {
        Simulation sim = NewSim();
        EntityId inStorm = sim.SpawnInfantry(0, Sneak, new CellCoord(20, 20));
        EntityId outside = sim.SpawnInfantry(0, Sneak, new CellCoord(30, 20));
        sim.Effects.StormX[0] = 20; sim.Effects.StormY[0] = 20; sim.Effects.StormRadius[0] = 3;
        sim.Effects.StormTicks[0] = 1000; sim.Effects.StormOwner[0] = 0;   // a live storm of player 0 (lead wiring, session 9)
        Run(sim, 6);   // 6 ticks > the 5-tick ash delay, well below the global 60
        Assert.True(sim.IsStealthedNow(ActorKind.Infantry, inStorm.Index));
        Assert.False(sim.IsStealthedNow(ActorKind.Infantry, outside.Index));
    }

    // ---- Blackout trail -------------------------------------------------------------------------

    [Fact]
    public void BlackoutTrail_MovingVehicleLaysCloud_StationaryAndDefaultDoNot()
    {
        Simulation sim = NewSim();
        EntityId sail = sim.SpawnVehicle(0, Sail, new CellCoord(5, 20));
        EntityId idle = sim.SpawnVehicle(0, Sail, new CellCoord(5, 30));
        EntityId hull = sim.SpawnVehicle(0, Hull, new CellCoord(5, 10));
        Run(sim, 3);
        Assert.Equal(0, sim.Ground.BlackoutAt(new CellCoord(5, 30)));
        Run(sim, 1, Move(sim, sail, 20, 20), Move(sim, hull, 20, 10));
        Run(sim, 40);
        int laid = 0;
        for (int x = 5; x <= 20; x++) if (sim.Ground.BlackoutAt(new CellCoord(x, 20)) > 0) laid++;
        Assert.True(laid >= 2, "the sail leaves a trail of several cells");
        Assert.True(sim.Ground.BlackoutAt(sim.Units.Position[sail.Index].ToCell()) > 0);
        Assert.Equal(0, sim.Ground.BlackoutAt(new CellCoord(12, 10)));
        Assert.Equal(0, sim.Ground.BlackoutAt(new CellCoord(5, 30)));
        Assert.NotEqual(idle, sail);
        Run(sim, 100);   // the cloud decays after the vehicle has left (or stopped)
        Assert.Equal(0, sim.Ground.BlackoutAt(new CellCoord(6, 20)));
    }

    [Fact]
    public void BlackoutTrail_RefreshesToMax_NeverShortens()
    {
        Simulation sim = NewSim();
        sim.Ground.Blackout[sim.Ground.IndexOf(new CellCoord(6, 20))] = 900;
        EntityId sail = sim.SpawnVehicle(0, Sail, new CellCoord(5, 20));
        Run(sim, 1, Move(sim, sail, 20, 20));
        Run(sim, 60);
        Assert.True(sim.Ground.BlackoutAt(new CellCoord(6, 20)) > 800);
    }

    // ---- Repair aura ----------------------------------------------------------------------------

    [Fact]
    public void Repair_HealsOwnAndAllied_NotSelfEnemyOrOutOfRange_RadiusInclusive()
    {
        Simulation sim = NewSim(allied: true);   // players 0 and 1 are allies, 2 is the enemy
        EntityId medic = sim.SpawnVehicle(0, Medic, new CellCoord(10, 10));
        EntityId own = sim.SpawnVehicle(0, Hull, new CellCoord(13, 10));      // exactly 3 cells: inside
        EntityId ally = sim.SpawnVehicle(1, Hull, new CellCoord(10, 8));
        EntityId far = sim.SpawnVehicle(0, Hull, new CellCoord(14, 10));      // 4 cells: outside
        EntityId enemy = sim.SpawnVehicle(2, Hull, new CellCoord(11, 10));
        foreach (EntityId v in new[] { medic, own, ally, far, enemy }) Damage(sim, v, 100);
        Run(sim, 1);   // tick 0 is an aura boundary
        Assert.Equal(120, sim.Units.Health[own.Index]);    // 200 * 100 / 1000 = 20
        Assert.Equal(120, sim.Units.Health[ally.Index]);
        Assert.Equal(100, sim.Units.Health[medic.Index]);
        Assert.Equal(100, sim.Units.Health[far.Index]);
        Assert.Equal(100, sim.Units.Health[enemy.Index]);
        Run(sim, 5);
        Assert.Equal(120, sim.Units.Health[own.Index]);    // only every 10 ticks
        Run(sim, 5);
        Assert.Equal(140, sim.Units.Health[own.Index]);
    }

    [Fact]
    public void Repair_CapsAtMax_AndBestSourceWinsWithoutStacking()
    {
        Simulation sim = NewSim();
        sim.SpawnVehicle(0, Medic, new CellCoord(10, 10));
        sim.SpawnVehicle(0, Medic, new CellCoord(10, 12));
        EntityId target = sim.SpawnVehicle(0, Hull, new CellCoord(11, 11));
        Damage(sim, target, 190);
        Run(sim, 1);
        Assert.Equal(200, sim.Units.Health[target.Index]);   // two sources would be +40: capped, and anyway +20 once
        Damage(sim, target, 100);
        Run(sim, 10);
        Assert.Equal(120, sim.Units.Health[target.Index]);   // +20, not +40
    }

    [Fact]
    public void Repair_PackedRigsAreVehiclesAndHeal()
    {
        Simulation sim = NewSim();
        sim.SpawnVehicle(0, Medic, new CellCoord(10, 10));
        EntityId rig = sim.SpawnVehicle(0, Rig, new CellCoord(11, 10));
        sim.Rigs.PackedFrom[rig.Index] = 1;
        Damage(sim, rig, 50);
        Run(sim, 1);
        Assert.Equal(70, sim.Units.Health[rig.Index]);
    }

    [Fact]
    public void Repair_InfantryTinkerGainsRangeFromUpgrade_NotBefore()
    {
        Simulation sim = NewSim();
        sim.SpawnInfantry(0, Tinker, new CellCoord(10, 10));
        EntityId v = sim.SpawnVehicle(0, Hull, new CellCoord(13, 10));
        EntityId far = sim.SpawnVehicle(0, Hull, new CellCoord(10 + Simulation.RangeRepairRadiusCells + 1, 10));
        Damage(sim, v, 100);
        Damage(sim, far, 100);
        Run(sim, 11);
        Assert.Equal(100, sim.Units.Health[v.Index]);                       // no aura of its own, no upgrade: nothing
        sim.Players.Upgrades[0] |= 1UL;
        Run(sim, 10);
        Assert.Equal(100 + Math.Max(1, 200 * Simulation.RangeRepairPermille / 1000), sim.Units.Health[v.Index]);
        Assert.Equal(100, sim.Units.Health[far.Index]);
    }

    [Fact]
    public void Repair_UpgradeWidensExistingAuraRadius()
    {
        Simulation sim = NewSim();
        sim.SpawnVehicle(0, Medic, new CellCoord(10, 10));
        EntityId v = sim.SpawnVehicle(0, Hull, new CellCoord(15, 10));   // 5 cells: beyond 3, inside 3 + 2
        Damage(sim, v, 100);
        Run(sim, 11);
        Assert.Equal(100, sim.Units.Health[v.Index]);
        sim.Players.Upgrades[0] |= 1UL;
        Run(sim, 10);
        Assert.Equal(120, sim.Units.Health[v.Index]);
    }

    // ---- Tow aura -------------------------------------------------------------------------------

    [Fact]
    public void Tow_OnlyPackedRigsInRange_FriendlyOnly()
    {
        Simulation sim = NewSim();
        sim.SpawnVehicle(0, Tow, new CellCoord(10, 10));
        EntityId packed = sim.SpawnVehicle(0, Rig, new CellCoord(13, 10));
        EntityId packedFar = sim.SpawnVehicle(0, Rig, new CellCoord(15, 10));
        EntityId unpacked = sim.SpawnVehicle(0, Rig, new CellCoord(12, 10));
        EntityId packedEnemy = sim.SpawnVehicle(1, Rig, new CellCoord(11, 12));
        EntityId hull = sim.SpawnVehicle(0, Hull, new CellCoord(11, 10));
        foreach (EntityId r in new[] { packed, packedFar, packedEnemy }) sim.Rigs.PackedFrom[r.Index] = 1;
        Run(sim, 1);
        Assert.Equal(1500, sim.Effects.VehicleSpeedPermille[packed.Index]);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[packedFar.Index]);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[unpacked.Index]);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[packedEnemy.Index]);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[hull.Index]);
    }

    // ---- storm folding --------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0, 1000, 0)]       // nothing
    [InlineData(0, 0, 600, 600)]      // storm alone slows
    [InlineData(0, 800, 600, 600)]    // worst debuff wins
    [InlineData(0, 500, 700, 500)]
    [InlineData(1500, 0, 600, 1500)]  // any buff (tow / aura) beats the storm
    [InlineData(1200, 800, 600, 1200)]
    [InlineData(0, 800, 1000, 800)]   // no storm: the aura result is untouched
    public void FoldSpeed_BuffBeatsStorm_WorstDebuffWins(int buff, int debuff, int storm, int expected) =>
        Assert.Equal(expected, Simulation.FoldSpeed(buff, debuff, storm));

    // ---- save / replay --------------------------------------------------------------------------

    [Fact]
    public void SaveLoad_MidAura_ReplaysToTheSameHash()
    {
        Simulation a = Build();
        Run(a, 37);
        var ms = new MemoryStream();
        a.Save(ms);
        ms.Position = 0;
        Simulation b = Simulation.Load(ms, a.Setup);
        Run(a, 60); Run(b, 60);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.Equal(a.Units.Health[2], b.Units.Health[2]);

        Simulation c = Build();
        Run(c, 97);
        Assert.Equal(a.ComputeStateHash(), c.ComputeStateHash());

        static Simulation Build()
        {
            Simulation s = NewSim();
            s.SpawnVehicle(0, Medic, new CellCoord(10, 10));
            EntityId t = s.SpawnVehicle(0, Hull, new CellCoord(11, 10));
            s.Units.Health[t.Index] = 20;
            EntityId sail = s.SpawnVehicle(0, Sail, new CellCoord(5, 20));
            s.SpawnVehicle(0, Tow, new CellCoord(20, 20));
            EntityId rig = s.SpawnVehicle(0, Rig, new CellCoord(22, 20));
            s.Rigs.PackedFrom[rig.Index] = 1;
            s.Step(new[] { Move(s, sail, 18, 20), Move(s, rig, 30, 20) });
            return s;
        }
    }
}
