// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.M8_15;

/// <summary>What one bot walk of a mission produced (M8-15).</summary>
/// <param name="Mission">Mission id.</param>
/// <param name="Tier">Difficulty tier the walk ran at.</param>
/// <param name="Won">The mission outcome is Won and every primary objective is completed.</param>
/// <param name="Tick">Sim tick at which the walk ended (the win tick when Won).</param>
/// <param name="PlayerLosses">Units and structures the human lost.</param>
/// <param name="EnemyLosses">Units and structures the enemies lost.</param>
/// <param name="Failure">Why a step of the walk gave up, or null.</param>
/// <param name="Errors">Script errors.</param>
/// <param name="LocaleWarnings">Number of locale warnings of the mission.</param>
/// <param name="Hash">Final state hash.</param>
/// <param name="Diagnostics">One-line state dump.</param>
internal sealed record WalkResult(string Mission, int Tier, bool Won, int Tick, int PlayerLosses, int EnemyLosses, string? Failure,
    IReadOnlyList<string> Errors, int LocaleWarnings, ulong Hash, string Diagnostics);

/// <summary>A step of a campaign walk could not be driven (the walk stops; the result records why).</summary>
internal sealed class WalkFailure : Exception
{
    public WalkFailure(string message) : base(message) { }
}

/// <summary>
/// M8-15: the winning bot walk of every campaign mission as one reusable method. A walk only talks to the world through the
/// <see cref="MissionBot"/> (never a cached simulation), so a test can save and load the game under it
/// (<see cref="MissionBot.BeforeTick"/> + <see cref="MissionBot.Rebind"/>). The steps are the ones of the per-mission test files.
/// </summary>
internal static class CampaignWalks
{
    /// <summary>The four campaigns in campaign order (data/campaigns/*.toml).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Campaigns = new Dictionary<string, string[]>
    {
        ["authority"] = new[] { "auth_01", "auth_02", "auth_03", "auth_04" },
        ["directorate"] = new[] { "dir_01", "dir_02", "dir_03", "dir_04" },
        ["left_behind"] = new[] { "lb_01", "lb_02", "lb_03", "lb_04" },
        ["expedition"] = new[] { "exp_01", "exp_02", "exp_03", "exp_04" },
    };

    /// <summary>All 16 mission ids, campaign by campaign.</summary>
    public static IEnumerable<string> AllMissions => Campaigns.Values.SelectMany(x => x);

    /// <summary>xunit data: one row per mission id.</summary>
    public static IEnumerable<object[]> AllMissionRows => AllMissions.Select(x => new object[] { x });

    /// <summary>The design tick limit of a mission: the bot must win before this absolute tick (the limit its own test file asserts).</summary>
    public static int TickLimit(string id) => id switch
    {
        "auth_01" => 14000,
        "auth_04" => 16000,
        "dir_01" or "exp_01" or "lb_01" => 20000,
        _ => 30000,
    };

    /// <summary>The tiers the per-mission test supports for this mission's walk.</summary>
    public static int[] Tiers(string id) => id switch
    {
        "auth_01" or "auth_02" or "auth_03" or "auth_04" or "dir_01" or "exp_01" => new[] { 3, 5 },
        _ => new[] { 1, 3, 5 },
    };

    /// <summary>
    /// Plays <paramref name="missionId"/> at <paramref name="tier"/> on the fresh session <paramref name="m"/> with the mission's winning
    /// bot walk. <paramref name="beforeTick"/> is installed as <see cref="MissionBot.BeforeTick"/>. Never throws for a walk that cannot be
    /// driven: the reason is in <see cref="WalkResult.Failure"/>.
    /// </summary>
    public static WalkResult Play(string missionId, int tier, MissionSession m, Action<MissionBot>? beforeTick = null)
    {
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m) { BeforeTick = beforeTick };
        string? failure = null;
        try
        {
            switch (missionId)
            {
                case "auth_01": Auth01(bot); break;
                case "auth_02": Auth02(bot); break;
                case "auth_03": Auth03(bot); break;
                case "auth_04": Auth04(bot); break;
                case "dir_01": Dir01(bot); break;
                case "dir_02": Dir02(bot); break;
                case "dir_03": Dir03(bot); break;
                case "dir_04": Dir04(bot); break;
                case "lb_01": Lb01(bot); break;
                case "lb_02": Lb02(bot); break;
                case "lb_03": Lb03(bot); break;
                case "lb_04": Lb04(bot); break;
                case "exp_01": Exp01(bot); break;
                case "exp_02": Exp02(bot); break;
                case "exp_03": Exp03(bot); break;
                case "exp_04": Exp04(bot); break;
                default: throw new ArgumentException("no walk for " + missionId, nameof(missionId));
            }
        }
        catch (WalkFailure f) { failure = f.Message; }

        Simulation s = bot.Sim;
        MissionHost host = bot.Host;
        bool primaryDone = host.Objectives.Where(o => o.Primary).All(o => o.Status == ObjectiveStatus.Completed);
        bool won = host.Outcome == MissionOutcome.Won && primaryDone && s.WinningTeam == 0;
        int enemyLosses = 0;
        for (byte p = 1; p < s.Players.Count; p++)
            if (s.RelationOf(0, p) == Relation.Enemy)
            {
                PlayerStats st = s.StatsOf(p);
                enemyLosses += st.UnitsLost + st.StructuresLost;
            }
        PlayerStats me = s.StatsOf(0);
        return new WalkResult(missionId, tier, won, s.Tick, me.UnitsLost + me.StructuresLost, enemyLosses, failure, host.Errors.ToArray(),
            m.Def.Locale.Warnings.Count, s.ComputeStateHash(), bot.Diagnostics());
    }

    private static void Need(bool ok, MissionBot bot, string what)
    {
        if (!ok) throw new WalkFailure(what + ": " + bot.Diagnostics());
    }

    // ---------------------------------------------------------------- Authority

    private static void Auth01(MissionBot bot)
    {
        var site = new CellCoord(56, 6);
        bot.OrderEveryone(CommandKind.AttackMove, site);
        int queuedPower = -1, queuedRefinery = -1;
        for (int guard = 0; guard < 14000 && !bot.Sim.GameOver; guard++)
        {
            Simulation sim = bot.Sim;
            bool build = bot.Host.Objectives.Any(o => o.Id == "refinery");
            if (build && queuedPower < 0) { bot.Train("auth_power"); queuedPower = sim.Tick; continue; }
            if (build && sim.Tick % 20 == 0 && queuedPower >= 0 && OwnBuildings(sim) < 2) { bot.TryPlace("auth_power", new CellCoord(27, 25)); continue; }
            if (build && OwnBuildings(sim) >= 2 && queuedRefinery < 0) { bot.Train("auth_refinery"); queuedRefinery = sim.Tick; continue; }
            if (build && queuedRefinery >= 0 && sim.Tick % 20 == 0) { bot.TryPlace("auth_refinery", new CellCoord(23, 29)); continue; }
            if (guard % 100 == 0 && !build) { bot.OrderEveryone(CommandKind.AttackMove, site); continue; }
            bot.Idle();
        }
    }

    private static int OwnBuildings(Simulation sim)
    {
        int n = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == 0) n++;
        return n;
    }

    private static void Auth02(MissionBot bot)
    {
        const int limit = 30000;
        var turretSpots = new CellCoord[]
        {
            new(37, 17), new(37, 20), new(36, 18), new(36, 20), new(37, 18), new(37, 19), new(36, 19), new(36, 15), new(37, 16),
        };
        Need(bot.BuildAndPlace("auth_turret", turretSpots, 600), bot, "first turret");
        Need(bot.BuildAndPlace("auth_turret", turretSpots, 600), bot, "second turret");
        for (int i = 0; i < 6; i++) bot.Train("auth_rifleman");
        bot.Train("auth_tank");
        bot.Train("auth_tank");
        for (int t = 0; t < limit && !bot.Sim.GameOver; t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, new CellCoord(39, 18));
            if (t % 600 == 0) bot.Train("auth_rifleman");
            bot.Idle(99);
        }
    }

    private static void Auth03(MissionBot bot)
    {
        const int limit = 30000;
        var turretSpots = new CellCoord[] { new(19, 13), new(19, 15), new(18, 17), new(18, 11), new(19, 17), new(19, 12), new(18, 18) };

        // a strike force and two Turrets first, then the convoy is every unit that exists; later units stay home for the raids.
        for (int i = 0; i < 4; i++) bot.Train("auth_tank");
        for (int i = 0; i < 5; i++) bot.Train("auth_rifleman");
        Need(bot.BuildAndPlace("auth_turret", turretSpots, 900), bot, "first turret");
        Need(bot.BuildAndPlace("auth_turret", turretSpots, 900), bot, "second turret");
        Need(bot.RunUntil(() => bot.Count("auth_tank") >= 6 && bot.Count("auth_rifleman") >= 8, 4200, 10), bot, "strike force");

        Simulation s = bot.Sim;
        var carriers = new List<EntityId>();
        var escort = new List<(EntityId Id, ActorKind Kind)>();
        int carrierType = SimRules.IndexOf(s.Setup.Rules.Vehicles, "auth_carrier", v => v.Id);
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0)
            {
                if (s.Units.TypeIndex[i] == carrierType) carriers.Add(s.Units.Slots.IdOf(i));
                else escort.Add((s.Units.Slots.IdOf(i), ActorKind.Vehicle));
            }
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0) escort.Add((s.Infantry.Slots.IdOf(i), ActorKind.Infantry));

        bool assault = false;
        void March()
        {
            foreach (EntityId c in carriers)
                if (bot.Sim.Units.Slots.IsAlive(c)) bot.Send(CommandKind.AttackMove, c, assault ? new CellCoord(56, 82) : new CellCoord(52, 74), (int)ActorKind.Vehicle);
            foreach ((EntityId id, ActorKind kind) in escort)
            {
                bool alive = kind == ActorKind.Vehicle ? bot.Sim.Units.Slots.IsAlive(id) : bot.Sim.Infantry.Slots.IsAlive(id);
                if (alive) bot.Send(CommandKind.AttackMove, id, assault ? new CellCoord(64, 81) : new CellCoord(52, 76), (int)kind);
            }
        }

        // march to a staging point north of the beach, regroup, then assault the picket together.
        March();
        for (int i = 0; i < 6; i++) bot.Train("auth_rifleman");
        bot.Train("auth_tank");
        bot.Train("auth_tank");
        int marched = bot.Sim.Tick;
        for (int t = 0; t < limit && !bot.Sim.GameOver; t += 100)
        {
            if (!assault && bot.Sim.Tick - marched > 1400) assault = true;
            if (t % 300 == 0) March();
            if (t % 900 == 0) bot.Train("auth_rifleman");
            bot.Idle(99);
        }
    }

    private static void Auth04(MissionBot bot)
    {
        const int limit = 16000;
        var turretSpots = new CellCoord[] { new(52, 12), new(52, 14), new(48, 17), new(44, 18), new(52, 16), new(53, 10), new(46, 17) };

        for (int i = 0; i < 5; i++) bot.Train("auth_tank");
        for (int i = 0; i < 6; i++) bot.Train("auth_rifleman");
        Need(bot.BuildAndPlace("auth_turret", turretSpots, 900), bot, "first turret");
        Need(bot.BuildAndPlace("auth_turret", turretSpots, 900), bot, "second turret");
        Need(bot.RunUntil(() => bot.Count("auth_tank") >= 8 && bot.Count("auth_rifleman") >= 12, 3000, 10), bot, "assault group");

        Simulation s = bot.Sim;
        var group = new List<(EntityId Id, ActorKind Kind)>();
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0) group.Add((s.Units.Slots.IdOf(i), ActorKind.Vehicle));
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0) group.Add((s.Infantry.Slots.IdOf(i), ActorKind.Infantry));

        int stage = 0;
        void March()
        {
            CellCoord target = stage == 0 ? new CellCoord(47, 64) : stage == 1 ? new CellCoord(47, 78) : new CellCoord(47, 86);
            foreach ((EntityId id, ActorKind kind) in group)
            {
                bool alive = kind == ActorKind.Vehicle ? bot.Sim.Units.Slots.IsAlive(id) : bot.Sim.Infantry.Slots.IsAlive(id);
                if (alive) bot.Send(CommandKind.AttackMove, id, target, (int)kind);
            }
        }

        // cross to a staging point on the south bank, regroup, push to the outer line, then the device.
        March();
        for (int i = 0; i < 6; i++) bot.Train("auth_rifleman");
        bot.Train("auth_tank");
        int marched = bot.Sim.Tick;
        for (int t = 0; t < limit && !bot.Sim.GameOver; t += 100)
        {
            int since = bot.Sim.Tick - marched;
            stage = since > 2600 ? 2 : since > 1500 ? 1 : 0;
            if (t % 200 == 0) March();
            if (t % 900 == 0) bot.Train("auth_rifleman");
            bot.Idle(99);
        }
    }

    // ---------------------------------------------------------------- Directorate

    private static void Dir01(MissionBot bot)
    {
        var site = new CellCoord(30, 25);
        var vents = new[] { new CellCoord(27, 24), new CellCoord(27, 26), new CellCoord(23, 29), new CellCoord(20, 25) };
        Need(bot.BuildAndPlace("dir_reactor_vent", vents), bot, "reactor vent");
        var hatchSpots = new List<CellCoord>();
        for (int y = 23; y <= 27; y++) for (int x = 26; x <= 31; x++) hatchSpots.Add(new CellCoord(x, y));
        Need(bot.BuildAndPlace("dir_hatch", hatchSpots), bot, "hatch");
        Need(bot.RunUntilObjective("hatch", 1000), bot, "hatch objective");

        var muster = new[] { new CellCoord(20, 29), new CellCoord(27, 20), new CellCoord(20, 21), new CellCoord(17, 25), new CellCoord(23, 17) };
        Need(bot.BuildAndPlace("dir_muster_hatch", muster), bot, "muster hatch");
        for (int i = 0; i < 14; i++) bot.Train("dir_conscript");
        for (int i = 0; i < 4; i++) bot.Train("dir_ashthrower");
        var pill = new[] { new CellCoord(30, 23), new CellCoord(30, 27), new CellCoord(28, 22), new CellCoord(28, 28) };
        bot.BuildAndPlace("dir_pillbox", pill, 1500);

        for (int guard = 0; guard < 20000 && !bot.Sim.GameOver; guard++)
        {
            if (guard % 100 == 0) bot.OrderAll(CommandKind.AttackMove, site);
            bot.Idle();
        }
    }

    private static void DirRaid(MissionBot bot, CellCoord[] stage, CellCoord[] target)
    {
        for (int guard = 0; guard < 30000 && !bot.Sim.GameOver; guard++)
        {
            if (guard % 200 == 0 && bot.Cash > 800)
            {   // the queue holds only a few items: keep topping it up
                for (int i = 0; i < 3; i++) bot.Train("dir_conscript");
                for (int i = 0; i < 2; i++) bot.Train("dir_conscript");
                if (guard % 1000 == 0) bot.Train("dir_sapper");
            }
            if (guard >= 1500 && guard % 100 == 0)
            {
                int leg = ((guard - 1500) / 2400) % stage.Length, into = (guard - 1500) % 2400;
                if (into == 0) bot.OrderAll(CommandKind.AttackMove, stage[leg]);
                else if (into == 1000) bot.OrderAll(CommandKind.AttackMove, target[leg]);
                else if (into > 1000 && into % 400 == 0) bot.OrderAll(CommandKind.AttackMove, target[leg]);
            }
            bot.Idle();
            if (bot.Host.Outcome == MissionOutcome.Won) break;
        }
    }

    private static void Dir02(MissionBot bot) => DirRaid(bot,
        new[] { new CellCoord(35, 21), new CellCoord(48, 36), new CellCoord(15, 33), new CellCoord(28, 40) },
        new[] { new CellCoord(38, 12), new CellCoord(55, 31), new CellCoord(9, 36), new CellCoord(29, 49) });

    private static void Dir03(MissionBot bot) => DirRaid(bot,
        new[] { new CellCoord(30, 40), new CellCoord(55, 30), new CellCoord(52, 44), new CellCoord(36, 44) },
        new[] { new CellCoord(44, 49), new CellCoord(66, 25), new CellCoord(58, 52), new CellCoord(44, 49) });

    private static void Dir04(MissionBot bot)
    {
        var head = new CellCoord(60, 57);
        var ridge = new CellCoord(60, 65);
        int sunrise = SimRules.IndexOf(bot.Sim.Setup.Rules.Buildings, "dir_sunrise", x => x.Id);
        for (int guard = 0; guard < 30000 && !bot.Sim.GameOver; guard++)
        {
            if (guard % 200 == 0 && bot.Cash > 800)
                for (int i = 0; i < 5; i++) bot.Train("dir_conscript");
            if (guard % 100 == 0 && guard >= 600) bot.OrderAll(CommandKind.AttackMove, head);
            if (guard >= 3100 && guard % 50 == 0)
            {
                Simulation s = bot.Sim;
                EntityId device = default;
                for (int i = 1; i < s.Buildings.Capacity && device == default; i++)
                    if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 0 && s.Buildings.TypeIndex[i] == sunrise)
                        device = s.Buildings.Slots.IdOf(i);
                if (device != default) bot.Send(CommandKind.Special, device, ridge, 0);
            }
            bot.Idle();
            if (bot.Host.Outcome == MissionOutcome.Won) break;
        }
    }

    // ---------------------------------------------------------------- Left Behind

    private static void Lb01(MissionBot bot)
    {
        const int limit = 20000;
        // 1. the packed Mother Rig parks on the ash; the player unpacks it on clear ground.
        var site = new CellCoord(30, 22);
        Need(bot.RunUntil(() => PackedRig(bot.Sim) is not null, 1500, 5), bot, "packed rig");
        bot.RunUntil(() => bot.Sim.Tick >= 600, 700, 5);
        bot.Send(CommandKind.Pack, PackedRig(bot.Sim)!.Value, site, 0);
        Need(bot.RunUntilObjective("deploy", 3000), bot, "deploy");

        // 2. a camp: power, a Muster Rig, then hands.
        var ring = new[] { new CellCoord(34, 22), new CellCoord(26, 26), new CellCoord(30, 26), new CellCoord(34, 26), new CellCoord(38, 22), new CellCoord(30, 29), new CellCoord(34, 29) };
        Need(bot.BuildAndPlace("lb_dynamo_rig", ring), bot, "dynamo rig");
        Need(bot.BuildAndPlace("lb_muster_rig", ring), bot, "muster rig");
        for (int i = 0; i < 4; i++) bot.Train("lb_hook_runner");
        for (int i = 0; i < 8; i++) bot.Train("lb_scavenger");

        // 3. take position on the road before the convoy leaves the depot, then chase until the scrapers and the relief are down.
        bot.RunUntil(() => bot.Sim.Tick >= 2300, 2300, 10);
        bot.OrderAll(CommandKind.AttackMove, new CellCoord(34, 16));
        Need(bot.RunUntil(() => bot.Host.Timers.Count == 0, 4500, 10), bot, "relief timer");
        var crossing = new CellCoord(34, 16);
        for (int t = 0; t < limit && !bot.Sim.GameOver; t += 100)
        {
            CellCoord? foe = bot.Done("scrapers") ? NearestEnemyUnit(bot.Sim, crossing, 1) : null;
            bot.OrderAll(CommandKind.AttackMove, foe ?? crossing);
            bot.Idle(99);
        }
    }

    private static EntityId? PackedRig(Simulation s)
    {
        int type = SimRules.IndexOf(s.Setup.Rules.Vehicles, "lb_mother_rig_packed", v => v.Id);
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Units.TypeIndex[i] == type) return s.Units.Slots.IdOf(i);
        return null;
    }

    /// <summary>Nearest soldier or vehicle (Manhattan) of the players for which <paramref name="accept"/> holds; within <paramref name="reach"/> cells.</summary>
    private static CellCoord? NearestFoe(Simulation s, CellCoord from, Func<byte, bool> accept, int reach = int.MaxValue - 1)
    {
        CellCoord? best = null;
        int bestD = reach + 1;
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && accept(s.Units.Player[i])) Consider(s.Units.Position[i].ToCell());
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && accept(s.Infantry.Player[i])) Consider(s.Infantry.Position[i].ToCell());
        return best;

        void Consider(CellCoord c)
        {
            int d = Math.Abs(c.X - from.X) + Math.Abs(c.Y - from.Y);
            if (d < bestD) { bestD = d; best = c; }
        }
    }

    private static CellCoord? NearestEnemyUnit(Simulation s, CellCoord from, byte player, int reach = int.MaxValue - 1) =>
        NearestFoe(s, from, p => p == player, reach);

    private static void Lb02(MissionBot bot)
    {
        // 1. a Garage Rig, then slingers (they are what breaks turrets).
        var ring = new[] { new CellCoord(32, 17), new CellCoord(36, 17), new CellCoord(28, 17), new CellCoord(40, 17), new CellCoord(28, 25), new CellCoord(28, 21) };
        Need(bot.BuildAndPlace("lb_garage_rig", ring), bot, "garage rig");
        Need(bot.BuildAndPlace("lb_lookout", ring.Select(c => new CellCoord(c.X + 3, c.Y)).Concat(ring).ToArray()), bot, "lookout");
        for (int i = 0; i < 3; i++) bot.Train("lb_slinger");
        bot.RunUntil(() => bot.Sim.Tick >= 3600, 3600, 10);
        Need(bot.Done("gate"), bot, "gate");
        Need(bot.Count("lb_slinger") >= 12, bot, "slingers");

        // 2. post 1 (north road); its fall calls the reprisal column to the camp, so come home and beat it.
        Need(HoldUntil(bot, new CellCoord(41, 19), () => PowerDown(bot) >= 1, 3000), bot, "post 1 power");
        Need(HoldUntil(bot, new CellCoord(38, 13), () => PostsDown(bot) >= 1, 3000), bot, "post 1");
        Need(HoldUntil(bot, new CellCoord(35, 20), () => bot.Done("hold"), 6000), bot, "hold the camp");

        // 3. posts 2 and 3.
        Need(HoldUntil(bot, new CellCoord(18, 29), () => PowerDown(bot) >= 2, 5000), bot, "post 2 power");
        Need(HoldUntil(bot, new CellCoord(10, 32), () => PostsDown(bot) >= 2, 4000), bot, "post 2");
        Need(HoldUntil(bot, new CellCoord(21, 32), () => PowerDown(bot) >= 3, 5000), bot, "post 3 power");
        Need(HoldUntil(bot, new CellCoord(12, 41), () => PostsDown(bot) >= 3 || bot.Sim.GameOver, 8000), bot, "post 3");
        bot.RunUntil(() => bot.Host.Outcome != MissionOutcome.None, 30000, 10);
    }

    private static bool HoldUntil(MissionBot bot, CellCoord cell, Func<bool> done, int budget)
    {
        int end = bot.Sim.Tick + budget;
        while (bot.Sim.Tick < end && !bot.Sim.GameOver)
        {
            if (done()) return true;
            bot.OrderAll(CommandKind.AttackMove, cell);
            bot.Idle(99);
        }
        return done();
    }

    private static int PowerDown(MissionBot bot) => bot.Host.Messages.Count(x => x.Text.Contains("power plant"));

    private static int PostsDown(MissionBot bot) => bot.Host.Messages.Count(x => x.Text.Contains("road post is down"));

    private static void Lb03(MissionBot bot)
    {
        string[] strike = { "lb_harpoon_wagon", "lb_slinger", "lb_drifter_brute" };
        string[] home = { "lb_scavenger", "lb_hook_runner", "lb_buggy", "lb_tinker" };
        bot.Idle(5);

        // 1. two more wagons from the Garage Rig while the strike team forms up.
        bot.Train("lb_harpoon_wagon");
        bot.Train("lb_harpoon_wagon");
        bot.RunUntil(() => bot.Sim.Tick >= 1500, 1500, 10);

        // 2. the strike team takes the vent from range; the rest guard the camp.
        EntityId? vent = FindEnemyBuilding(bot.Sim, "dir_reactor_vent");
        Need(vent is not null, bot, "vent exists");
        for (int t = 0; t < 4000 && !bot.Sim.GameOver && !bot.Done("vent"); t += 100)
        {
            OrderTypes(bot, new[] { "lb_slinger", "lb_drifter_brute" }, CommandKind.AttackMove, new CellCoord(54, 40));
            OrderTypes(bot, home, CommandKind.AttackMove, new CellCoord(30, 22));
            OrderAttack(bot, new[] { "lb_harpoon_wagon" }, vent!.Value);
            bot.Idle(99);
        }
        Need(bot.Done("vent"), bot, "vent");
        EntityId? mast = FindEnemyBuilding(bot.Sim, "dir_mast");
        for (int t = 0; t < 3000 && !bot.Sim.GameOver && mast is not null && bot.Host.Objectives.Any(o => o.Id == "mast" && o.Status != ObjectiveStatus.Completed); t += 100)
        {
            OrderTypes(bot, strike, CommandKind.AttackMove, new CellCoord(65, 40));
            bot.Idle(99);
        }
        Need(bot.Host.Objectives.Any(o => o.Id == "mast" && o.Status == ObjectiveStatus.Completed), bot, "mast");

        // 3. close the hatches, with the strike team on attack-move and the home guard at the camp.
        var hatches = new[] { new CellCoord(57, 37), new CellCoord(63, 36), new CellCoord(60, 43) };
        for (int t = 0; t < 12000 && !bot.Sim.GameOver && !bot.Done("hatches"); t += 100)
        {
            CellCoord target = hatches.FirstOrDefault(h => EnemyBuildingAt(bot.Sim, h, 2));
            if (target.X == 0) target = new CellCoord(60, 40);
            OrderTypes(bot, strike, CommandKind.AttackMove, target);
            OrderTypes(bot, home, CommandKind.AttackMove, new CellCoord(30, 22));
            bot.Idle(99);
        }
        Need(bot.Done("hatches"), bot, "hatches");

        // 4. everyone home to finish the column.
        for (int t = 0; t < 12000 && !bot.Sim.GameOver; t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, NearestFoe(bot.Sim, new CellCoord(30, 22), p => p != 0 && p < 3) ?? new CellCoord(30, 22));
            bot.Idle(99);
        }
    }

    private static void Lb04(MissionBot bot)
    {
        bot.Idle(5);

        // 1. hold the camp until the counter-landing is broken.
        var camp = new CellCoord(47, 24);
        for (int t = 0; t < 9000 && !bot.Sim.GameOver && !bot.Done("counter"); t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, NearestFoe(bot.Sim, camp, p => p == 1, 30) ?? camp);
            bot.Idle(99);
        }
        Need(bot.Done("counter"), bot, "counter-landing");

        // 2. swing round to the east flank, out of the batteries' reach, and take the pile houses from range.
        for (int t = 0; t < 2400 && !bot.Sim.GameOver; t += 100)
        {
            bot.OrderAll(CommandKind.Move, new CellCoord(72, 70));
            bot.Idle(99);
        }
        for (int t = 0; t < 5000 && !bot.Sim.GameOver && !bot.Done("power"); t += 100)
        {
            EntityId? pile = FindEnemyBuilding(bot.Sim, "exp_pile_house");
            if (pile is null) break;
            OrderAttack(bot, new[] { "lb_rolling_fort", "lb_harpoon_wagon", "lb_organ" }, pile.Value);
            OrderTypes(bot, new[] { "lb_slinger", "lb_drifter_brute", "lb_scavenger", "lb_hook_runner", "lb_veilwalker", "lb_tinker", "lb_buggy" }, CommandKind.AttackMove, new CellCoord(70, 76));
            bot.Idle(99);
        }
        Need(bot.Done("power"), bot, "power");

        // 3. dark batteries are walls: break the depot.
        var depot = new CellCoord(47, 82);
        for (int t = 0; t < 12000 && !bot.Sim.GameOver; t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, depot);
            bot.Idle(99);
        }
    }

    private static EntityId? FindEnemyBuilding(Simulation s, string typeId)
    {
        int type = SimRules.IndexOf(s.Setup.Rules.Buildings, typeId, b => b.Id);
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.TypeIndex[i] == type && s.Buildings.Player[i] != 0) return s.Buildings.Slots.IdOf(i);
        return null;
    }

    private static bool EnemyBuildingAt(Simulation s, CellCoord cell, int player)
    {
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == player && s.Buildings.Cell[i].X == cell.X && s.Buildings.Cell[i].Y == cell.Y) return true;
        return false;
    }

    // One tick with an order for every own unit or soldier of the listed types.
    private static void OrderTypes(MissionBot bot, string[] typeIds, CommandKind kind, CellCoord cell)
    {
        Simulation s = bot.Sim;
        var rules = s.Setup.Rules;
        var cmds = new List<Command>();
        foreach (string id in typeIds)
        {
            int v = SimRules.IndexOf(rules.Vehicles, id, x => x.Id);
            if (v >= 0)
                for (int i = 1; i < s.Units.Capacity; i++)
                    if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Units.TypeIndex[i] == v)
                        cmds.Add(new Command(s.Tick, 0, bot.NextSeq(), kind, s.Units.Slots.IdOf(i), default, cell, (int)ActorKind.Vehicle));
            int f = SimRules.IndexOf(rules.Infantry, id, x => x.Id);
            if (f >= 0)
                for (int i = 1; i < s.Infantry.Capacity; i++)
                    if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0 && s.Infantry.TypeIndex[i] == f)
                        cmds.Add(new Command(s.Tick, 0, bot.NextSeq(), kind, s.Infantry.Slots.IdOf(i), default, cell, (int)ActorKind.Infantry));
        }
        bot.Step(cmds);
    }

    // One tick with an Attack order on a building for every own unit of the listed vehicle types.
    private static void OrderAttack(MissionBot bot, string[] typeIds, EntityId target)
    {
        Simulation s = bot.Sim;
        var cmds = new List<Command>();
        foreach (string id in typeIds)
        {
            int v = SimRules.IndexOf(s.Setup.Rules.Vehicles, id, x => x.Id);
            for (int i = 1; i < s.Units.Capacity; i++)
                if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Units.TypeIndex[i] == v)
                    cmds.Add(new Command(s.Tick, 0, bot.NextSeq(), CommandKind.Attack, s.Units.Slots.IdOf(i), target, default, (int)ActorKind.Vehicle | ((int)ActorKind.Building << 8)));
        }
        bot.Step(cmds);
    }

    // ---------------------------------------------------------------- Expedition

    private static void Exp01(MissionBot bot)
    {
        const int limit = 20000;
        // 1. Deploy the Command Ship into the Landing Depot.
        int ship = SimRules.IndexOf(bot.Sim.Setup.Rules.Vehicles, "exp_command_ship", v => v.Id);
        EntityId flagship = default;
        for (int i = 1; i < bot.Sim.Units.Capacity; i++)
            if (bot.Sim.Units.Slots.IsIndexAlive(i) && bot.Sim.Units.Player[i] == 0 && bot.Sim.Units.TypeIndex[i] == ship) { flagship = bot.Sim.Units.Slots.IdOf(i); break; }
        Need(flagship != default, bot, "command ship");
        bot.Send(CommandKind.Deploy, flagship, new CellCoord(86, 82), 0);
        Need(bot.RunUntilObjective("landing", 600), bot, "landing");
        CellCoord depot = ExpeditionMissionTestKit.BuildingCell(bot.Sim, "exp_landing_depot");

        // 2. Power, billet, seawall, each beside the depot; then more marines.
        var spotList = new List<CellCoord>();
        for (int r = 3; r <= 7; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && depot.Y + dy >= 0) spotList.Add(new CellCoord(depot.X + dx, depot.Y + dy));
        spotList.Sort((a, b) => a.Y.CompareTo(b.Y));
        CellCoord[] spots = spotList.ToArray();
        Need(bot.BuildAndPlace("exp_pile_house", spots), bot, "pile house");
        Need(bot.BuildAndPlace("exp_billet", spots), bot, "billet");
        Need(bot.BuildAndPlace("exp_pillbox", spots), bot, "pillbox");
        Need(bot.RunUntilObjective("build", limit), bot, "build objective");
        for (int i = 0; i < 4; i++) bot.Train("exp_marine");

        // 3. Hold: marines stand guard at the depot while the waves come.
        var hold = new CellCoord(depot.X + 1, depot.Y - 3);
        for (int guard = 0; bot.Sim.Tick < limit && !bot.Sim.GameOver; guard++)
        {
            if (guard % 50 == 0) bot.OrderAll(CommandKind.AttackMove, ExpeditionMissionTestKit.EnemyNear(bot.Sim, depot, 20) ?? hold);
            else bot.Idle();
        }
    }

    private static void Exp02(MissionBot bot)
    {
        const int limit = 30000;
        CellCoord depot = ExpeditionMissionTestKit.BuildingCell(bot.Sim, "exp_landing_depot");

        // 1. The Anchorage on the shallows of the north shore.
        Need(bot.BuildAndPlace("exp_anchorage", ExpeditionMissionTestKit.DockSpots(bot.Sim, depot, 9)), bot, "anchorage");
        Need(bot.RunUntilObjective("dock", limit), bot, "dock objective");

        // 2. A seawall, four provision stores and a second billet, then Glide Tanks and marines; the army waits on the landing and clears the road once the columns are beaten.
        CellCoord[] spots = ExpeditionMissionTestKit.Spots(bot.Sim, depot, 4, 9, c => c.Y >= 10);
        Need(bot.BuildAndPlace("exp_pillbox", spots), bot, "pillbox");
        for (int i = 0; i < 4; i++) Need(bot.BuildAndPlace("exp_provision_store", spots), bot, "provision store");
        Need(bot.BuildAndPlace("exp_billet", spots), bot, "billet");
        var hold = new CellCoord(20, 14);
        var target = new CellCoord(41, 25);
        bool pushed = false;
        for (int guard = 0; bot.Sim.Tick < limit && !bot.Sim.GameOver; guard++)
        {
            if (guard % 40 == 0) bot.Train(bot.Count("exp_glide_tank") < 8 || bot.Count("exp_marine") >= 14 ? "exp_glide_tank" : "exp_marine");
            if (guard % 100 == 0)
            {
                if (bot.Done("hold") && bot.Count("exp_glide_tank") >= 6) pushed = true;
                ExpeditionMissionTestKit.OrderLand(bot.Sim, CommandKind.AttackMove, pushed ? target : hold);
            }
            else bot.Idle();
        }
    }

    private static void Exp03(MissionBot bot)
    {
        const int limit = 30000;
        CellCoord depot = ExpeditionMissionTestKit.BuildingCell(bot.Sim, "exp_landing_depot");
        var hold = new CellCoord(21, 44);
        bool BuildD(string id, CellCoord[] sp) => ExpeditionMissionTestKit.BuildDefending(bot, id, sp, depot, hold);
        CellCoord[] spots = ExpeditionMissionTestKit.Spots(bot.Sim, depot, 4, 10, c => c.X >= 14 && c.Y >= 36 && c.Y <= 55);

        for (int i = 0; i < 3; i++) { bot.Train("exp_glide_tank"); bot.Train("exp_marine"); }
        // Power first: the SAM Mounts and the Flight Deck go dark on a short grid.
        Need(BuildD("exp_pile_house", spots), bot, "exp_pile_house");
        Need(BuildD("exp_pile_house", spots), bot, "exp_pile_house");
        Need(BuildD("exp_sam", spots), bot, "exp_sam");
        Need(BuildD("exp_flight_deck", spots), bot, "exp_flight_deck");
        Need(BuildD("exp_sam", spots), bot, "exp_sam");
        Need(BuildD("exp_sam", spots), bot, "exp_sam");
        for (int i = 0; i < 3; i++) Need(BuildD("exp_provision_store", spots), bot, "exp_provision_store");
        Need(BuildD("exp_pillbox", spots), bot, "exp_pillbox");
        Need(BuildD("exp_billet", spots), bot, "exp_billet");

        for (int guard = 0; bot.Sim.Tick < limit && !bot.Sim.GameOver; guard++)
        {
            if (guard % 40 == 0) bot.Train(bot.Count("exp_glide_tank") < 8 || bot.Count("exp_marine") >= 14 ? "exp_glide_tank" : "exp_marine");
            if (guard % 60 == 0) ExpeditionMissionTestKit.OrderLand(bot.Sim, CommandKind.AttackMove, ExpeditionMissionTestKit.EnemyNear(bot.Sim, depot, 20) ?? hold);
            else bot.Idle();
        }
    }

    private static void Exp04(MissionBot bot)
    {
        const int limit = 30000;
        CellCoord depot = ExpeditionMissionTestKit.BuildingCell(bot.Sim, "exp_landing_depot");
        var hold = new CellCoord(46, 76);
        var target = new CellCoord(46, 19);
        bool BuildD(string id, CellCoord[] sp) => ExpeditionMissionTestKit.BuildDefending(bot, id, sp, depot, hold);
        CellCoord[] spots = ExpeditionMissionTestKit.Spots(bot.Sim, depot, 4, 10, c => c.Y >= 72 && c.Y <= 90 && (c.X <= 49 || c.X >= 60));
        CellCoord[] ash = ExpeditionMissionTestKit.Spots(bot.Sim, depot, 4, 12, c => c.X >= 51 && c.X <= 55 && c.Y >= 79 && c.Y <= 85);

        for (int i = 0; i < 3; i++) { bot.Train("exp_glide_tank"); bot.Train("exp_marine"); }
        Need(BuildD("exp_dredge_works", ash), bot, "dredge");
        Need(BuildD("exp_pile_house", spots), bot, "pile");
        for (int i = 0; i < 4; i++) Need(BuildD("exp_provision_store", spots), bot, "store");
        Need(BuildD("exp_pillbox", spots), bot, "pillbox");
        Need(BuildD("exp_billet", spots), bot, "billet");
        Need(bot.Done("fuel"), bot, "fuel");

        bool pushed = false;
        for (int guard = 0; bot.Sim.Tick < limit && !bot.Sim.GameOver; guard++)
        {
            if (guard % 40 == 0) bot.Train(bot.Count("exp_glide_tank") < 12 || bot.Count("exp_marine") >= 12 ? "exp_glide_tank" : "exp_marine");
            if (guard % 60 == 0)
            {
                if (bot.Done("hold") && bot.Count("exp_glide_tank") >= 10) pushed = true;
                CellCoord? near = ExpeditionMissionTestKit.EnemyNear(bot.Sim, depot, 20);
                ExpeditionMissionTestKit.OrderLand(bot.Sim, CommandKind.AttackMove, pushed ? target : near ?? hold);
            }
            else bot.Idle();
        }
    }
}
