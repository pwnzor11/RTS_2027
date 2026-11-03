using Rts.Sim.Core;
using Rts.Sim.Missions;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Players;

/// <summary>Player relations (D-054): RelationOf, auto-targeting, explicit attacks, splash, shared vision.</summary>
public class RelationTests
{
    private const byte N = Teams.Neutral;

    // Players 0 and 1 are allies (team 0), 2 is an enemy (team 1), 3 is neutral.
    private static Simulation Teamed(int size = 32, params byte[] teams) =>
        new(new SimSetup(1, Rts.Sim.Map.MapData.CreateFlat(size, size), teams.Length, Loaded.Sim, Loaded.RulesHash) { Teams = teams });

    private static Simulation Standard() => Teamed(32, 0, 0, 1, N);

    private static void Guard0(Simulation sim, byte player, EntityId id, int x, int y) =>
        sim.AssignMission(new ActorRef(ActorKind.Infantry, id), MissionKind.Guard, new CellCoord(x, y));

    [Fact]
    public void RelationOf_Table()
    {
        var sim = Standard();
        Assert.Equal(Relation.Self, sim.RelationOf(0, 0));
        Assert.Equal(Relation.Self, sim.RelationOf(3, 3));   // a neutral slot is still Self to itself
        Assert.Equal(Relation.Ally, sim.RelationOf(0, 1));
        Assert.Equal(Relation.Ally, sim.RelationOf(1, 0));
        Assert.Equal(Relation.Enemy, sim.RelationOf(0, 2));
        Assert.Equal(Relation.Enemy, sim.RelationOf(2, 1));
        Assert.Equal(Relation.Neutral, sim.RelationOf(0, 3));
        Assert.Equal(Relation.Neutral, sim.RelationOf(3, 2));
    }

    [Fact]
    public void RelationOf_TeamsNull_EveryoneElseIsEnemy()
    {
        var sim = NewSim();
        Assert.Equal(Relation.Self, sim.RelationOf(0, 0));
        Assert.Equal(Relation.Enemy, sim.RelationOf(0, 1));
        Assert.Equal(Relation.Enemy, sim.RelationOf(1, 0));
    }

    [Fact]
    public void Allies_DoNotAutoTargetEachOther_ButEnemiesAreTargeted()
    {
        var sim = Standard();
        var a = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        var ally = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(12, 10));
        var enemy = sim.SpawnInfantry(2, Inf("test_rifleman"), new CellCoord(10, 12));
        Guard0(sim, 0, a, 10, 10);
        Run(sim, 80);
        Assert.Equal(50, sim.Infantry.Health[ally.Index]);
        Assert.True(sim.Infantry.Health[enemy.Index] < 50);
    }

    [Fact]
    public void ExplicitAttack_OnAlly_IsRejected_OnEnemyAndNeutralAccepted()
    {
        var sim = Standard();
        var a = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        var ally = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(11, 10));
        var self = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(14, 10));
        var neutral = sim.SpawnInfantry(3, Inf("test_rifleman"), new CellCoord(10, 12));
        Run(sim, 40, Attack(sim, 0, ActorKind.Infantry, a, ActorKind.Infantry, ally));
        Assert.Equal(50, sim.Infantry.Health[ally.Index]);
        Assert.NotEqual(MissionKind.Attack, sim.Infantry.Mission[a.Index]);

        Run(sim, 20, Attack(sim, 0, ActorKind.Infantry, a, ActorKind.Infantry, self));
        Assert.Equal(50, sim.Infantry.Health[self.Index]);
        Assert.Equal(50, sim.Infantry.Health[neutral.Index]);   // neutral is never auto-targeted

        Run(sim, 40, Attack(sim, 0, ActorKind.Infantry, a, ActorKind.Infantry, neutral));
        Assert.True(sim.Infantry.Health[neutral.Index] < 50);
    }

    [Fact]
    public void Splash_WithoutHitsAllies_SparesSelfAndAllies_HurtsNeutral()
    {
        var sim = Standard();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var enemy = sim.SpawnInfantry(2, Inf("test_rifleman"), new CellCoord(10, 6), 1);
        var own = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 6), 2);
        var ally = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 6), 3);
        var neutral = sim.SpawnInfantry(3, Inf("test_rifleman"), new CellCoord(10, 6), 4);
        Run(sim, 40, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, enemy));
        Assert.Equal(40, sim.Infantry.Health[enemy.Index]);
        Assert.Equal(50, sim.Infantry.Health[own.Index]);
        Assert.Equal(50, sim.Infantry.Health[ally.Index]);
        Assert.Equal(40, sim.Infantry.Health[neutral.Index]);
    }

    [Fact]
    public void Ally_SeesWhatPartnerSees_NonAllyDoesNot()
    {
        var sim = Standard();
        sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 10));
        sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(28, 28));
        var enemy = sim.SpawnInfantry(2, Inf("test_rifleman"), new CellCoord(13, 10));
        sim.SpawnInfantry(3, Inf("test_rifleman"), new CellCoord(28, 2));
        Run(sim, 2);
        Assert.Contains(sim.Snapshot(0).Units, u => u.Id == enemy);   // via partner 1
        Assert.Contains(sim.Snapshot(1).Units, u => u.Id == enemy);
        Assert.DoesNotContain(sim.Snapshot(3).Units, u => u.Id == enemy);   // neutral shares with nobody
        Assert.Equal(2, sim.Snapshot(0).Vision[10 * 32 + 13]);
        Assert.Equal(0, sim.Snapshot(3).Vision[10 * 32 + 13]);

        var apart = Teamed(32, 0, 1, 2);
        apart.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 10));
        apart.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(28, 28));
        var e2 = apart.SpawnInfantry(2, Inf("test_rifleman"), new CellCoord(13, 10));
        Run(apart, 2);
        Assert.DoesNotContain(apart.Snapshot(0).Units, u => u.Id == e2);
    }

    [Fact]
    public void NeutralBuilding_NotAutoTargeted_NorAutoTargeting_ButAttackableExplicitly()
    {
        var sim = Standard();
        var shooter = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        var bld = sim.PlaceBuilding(3, Bld("test_barracks"), new CellCoord(12, 10));
        var turret = sim.PlaceBuilding(3, Bld("test_turret"), new CellCoord(10, 12));
        Assert.False(bld.IsNone);
        Assert.False(turret.IsNone);
        Guard0(sim, 0, shooter, 10, 10);
        Run(sim, 80);
        Assert.Equal(sim.Setup.Rules.Buildings[Bld("test_barracks")].MaxHealth, sim.Buildings.Health[bld.Index]);
        Assert.Equal(sim.Setup.Rules.Buildings[Bld("test_turret")].MaxHealth, sim.Buildings.Health[turret.Index]);
        Assert.Equal(50, sim.Infantry.Health[shooter.Index]);   // the neutral turret never fired

        Run(sim, 60, Attack(sim, 0, ActorKind.Infantry, shooter, ActorKind.Building, bld));
        Assert.True(sim.Buildings.Health[bld.Index] < sim.Setup.Rules.Buildings[Bld("test_barracks")].MaxHealth);
    }

    [Fact]
    public void TeamsNull_HashesExactlyAsDistinctTeams()
    {
        ulong Play(IReadOnlyList<byte>? teams)
        {
            var sim = new Simulation(new SimSetup(7, Rts.Sim.Map.MapData.CreateFlat(32, 32), 2, Loaded.Sim, Loaded.RulesHash) { Teams = teams });
            var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(5, 5));
            var inf = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(9, 5));
            var guard = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(20, 20));
            sim.PlaceBuilding(1, Bld("test_turret"), new CellCoord(12, 8));
            sim.PlaceBuilding(0, Bld("test_barracks"), new CellCoord(3, 12));
            Run(sim, 30, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, inf));
            Run(sim, 20, Move(sim, 0, ActorKind.Vehicle, tank, 14, 9));
            Run(sim, 60, AttackMove(sim, 1, ActorKind.Infantry, guard, 6, 6));
            return sim.ComputeStateHash();
        }
        Assert.Equal(Play(null), Play(new byte[] { 0, 1 }));
    }
}
