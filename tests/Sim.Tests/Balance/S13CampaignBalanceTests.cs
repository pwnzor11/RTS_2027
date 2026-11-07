// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Balance;

/// <summary>
/// S13 measurements for the owner (docs/balance/s13_campaign_balance.md): three balance issues the play-test bots found. Every
/// scenario uses fixed seeds and prints numbers; the only asserts say the scenario ran. Candidate changes are in-memory edits of
/// the shipped rules text (<see cref="S13Edit"/>); nothing under data/ is touched (D-152: the owner decides rule numbers).
/// </summary>
public sealed class S13CampaignBalanceTests
{
    private readonly ITestOutputHelper _out;
    public S13CampaignBalanceTests(ITestOutputHelper output) => _out = output;

    private static S13Edit E(string file, string section, string key, string value) => new(file, section, key, value);

    // ---------------------------------------------------------------- A. Ashthrower friendly fire

    private sealed record AshResult(int EnemyDead, int ConsDead, int ConsHpLost, int AshDead, int AshHpLost, int TicksEnemyCleared);

    /// <summary>5 Ashthrowers behind 10 own Conscripts that stand next to 10 enemy Riflemen; everyone attack-moves into the enemy.
    /// With <paramref name="cloudOnly"/> the Rifle does no damage, so every HP the Directorate loses is from the ash cloud.</summary>
    private AshResult Ash(IReadOnlyList<S13Edit> edits, bool cloudOnly, int nAsh = 5, int nCons = 10, int nEnemy = 10, int horizon = 2400)
    {
        var all = new List<S13Edit>(edits);
        if (cloudOnly) all.Add(E("weapons.toml", "auth_rifle", "damage", "0"));
        var a = new S13Arena(all);
        var enemy = a.Infantry(1, "auth_rifleman", nEnemy, new CellCoord(54, 48));
        var cons = a.Infantry(0, "dir_conscript", nCons, new CellCoord(53, 48));
        var ash = a.Infantry(0, "dir_ashthrower", nAsh, new CellCoord(49, 48));
        int consHp0 = cons.Sum(a.InfHealth), ashHp0 = ash.Sum(a.InfHealth);
        var go = a.AttackMoveInf(0, cons.Concat(ash), new CellCoord(58, 48));
        int cleared = -1;
        for (int t = 0; t < horizon; t++)
        {
            a.Step(t == 0 ? go : Array.Empty<Command>());
            if (cleared < 0 && enemy.All(e => !a.InfAlive(e))) cleared = t;
        }
        return new AshResult(enemy.Count(e => !a.InfAlive(e)), cons.Count(c => !a.InfAlive(c)), consHp0 - cons.Sum(a.InfHealth),
            ash.Count(c => !a.InfAlive(c)), ashHp0 - ash.Sum(a.InfHealth), cleared);
    }

    private void PrintAsh(string label, IReadOnlyList<S13Edit> edits, bool big = false)
    {
        (int nAsh, int nCons, int nEnemy) = big ? (10, 25, 20) : (5, 10, 10);
        AshResult real = Ash(edits, false, nAsh, nCons, nEnemy), cloud = Ash(edits, true, nAsh, nCons, nEnemy);
        double perKill = real.EnemyDead == 0 ? double.NaN : (double)real.ConsDead / real.EnemyDead;
        _out.WriteLine($"ASH {(big ? "BIG " : "")}{label,-32} | real fight: enemy dead {real.EnemyDead}/{nEnemy} (cleared t={real.TicksEnemyCleared}), own Conscripts dead {real.ConsDead}/{nCons} (HP lost {real.ConsHpLost}), " +
                       $"own Ashthrowers dead {real.AshDead}/{nAsh}, own deaths per enemy kill {perKill:F2} | cloud only (enemy rifles at 0 damage): " +
                       $"enemy dead {cloud.EnemyDead}/{nEnemy}, Conscripts dead {cloud.ConsDead}/{nCons} (HP lost {cloud.ConsHpLost}/{nCons * 40}), Ashthrowers dead {cloud.AshDead}/{nAsh} (HP lost {cloud.AshHpLost}/{nAsh * 50})");
        Assert.True(real.EnemyDead >= 0 && cloud.EnemyDead >= 0);
    }

    [Fact]
    public void Ashthrower_FriendlyFire_NowAndCandidates()
    {
        PrintAsh("now (contaminate 40)", Array.Empty<S13Edit>());
        PrintAsh("contaminate 20", new[] { E("warheads.toml", "dir_ash", "contaminate", "20") });
        PrintAsh("contaminate 0 (no cloud)", new[] { E("warheads.toml", "dir_ash", "contaminate", "0") });
        PrintAsh("Directorate infantry immune to ash",
            new[] { E("infantry.toml", "dir_conscript", "rad_resist_permille", "1000"), E("infantry.toml", "dir_ashthrower", "rad_resist_permille", "1000") });
        PrintAsh("Conscript+Ashthrower resist 600->800",
            new[] { E("infantry.toml", "dir_conscript", "rad_resist_permille", "800"), E("infantry.toml", "dir_ashthrower", "rad_resist_permille", "800") });
        PrintAsh("now (contaminate 40)", Array.Empty<S13Edit>(), true);
        PrintAsh("contaminate 20", new[] { E("warheads.toml", "dir_ash", "contaminate", "20") }, true);
        PrintAsh("contaminate 0 (no cloud)", new[] { E("warheads.toml", "dir_ash", "contaminate", "0") }, true);
    }

    // ---------------------------------------------------------------- B. Bomber vs Expedition depot with SAMs

    private sealed record BomberResult(int Sams, int DepotKillTick, int BomberDeathTick, int SamShots, int HitEvents, int FirstHit, int BomberHpLeft, int DepotHpLeft, int Ticks);

    private BomberResult Bomber(IReadOnlyList<S13Edit> edits, int sams, bool powered, int horizon = 4000)
    {
        // "Unpowered" = the keep-alive power plant makes nothing, so the SAM Mounts (power use 10 each) are in a power shortage and hold fire.
        var a = new S13Arena(powered || sams == 0 ? edits : edits.Concat(new[] { E("buildings.toml", "auth_power", "power", "0") }).ToList());
        EntityId depot = a.Sim.PlaceBuilding(1, a.Building("exp_landing_depot"), new CellCoord(60, 48));
        Assert.False(depot.IsNone);
        if (powered) Assert.False(a.Sim.PlaceBuilding(1, a.Building("exp_pile_house"), new CellCoord(66, 50)).IsNone);   // power for the SAM Mounts (they hold fire without it)
        var spots = new[] { new CellCoord(57, 47), new CellCoord(64, 47), new CellCoord(61, 52) };
        for (int s = 0; s < sams; s++) Assert.False(a.Sim.PlaceBuilding(1, a.Building("exp_sam"), spots[s]).IsNone);
        EntityId bomber = a.Sim.SpawnVehicle(0, a.Veh("auth_bomber"), new CellCoord(30, 49));
        int hp0 = a.Sim.Units.Health[bomber.Index], lastHp = hp0, hits = 0, firstHit = 0, depotKill = -1, deathTick = -1, shots = 0, ticks = 0;
        var seen = new HashSet<EntityId>();
        var attack = a.Cmd(0, CommandKind.Attack, bomber, depot, default, (int)ActorKind.Vehicle | ((int)ActorKind.Building << 8));
        for (int t = 0; t < horizon; t++)
        {
            a.Step(t == 0 ? new[] { attack } : Array.Empty<Command>());
            ticks = t + 1;
            for (int p = 1; p < a.Sim.Projectiles.Capacity; p++)
                if (a.Sim.Projectiles.Slots.IsIndexAlive(p) && a.Sim.Projectiles.Owner[p] == 1 && seen.Add(a.Sim.Projectiles.Slots.IdOf(p))) shots++;
            bool bomberAlive = a.Sim.Units.Slots.IsAlive(bomber) && a.Sim.Units.Health[bomber.Index] > 0;
            int hp = bomberAlive ? a.Sim.Units.Health[bomber.Index] : 0;
            if (hp < lastHp) { hits++; if (firstHit == 0) firstHit = lastHp - hp; lastHp = hp; }
            if (!bomberAlive && deathTick < 0) { deathTick = t; break; }
            if (!a.Sim.Buildings.Slots.IsAlive(depot) || a.Sim.Buildings.Health[depot.Index] <= 0) { depotKill = t; break; }
        }
        int depotHp = a.Sim.Buildings.Slots.IsAlive(depot) ? Math.Max(0, a.Sim.Buildings.Health[depot.Index]) : 0;
        int bhp = a.Sim.Units.Slots.IsAlive(bomber) ? Math.Max(0, a.Sim.Units.Health[bomber.Index]) : 0;
        return new BomberResult(sams, depotKill, deathTick, shots, hits, firstHit, bhp, depotHp, ticks);
    }

    private void PrintBomber(string label, IReadOnlyList<S13Edit> edits)
    {
        foreach ((int sams, bool powered) in new[] { (0, true), (1, true), (3, true), (3, false) })
        {
            BomberResult r = Bomber(edits, sams, powered);
            _out.WriteLine($"BOMBER {label,-28} | SAMs {sams}{(sams > 0 && !powered ? " UNPOWERED" : "")}: depot dead at tick {(r.DepotKillTick < 0 ? "never" : r.DepotKillTick.ToString())}, " +
                           $"bomber {(r.BomberDeathTick < 0 ? "survives" : "shot down at tick " + r.BomberDeathTick)} (hp left {r.BomberHpLeft}), SAM shots {r.SamShots}, " +
                           $"hit events {r.HitEvents}, damage first hit {r.FirstHit}, depot hp left {r.DepotHpLeft}, ran {r.Ticks} ticks");
            Assert.True(r.Ticks > 0);
        }
    }

    [Fact]
    public void Bomber_VsDepotWithSams_NowAndCandidates()
    {
        PrintBomber("now", Array.Empty<S13Edit>());
        PrintBomber("SAM needs_power false", new[] { E("buildings.toml", "exp_sam", "needs_power", "false") });
        PrintBomber("SAM damage 45 to 25", new[] { E("weapons.toml", "exp_sam_missile", "damage", "25") });
        PrintBomber("SAM damage 45 to 90", new[] { E("weapons.toml", "exp_sam_missile", "damage", "90") });
        PrintBomber("depot hp 1000 to 1500", new[] { E("buildings.toml", "exp_landing_depot", "max_health", "1500") });
        PrintBomber("bomber hp 200 to 100", new[] { E("vehicles.toml", "auth_bomber", "max_health", "100") });
    }

    // ---------------------------------------------------------------- C. Conscripts vs Riflemen

    private sealed record FightResult(string Winner, int ConsLeft, int RifLeft, int ConsHpLeft, int RifHpLeft, int Ticks);

    private FightResult Fight(IReadOnlyList<S13Edit> edits, int nCons, int nRif, bool swapSides, int horizon = 4000)
    {
        var a = new S13Arena(edits);
        byte consP = (byte)(swapSides ? 1 : 0), rifP = (byte)(swapSides ? 0 : 1);
        int consX = swapSides ? 56 : 40, rifX = swapSides ? 40 : 56;
        var cons = a.Infantry(consP, "dir_conscript", nCons, new CellCoord(consX, 48));
        var rif = a.Infantry(rifP, "auth_rifleman", nRif, new CellCoord(rifX, 48));
        var go = a.AttackMoveInf(consP, cons, new CellCoord(rifX, 48)).Concat(a.AttackMoveInf(rifP, rif, new CellCoord(consX, 48))).ToArray();
        int ticks = 0;
        for (int t = 0; t < horizon; t++)
        {
            a.Step(t == 0 ? go : Array.Empty<Command>());
            ticks = t + 1;
            if (cons.All(c => !a.InfAlive(c)) || rif.All(r => !a.InfAlive(r))) break;
        }
        int cl = cons.Count(a.InfAlive), rl = rif.Count(a.InfAlive);
        return new FightResult(cl == 0 && rl == 0 ? "both wiped" : cl == 0 ? "Riflemen" : rl == 0 ? "Conscripts" : $"undecided ({(cl > rl ? "Conscripts" : "Riflemen")} ahead)", cl, rl, cons.Sum(a.InfHealth), rif.Sum(a.InfHealth), ticks);
    }

    private void PrintFight(string label, IReadOnlyList<S13Edit> edits)
    {
        S13Arena probe = new(edits);
        int consCost = probe.InfCost("dir_conscript"), rifCost = probe.InfCost("auth_rifleman");
        foreach (int n in new[] { 5, 10 })
        {
            FightResult r = Fight(edits, n, n, false), s = Fight(edits, n, n, true);
            _out.WriteLine($"FIGHT {label,-26} | {n}v{n} equal count: Conscripts left {r.ConsLeft} Riflemen left {r.RifLeft} -> {r.Winner} at tick {r.Ticks} (sides swapped: {s.Winner}, {s.ConsLeft} v {s.RifLeft})");
            Assert.True(r.Ticks > 0);
        }
        foreach (int rifles in new[] { 4, 20 })
        {
            int cons = (int)Math.Round(rifles * (double)rifCost / consCost);
            FightResult r = Fight(edits, cons, rifles, false), s = Fight(edits, cons, rifles, true);
            _out.WriteLine($"FIGHT {label,-26} | equal cost {rifles * rifCost} ({cons} Conscripts @{consCost} v {rifles} Riflemen @{rifCost}): Conscripts left {r.ConsLeft} Riflemen left {r.RifLeft} (hp {r.ConsHpLeft} v {r.RifHpLeft}) -> {r.Winner} at tick {r.Ticks} (sides swapped: {s.Winner}, {s.ConsLeft} v {s.RifLeft})");
            Assert.True(r.Ticks > 0);
        }
    }

    [Fact]
    public void ConscriptsVsRiflemen_NowAndCandidates()
    {
        PrintFight("now (hp 40, cost 80)", Array.Empty<S13Edit>());
        PrintFight("Conscript hp 50", new[] { E("infantry.toml", "dir_conscript", "max_health", "50") });
        PrintFight("Conscript hp 55", new[] { E("infantry.toml", "dir_conscript", "max_health", "55") });
        PrintFight("Conscript cost 60", new[] { E("infantry.toml", "dir_conscript", "cost", "60") });
        PrintFight("Conscript hp 50 + cost 70", new[] { E("infantry.toml", "dir_conscript", "max_health", "50"), E("infantry.toml", "dir_conscript", "cost", "70") });
    }
}
