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

    private sealed record Outcome(int FirstWave, int TargetAtWave, int Tier2Tick, int Tier3Tick, int AntiAir, int Waves, int MaxTier);

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
        int firstWave = -1, target = -1;
        for (int t = 0; t < ticks; t++)
        {
            driver.Collect(sim, pending);
            if (firstWave < 0 && ai.Waves > 0) { firstWave = sim.Tick; target = ai.TargetPlayer; if (stopAtWave > 0) break; }
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
        }
        int antiAir = CountAntiAir(sim, faction);
        log.WriteLine($"directive={(arg is null ? "none" : arg.ToString())} firstWave={firstWave} target={target} tier2={ai.Tier2Tick} tier3={ai.Tier3Tick} antiAir={antiAir} waves={ai.Waves} at tick {sim.Tick}");
        return new Outcome(firstWave, target, ai.Tier2Tick, ai.Tier3Tick, antiAir, ai.Waves, ai.MaxTier);
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
        Assert.NotEqual(plain.TargetAtWave, allIn.TargetAtWave);
        Assert.Equal(other, allIn.TargetAtWave);
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
        Assert.True(tech.Tier2Tick > 0, "the tech focus reaches tier 2");
        Assert.True(plain.Tier2Tick < 0 || tech.Tier2Tick < plain.Tier2Tick - 600, $"tier 2 at {tech.Tier2Tick} with the directive, {plain.Tier2Tick} without");
    }

    [Fact]
    public void ADirectiveThatIsNeverSentChangesNothing()
    {
        Outcome a = Play(Tier(3), null, 4000), b = Play(Tier(3), null, 4000);
        Assert.Equal(a, b);
    }
}
