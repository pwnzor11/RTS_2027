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
        ["authority"] = new[] { "auth_01", "auth_05", "auth_06", "auth_02", "auth_07", "auth_03", "auth_04" },
        ["directorate"] = new[] { "dir_01", "dir_05", "dir_06", "dir_02", "dir_03", "dir_04" },
        ["left_behind"] = new[] { "lb_01", "lb_05", "lb_06", "lb_02", "lb_03", "lb_04" },
        ["expedition"] = new[] { "exp_01", "exp_02", "exp_03", "exp_04" },
    };

    /// <summary>Every mission id, campaign by campaign (S47: the 16 built ones plus the pilot auth_07; S48: plus the C-12 wave-1 missions).</summary>
    public static IEnumerable<string> AllMissions => Campaigns.Values.SelectMany(x => x);

    /// <summary>xunit data: one row per mission id.</summary>
    public static IEnumerable<object[]> AllMissionRows => AllMissions.Select(x => new object[] { x });

    /// <summary>The design tick limit of a mission: the bot must win before this absolute tick (the limit its own test file asserts).</summary>
    public static int TickLimit(string id) => id switch
    {
        "auth_01" => 14000,
        "auth_04" => 16000,
        "auth_05" => 9000,   // S48: the 9600-tick "name read out" timer is the hard limit; a clean walk is done in about a third of it
        "auth_06" => 15000,   // S48: the grain-train timer ends the mission at tick 15000, so a winning walk must be done before it
        "auth_07" => 8000,   // S47 pilot: found by about tick 2000, hunted from found + 600, so a clean run is home well before this
        "dir_05" => 7000,   // S48: three plants in about 3000 ticks and the walk home; the dawn timer is 5400
        "lb_05" => 20000,   // S48 C-12: both holds are down by about tick 9000 at tier 3; the Gathering leaves at 21600
        "lb_06" => 8000,   // S48: the crews arrive at tick 5400; a clean walk banks the ash well before
        "dir_01" or "exp_01" or "lb_01" => 20000,
        "dir_06" => 20000,   // S48: build, hold the pad against the survey column (tick 6000), then clear the camp
        _ => 30000,
    };

    /// <summary>The tiers the per-mission test supports for this mission's walk.</summary>
    public static int[] Tiers(string id) => id switch
    {
        "auth_04" or "dir_01" or "exp_01" => new[] { 3, 5 },
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
                case "auth_05": Auth05(bot); break;
                case "auth_06": Auth06(bot); break;
                case "auth_07": Auth07(bot); break;
                case "auth_04": Auth04(bot); break;
                case "dir_01": Dir01(bot); break;
                case "dir_05": Dir05(bot); break;
                case "dir_06": Dir06(bot); break;
                case "dir_02": Dir02(bot); break;
                case "dir_03": Dir03(bot); break;
                case "dir_04": Dir04(bot); break;
                case "lb_05": Lb05(bot); break;
                case "lb_01": Lb01(bot); break;
                case "lb_06": Lb06(bot); break;
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
        int assaultAt = 0;
        bool sweep = false; // after the first push, advance onto the rear of the picket (units only acquire targets inside their own weapon range)
        void March()
        {
            foreach (EntityId c in carriers)
                if (bot.Sim.Units.Slots.IsAlive(c)) bot.Send(CommandKind.AttackMove, c, assault ? new CellCoord(56, 82) : new CellCoord(52, 74), (int)ActorKind.Vehicle);
            foreach ((EntityId id, ActorKind kind) in escort)
            {
                bool alive = kind == ActorKind.Vehicle ? bot.Sim.Units.Slots.IsAlive(id) : bot.Sim.Infantry.Slots.IsAlive(id);
                if (alive) bot.Send(CommandKind.AttackMove, id, assault ? (sweep ? new CellCoord(67, 83) : new CellCoord(64, 81)) : new CellCoord(52, 76), (int)kind);
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
            if (!assault && bot.Sim.Tick - marched > 1400) { assault = true; assaultAt = bot.Sim.Tick; }
            if (assault && !sweep && bot.Sim.Tick - assaultAt > 900) sweep = true;
            if (t % 300 == 0) March();
            if (t % 900 == 0) bot.Train("auth_rifleman");
            bot.Idle(99);
        }
    }

    /// <summary>
    /// S48 (auth_05 "Hatch Country"): the column attack-moves from hatch to hatch (west to east), then onto the loudspeaker pole; the hidden
    /// logbook is a short detour at the end only if time allows (not part of the win).
    /// </summary>
    private static void Auth05(MissionBot bot)
    {
        const int limit = 9000;
        var targets = new[] { new CellCoord(22, 14), new CellCoord(33, 17), new CellCoord(43, 13), new CellCoord(40, 10) };
        int stage = 0;
        bot.OrderAll(CommandKind.AttackMove, targets[0]);
        for (int t = 0; t < limit && !bot.Sim.GameOver && bot.Host.Outcome == MissionOutcome.None; t += 20)
        {
            while (stage < targets.Length && !EnemyBuildingAt(bot.Sim, targets[stage], 1)) stage++;
            if (stage >= targets.Length) { bot.Idle(20); continue; }
            if (t % 100 == 0) bot.OrderAll(CommandKind.AttackMove, targets[stage]);
            bot.Idle(20);
        }
    }

    /// <summary>
    /// S48 (auth_06 "The Mill Town"): raise a power plant, the factory and a ration hall, two Turrets at the yard, then six tanks and riflemen;
    /// two scrapers go to the pitchblende spur; the strike force takes the vents and hatches one after the other (west to east, the hatch beside
    /// the grain elevator last) while later production stays home for the raids.
    /// </summary>
    private static void Auth06(MissionBot bot)
    {
        const int limit = 14000;
        var spots = new List<CellCoord>();
        for (int y = 48; y <= 61; y += 1)
            for (int x = 2; x <= 14; x += 1) spots.Add(new CellCoord(x, y));
        var turretSpots = new CellCoord[] { new(14, 55), new(14, 58), new(10, 49), new(16, 56), new(12, 47), new(5, 47) };
        Need(bot.BuildAndPlace("auth_power", spots, 1500), bot, "second power plant");
        Need(bot.BuildAndPlace("auth_factory", spots, 3000), bot, "factory");
        Need(bot.BuildAndPlace("auth_ration_hall", spots, 1500), bot, "ration hall");
        Need(bot.BuildAndPlace("auth_turret", turretSpots, 1500), bot, "first turret");
        Need(bot.BuildAndPlace("auth_turret", turretSpots, 1500), bot, "second turret");
        for (int i = 0; i < 4; i++) bot.Train("auth_tank");
        for (int i = 0; i < 6; i++) bot.Train("auth_rifleman");
        Need(bot.RunUntil(() => bot.Count("auth_tank") >= 4 && bot.Count("auth_rifleman") >= 10, bot.Sim.Tick + 7000, 10), bot, "strike force");

        // two scrapers go to the spur and stay (a Move order leaves them idle there)
        Simulation s = bot.Sim;
        int sent = 0;
        for (int i = 1; i < s.Units.Capacity && sent < 2; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Setup.Rules.Vehicles[s.Units.TypeIndex[i]].HarvestCapacity > 0)
            {
                bot.Send(CommandKind.Move, s.Units.Slots.IdOf(i), new CellCoord(12 + sent, 25), (int)ActorKind.Vehicle);
                sent++;
            }

        var strike = new List<(EntityId Id, ActorKind Kind)>();
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Setup.Rules.Vehicles[s.Units.TypeIndex[i]].HarvestCapacity == 0)
                strike.Add((s.Units.Slots.IdOf(i), ActorKind.Vehicle));
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0) strike.Add((s.Infantry.Slots.IdOf(i), ActorKind.Infantry));

        var targets = new CellCoord[] { new(22, 26), new(24, 19), new(31, 25), new(38, 22), new(40, 27), new(46, 22) };
        bool Standing(CellCoord c)
        {
            Simulation s = bot.Sim;   // not a cached reference: the save/load test rebinds the bot to a loaded simulation
            for (int i = 1; i < s.Buildings.Capacity; i++)
                if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 1 && s.Buildings.Cell[i].Equals(c) && s.Buildings.Health[i] > 0) return true;
            return false;
        }
        for (int t = 0; t < limit && !bot.Sim.GameOver && bot.Host.Outcome == MissionOutcome.None; t += 100)
        {
            CellCoord goal = targets[^1];
            foreach (CellCoord c in targets)
                if (Standing(c)) { goal = c; break; }
            foreach ((EntityId id, ActorKind kind) in strike)
            {
                bool alive = kind == ActorKind.Vehicle ? bot.Sim.Units.Slots.IsAlive(id) : bot.Sim.Infantry.Slots.IsAlive(id);
                if (alive) bot.Send(CommandKind.AttackMove, id, goal, (int)kind);
            }
            if (t % 600 == 0) { bot.Train("auth_tank"); bot.Train("auth_rifleman"); }
            bot.Idle(99);
        }
    }

    /// <summary>
    /// S47 pilot (auth_07 "The Defector"): the column takes the long south-west road (past the culvert, which pays the hidden bonus)
    /// to the spoil heaps, Dray boards the sealed carrier, and the carrier drives home the same way, away from the bridge picket.
    /// </summary>
    private static void Auth07(MissionBot bot)
    {
        const int limit = 8000;
        var going = new[] { new CellCoord(18, 44), new CellCoord(22, 46), new CellCoord(41, 31) };
        var back = new[] { new CellCoord(18, 44), new CellCoord(8, 8) };
        int leg = 0;
        bool returning = false;
        bot.OrderAll(CommandKind.AttackMove, going[0]);
        for (int t = 0; t < limit && !bot.Sim.GameOver && bot.Host.Outcome == MissionOutcome.None; t += 20)
        {
            Simulation s = bot.Sim;
            int carrierType = SimRules.IndexOf(s.Setup.Rules.Vehicles, "auth_carrier", v => v.Id);
            int engineerType = SimRules.IndexOf(s.Setup.Rules.Infantry, "auth_engineer", v => v.Id);
            EntityId carrier = default, dray = default;
            for (int i = 1; i < s.Units.Capacity; i++)
                if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Units.TypeIndex[i] == carrierType) carrier = s.Units.Slots.IdOf(i);
            for (int i = 1; i < s.Infantry.Capacity; i++)
                if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0 && s.Infantry.TypeIndex[i] == engineerType) dray = s.Infantry.Slots.IdOf(i);
            if (carrier.IsNone) throw new WalkFailure("the carrier is gone: " + bot.Diagnostics());
            CellCoord at = s.Units.Position[carrier.Index].ToCell();
            bool Near(CellCoord c) => Math.Max(Math.Abs(at.X - c.X), Math.Abs(at.Y - c.Y)) <= 4;

            if (dray.IsNone)
            {
                if (leg < going.Length - 1 && Near(going[leg])) { leg++; bot.OrderAll(CommandKind.AttackMove, going[leg]); }
                else if (t % 200 == 0) bot.OrderAll(CommandKind.AttackMove, going[leg]);
            }
            else if (!returning)
            {
                if (s.PassengersIn(carrier) > 0) { returning = true; leg = 0; bot.OrderAll(CommandKind.AttackMove, back[0]); }
                else if (t % 40 == 0)
                {   // drive the carrier up to him (boarding is instant within one cell), then tell him to get in
                    CellCoord d = s.Infantry.Position[dray.Index].ToCell();
                    if (Math.Max(Math.Abs(at.X - d.X), Math.Abs(at.Y - d.Y)) > 1) bot.Send(CommandKind.Move, carrier, d, (int)ActorKind.Vehicle);
                    else bot.Step(new[] { new Command(s.Tick, 0, bot.NextSeq(), CommandKind.Enter, dray, carrier, default, (int)ActorKind.Infantry | ((int)ActorKind.Vehicle << 8)) });
                }
            }
            else if (leg < back.Length - 1 && Near(back[leg])) { leg++; bot.OrderAll(CommandKind.AttackMove, back[leg]); }
            else if (t % 200 == 0) bot.OrderAll(CommandKind.AttackMove, back[leg]);
            bot.Idle(20);
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

    /// <summary>
    /// S48 (dir_05 "Night Shift"): the shift walks to each Authority power plant in turn (attack-move, then an Attack order on the plant),
    /// and once all three are dark it walks home to the hatch zone.
    /// </summary>
    private static void Dir05(MissionBot bot)
    {
        const int limit = 7000;
        var plants = new[] { new CellCoord(24, 12), new CellCoord(36, 4), new CellCoord(44, 10) };
        var home = new CellCoord(15, 21);
        int leg = 0;
        bool homeward = false;
        bot.OrderAll(CommandKind.AttackMove, new CellCoord(plants[0].X + 2, plants[0].Y + 3));
        for (int t = 0; t < limit && !bot.Sim.GameOver && bot.Host.Outcome == MissionOutcome.None; t += 20)
        {
            Simulation s = bot.Sim;
            int powerType = SimRules.IndexOf(s.Setup.Rules.Buildings, "auth_power", b => b.Id);
            EntityId target = default;
            while (leg < plants.Length && target.IsNone)
            {
                for (int i = 1; i < s.Buildings.Capacity; i++)
                    if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 1 && s.Buildings.TypeIndex[i] == powerType && s.Buildings.Cell[i].Equals(plants[leg]))
                        target = s.Buildings.Slots.IdOf(i);
                if (target.IsNone) leg++;
            }
            if (leg >= plants.Length)
            {
                if (!homeward) { homeward = true; bot.OrderAll(CommandKind.Move, home); }
                else if (t % 200 == 0) bot.OrderAll(CommandKind.Move, home);
            }
            else if (t % 60 == 0)
            {   // fight whatever stands within 9 cells of the shift first (attack-move onto it), otherwise every soldier attacks the plant
                int sx = 0, sy = 0, n = 0;
                for (int i = 1; i < s.Infantry.Capacity; i++)
                    if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0) { CellCoord c = s.Infantry.Position[i].ToCell(); sx += c.X; sy += c.Y; n++; }
                var foe = new CellCoord(-1, -1);
                int best = 9;
                if (n > 0)
                {
                    for (int i = 1; i < s.Infantry.Capacity; i++)
                        if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 1)
                        {
                            CellCoord c = s.Infantry.Position[i].ToCell();
                            int d = Math.Max(Math.Abs(c.X - sx / n), Math.Abs(c.Y - sy / n));
                            if (d < best) { best = d; foe = c; }
                        }
                    for (int i = 1; i < s.Units.Capacity; i++)
                        if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 1)
                        {
                            CellCoord c = s.Units.Position[i].ToCell();
                            int d = Math.Max(Math.Abs(c.X - sx / n), Math.Abs(c.Y - sy / n));
                            if (d < best) { best = d; foe = c; }
                        }
                }
                if (foe.X >= 0) bot.OrderAll(CommandKind.AttackMove, foe);
                else
                {
                    var cmds = new List<Command>();
                    for (int i = 1; i < s.Infantry.Capacity; i++)
                        if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0)
                            cmds.Add(new Command(s.Tick, 0, bot.NextSeq(), CommandKind.Attack, s.Infantry.Slots.IdOf(i), target, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8)));
                    bot.Step(cmds);
                }
            }
            bot.Idle(20);
        }
    }

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

    /// <summary>
    /// S48 (dir_06 "The Assembly Bore"): raise the smelter vent, then the bore, cistern and bunk gallery on the pad, queue conscripts and ashthrowers
    /// (and rocket trucks once the bore stands), and send the shift at the survey camp in groups.
    /// </summary>
    private static void Dir06(MissionBot bot)
    {
        var camp = new CellCoord(41, 27);
        var pad = new List<CellCoord>();
        for (int y = 24; y <= 29; y++) for (int x = 19; x <= 27; x++) pad.Add(new CellCoord(x, y));
        Need(bot.BuildAndPlace("dir_smelter_vent", pad, 2500), bot, "smelter vent");
        Need(bot.RunUntilObjective("smelter", bot.Sim.Tick + 200), bot, "smelter objective");
        // defenders first: the north patrol lands at tick 1800 and the rail gang at 3600
        for (int i = 0; i < 6; i++) bot.Train("dir_conscript");
        for (int i = 0; i < 3; i++) bot.Train("dir_sapper");
        bot.BuildAndPlace("dir_bunk_gallery", pad, 1500);
        bot.BuildAndPlace("dir_assembly_bore", pad, 2500);
        bot.BuildAndPlace("dir_cistern", pad, 1500);
        var hold = new CellCoord(26, 27);
        for (int guard = 0; guard < 20000 && !bot.Sim.GameOver; guard++)
        {
            if (guard % 200 == 0 && bot.Cash > 300)
            {   // the queues hold only a few items: keep topping them up
                for (int i = 0; i < 3; i++) bot.Train("dir_conscript");
                bot.Train("dir_ashthrower");
                if (guard % 400 == 0) bot.Train("dir_sapper");   // sappers are what cracks the searchlights
                if (guard % 400 == 200 && bot.Count("dir_assembly_bore") > 0) bot.Train("dir_rocket_truck");
            }
            // hold the pad until the patrol, the rail gang and the survey column (tick 6000) are beaten, then take the camp
            if (guard % 100 == 0) bot.OrderAll(CommandKind.AttackMove, guard >= 7000 ? camp : hold);
            bot.Idle();
            if (bot.Host.Outcome == MissionOutcome.Won) break;
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

    /// <summary>
    /// S48 C-12 (lb_05 "Under One Banner"): the whole column breaks the hold on the north-west road, comes home to meet the well's answer,
    /// then marches down the west road on the well. It never uses the Tinker, so the hidden Mother Rig bonus is not collected.
    /// </summary>
    private static void Lb05(MissionBot bot)
    {
        bot.Idle(5);
        var home = new CellCoord(9, 10);
        var outpost = new CellCoord(18, 23);
        var well = new CellCoord(11, 43);

        // 1. the hold on the road: attack-move at it until its Dynamo Rig and Muster Rig are down.
        for (int t = 0; t < 9000 && !bot.Sim.GameOver && !bot.Done("outpost"); t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, outpost);
            bot.Idle(99);
        }
        Need(bot.Done("outpost"), bot, "the hold on the road");

        // 2. back to the camp: the well's answer comes up the west road and from its army.
        for (int t = 0; t < 6000 && !bot.Sim.GameOver && !AnswerBroken(bot); t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, NearestFoe(bot.Sim, home, p => p == 1, 22) ?? home);   // only what has come up the road, not the well's own guards
            bot.Idle(99);
        }
        Need(AnswerBroken(bot), bot, "the well's answer");

        // 3. down the west road on the well.
        for (int t = 0; t < 12000 && !bot.Sim.GameOver && bot.Host.Outcome == MissionOutcome.None; t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, well);
            bot.Idle(99);
        }
    }

    private static bool AnswerBroken(MissionBot bot) => bot.Host.Messages.Any(x => x.Text.Contains("The answer is broken", StringComparison.Ordinal));

    private static EntityId? PackedRig(Simulation s)
    {
        int type = SimRules.IndexOf(s.Setup.Rules.Vehicles, "lb_mother_rig_packed", v => v.Id);
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Units.TypeIndex[i] == type) return s.Units.Slots.IdOf(i);
        return null;
    }

    /// <summary>
    /// S48 lb_06 "Salvage Rights": the war band attack-moves east along the train lane (shooting the cars open) while the haulers are sent to
    /// the nearest live wreck whenever they are idle; the walk ends when the script banks 1000 ash and wins.
    /// </summary>
    private static void Lb06(MissionBot bot)
    {
        const int limit = 8000;
        for (int t = 0; t < limit && !bot.Sim.GameOver && bot.Host.Outcome == MissionOutcome.None; t += 20)
        {
            Simulation s = bot.Sim;
            // the band shoots the nearest car still standing (attack-move to its cell), then goes back to guard the Yard against the scouts
            if (t % 60 == 0)
            {
                CellCoord? car = null;
                int best = int.MaxValue;
                for (int i = 1; i < s.Units.Capacity; i++)
                {
                    if (!s.Units.Slots.IsIndexAlive(i) || s.Units.Player[i] != 1) continue;
                    string id = s.Setup.Rules.Vehicles[s.Units.TypeIndex[i]].Id;
                    if (id != "auth_scraper" && id != "auth_carrier") continue;
                    CellCoord c = s.Units.Position[i].ToCell();
                    if (c.X < best) { best = c.X; car = c; }
                }
                bot.OrderAll(CommandKind.AttackMove, car ?? new CellCoord(43, 28));
            }
            if (t % 40 == 0)
            {
                int haulerType = SimRules.IndexOf(s.Setup.Rules.Vehicles, "lb_hauler", v => v.Id);
                for (int i = 1; i < s.Units.Capacity; i++)
                {
                    if (!s.Units.Slots.IsIndexAlive(i) || s.Units.Player[i] != 0 || s.Units.TypeIndex[i] != haulerType) continue;
                    MissionKind k = s.Units.Mission[i];
                    if (k == MissionKind.Salvage || k == MissionKind.Return) continue;
                    CellCoord from = s.Units.Position[i].ToCell();
                    int best = 0; long bestD = long.MaxValue;
                    for (int w = 1; w < s.Salvage.TicksLeft.Length; w++)
                    {
                        if (s.Salvage.TicksLeft[w] <= 0 || s.Salvage.Value[w] <= 0) continue;
                        long dx = s.Salvage.CellX[w] - from.X, dy = s.Salvage.CellY[w] - from.Y;
                        if (dx * dx + dy * dy < bestD) { bestD = dx * dx + dy * dy; best = w; }
                    }
                    if (best != 0) bot.Send(CommandKind.Harvest, s.Units.Slots.IdOf(i), new CellCoord(s.Salvage.CellX[best], s.Salvage.CellY[best]), (int)ActorKind.Vehicle);
                }
            }
            bot.Idle(20);
        }
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

    private static int PowerDown(MissionBot bot) => bot.Host.Messages.Count(x => x.Text.Contains("Power Plant", StringComparison.Ordinal));

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
            if (guard % 60 == 0) bot.Sim.Shipments.LastArrivalTick[0] = bot.Sim.Tick;   // M-10: stands in for the convoy that keeps the stores stocked (no Anchorage here)
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
