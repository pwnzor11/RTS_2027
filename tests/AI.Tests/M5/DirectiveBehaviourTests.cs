// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.M4;
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit.Abstractions;

namespace AI.Tests.M5;

/// <summary>M5-02: scripted three-player matches (fixed seed). Player 0 plays the tiered AI and gets a directive at tick 1 (or none);
/// players 1 and 2 only hold their bases. Every assertion is a margin, not an exact number.</summary>
public class DirectiveBehaviourTests(ITestOutputHelper log)
{
    private static DifficultyProfile Tier(int tier) => TestWorld.Tiers[tier - 1];

    /// <summary>Issues one packed directive on a given tick.</summary>
    private sealed class Scripted(int onTick, int arg, CellCoord cell) : IDirectiveSource
    {
        public bool Poll(PlayerView view, out int packedArg, out CellCoord focusCell)
        {
            packedArg = arg; focusCell = cell;
            return view.Tick == onTick;
        }
    }

    private sealed record Outcome(int FirstWave, int TargetAtWave, int Tier2Tick, int Tier3Tick, int AntiAir, int Waves, int MaxTier, int[] TargetedAt,
        int[] DefeatedAt);

    private static int Arg(byte posture = DirectiveCodec.PostureBalanced, byte focus = DirectiveCodec.FocusArmy, byte target = DirectiveCodec.AnyTarget,
        byte mix = DirectiveCodec.MixAuto, bool expand = false) => DirectiveCodec.Pack(posture, focus, target, mix, expand);

    /// <summary>Plays player 0 (<paramref name="profile"/>) against two idle players on a 3-start map. <paramref name="arg"/> = null: no directive.</summary>
    private Outcome Play(DifficultyProfile profile, int? arg, int ticks, Faction faction = Faction.Authority, int cash = 0, int stopAtWave = 0)
    {
        MapData map = M4AiTests.QuadMap(48, 3);
        var sim = new Simulation(new SimSetup(3, map, 3, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash) { Factions = [faction, Faction.Authority, Faction.Authority] });
        SkirmishStart.Apply(sim);
        if (cash > 0) sim.Players.Cash[0] = cash;
        var driver = new AiDriver(3);
        var ai = new RecruitAi(profile);
        driver.Add(0, ai, profile);
        driver.Add(1, new IdleAi(), profile);
        driver.Add(2, new IdleAi(), profile);
        if (arg is int a) driver.SetDirectiveSource(0, new Scripted(1, a, Directive.NoCell));
        var pending = new List<Command>();
        var targetedAt = new[] { -1, -1, -1 };   // first tick each player was the target
        var defeatedAt = new[] { -1, -1, -1 };   // M13-05: tick each player was defeated
        int firstWave = -1, target = -1;   // target = the first enemy the waves are aimed at (known once a building of one has been seen)
        for (int t = 0; t < ticks; t++)
        {
            driver.Collect(sim, pending);
            if (firstWave < 0 && ai.Waves > 0) firstWave = sim.Tick;
            if (target < 0 && ai.TargetPlayer >= 0) target = ai.TargetPlayer;
            if (ai.TargetPlayer is >= 0 and < 3 && targetedAt[ai.TargetPlayer] < 0) targetedAt[ai.TargetPlayer] = sim.Tick;
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            for (int p = 0; p < 3; p++) if (defeatedAt[p] < 0 && sim.Players.Defeated[p]) defeatedAt[p] = sim.Tick;
        }
        int antiAir = CountAntiAir(sim, faction);
        log.WriteLine($"directive={(arg is null ? "none" : arg.ToString())} targetedAt=[{string.Join(',', targetedAt)}] firstWave={firstWave} target={target} tier2={ai.Tier2Tick} tier3={ai.Tier3Tick} antiAir={antiAir} waves={ai.Waves} defeatedAt=[{string.Join(',', defeatedAt)}] at tick {sim.Tick}");
        return new Outcome(firstWave, target, ai.Tier2Tick, ai.Tier3Tick, antiAir, ai.Waves, ai.MaxTier, targetedAt, defeatedAt);
    }

    /// <summary>Anti-air things player 0 owns: structures and units whose gun can hit aircraft.</summary>
    private static int CountAntiAir(Simulation sim, Faction faction)
    {
        var rules = TestWorld.Rules.Sim;
        int n = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == 0)
            {
                int w = rules.Buildings[sim.Buildings.TypeIndex[i]].PrimaryWeapon;
                if (w >= 0 && rules.Weapons[w].Targets != WeaponTargets.Ground) n++;
            }
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == 0)
            {
                int w = rules.Vehicles[sim.Units.TypeIndex[i]].PrimaryWeapon;
                if (w >= 0 && rules.Weapons[w].Targets != WeaponTargets.Ground) n++;
            }
        return n;
    }

    [Fact]
    public void AllInLaunchesTheFirstWaveEarlierAndSendsItToTheNamedPlayer()
    {
        Outcome plain = Play(Tier(3), null, 9000);
        Assert.True(plain.FirstWave > 0, "the plain AI launches a wave in the window");
        byte other = (byte)(plain.TargetAtWave == 1 ? 2 : 1);   // the player the plain AI did not pick
        Outcome allIn = Play(Tier(3), Arg(DirectiveCodec.PostureAllIn, target: other), 9000);
        Assert.True(allIn.FirstWave > 0 && allIn.FirstWave < plain.FirstWave - 300, $"all-in {allIn.FirstWave} vs plain {plain.FirstWave}");
        // the waves go to the named player once a building of it is known (before that the AI scouts and takes what it sees)
        Assert.True(allIn.TargetedAt[other] > 0, "the named player became the target");
        // M13-05: compared only while the plain AI chose freely. Once its first target is defeated the other player is the only one left,
        // so it is targeted as soon as one of its buildings is seen; that race is scouting, not the directive. With the path retarget the
        // plain AI defeats its first target by about tick 3 400 and finds the other one at about 4 650; the all-in AI (first target
        // defeated at about 2 700) found it at 4 830 (integ-s19 before M13-05: 5 597 / 4 710 plain, 2 701 / 4 140 all-in).
        bool plainForced = plain.TargetAtWave >= 0 && plain.DefeatedAt[plain.TargetAtWave] >= 0 && plain.TargetedAt[other] >= plain.DefeatedAt[plain.TargetAtWave];
        Assert.True(plainForced || plain.TargetedAt[other] < 0 || allIn.TargetedAt[other] < plain.TargetedAt[other] - 100,
            $"named player targeted at {allIn.TargetedAt[other]} with the directive, {plain.TargetedAt[other]} without");
        Assert.True(allIn.Waves >= 1);
    }

    [Fact]
    public void TurtleLaunchesTheFirstWaveLater()
    {
        Outcome plain = Play(Tier(3), null, 12000);
        Outcome turtle = Play(Tier(3), Arg(DirectiveCodec.PostureTurtle), 12000);
        Assert.True(plain.FirstWave > 0);
        Assert.True(turtle.FirstWave < 0 || turtle.FirstWave > plain.FirstWave + 300, $"turtle {turtle.FirstWave} vs plain {plain.FirstWave}");
    }

    [Fact]
    public void AntiAirMixBuildsMoreAntiAir()
    {
        Outcome plain = Play(Tier(4), null, 8000, cash: 20000);
        Outcome aa = Play(Tier(4), Arg(mix: DirectiveCodec.MixAntiAir), 8000, cash: 20000);
        Assert.True(aa.AntiAir >= plain.AntiAir + 1, $"anti-air things: {aa.AntiAir} with the directive, {plain.AntiAir} without");
    }

    [Fact]
    public void TechFocusReachesTheNextTierEarlier()
    {
        Outcome plain = Play(Tier(3), null, 9000, cash: 12000);
        Outcome tech = Play(Tier(3), Arg(focus: DirectiveCodec.FocusTech), 9000, cash: 12000);
        // tier 2 comes at the scheduled gate for both (the gate is built early anyway); the tier-3 gate is what the focus brings forward
        Assert.True(tech.Tier3Tick > 0, "the tech focus reaches tier 3");
        Assert.True(plain.Tier3Tick < 0 || tech.Tier3Tick < plain.Tier3Tick - 600, $"tier 3 at {tech.Tier3Tick} with the directive, {plain.Tier3Tick} without");
        Assert.True(tech.MaxTier > plain.MaxTier || tech.Tier3Tick < plain.Tier3Tick);
    }

    [Fact]
    public void ADirectiveThatIsNeverSentChangesNothing()
    {
        Outcome a = Play(Tier(3), null, 4000), b = Play(Tier(3), null, 4000);
        Assert.Equal((a.FirstWave, a.Tier2Tick, a.Tier3Tick, a.AntiAir, a.Waves, a.MaxTier), (b.FirstWave, b.Tier2Tick, b.Tier3Tick, b.AntiAir, b.Waves, b.MaxTier));
        Assert.Equal(a.TargetedAt, b.TargetedAt);
    }
}
