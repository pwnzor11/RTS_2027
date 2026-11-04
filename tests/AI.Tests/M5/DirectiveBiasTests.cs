// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Sim.Core;

namespace AI.Tests.M5;

/// <summary>M5-02: the directive-to-knob table, alone (no sim).</summary>
public class DirectiveBiasTests
{
    private static Directive D(byte posture = DirectiveCodec.PostureBalanced, byte focus = DirectiveCodec.FocusArmy, byte target = DirectiveCodec.AnyTarget,
        byte mix = DirectiveCodec.MixAuto, bool expand = false, CellCoord? cell = null) =>
        new(true, posture, focus, target, mix, expand, cell ?? Directive.NoCell, 100);

    [Fact]
    public void NoDirectiveGivesTheValuesTheManagersUseToday()
    {
        DirectiveBias b = DirectiveBias.For(Directive.None);
        Assert.False(b.Set);
        Assert.Equal(1000, b.WavePermille);
        Assert.Equal(0, b.HomeGuard);
        Assert.Equal(1000, b.RetreatPermille);
        Assert.Equal(-1, b.TargetPlayer);
        Assert.Equal(0, b.MixPermille);
        Assert.Equal(0, b.UnitReserve);
        Assert.Equal(UpgradeManager.MinArmy, b.UpgradeMinArmy);
        Assert.Equal(UpgradeManager.Cushion, b.UpgradeCushion);
        Assert.Equal(1000, b.GatePermille);
        Assert.Equal(1000, b.Tier3ArmyPermille);
        Assert.False(b.ForceTech || b.EarlyLateTech || b.Expand);
        Assert.Equal(GarrisonManager.KeepInArmy, b.GarrisonKeep);
        Assert.Equal(GarrisonManager.RadiusCells, b.GarrisonRadius);
        Assert.False(b.HasAttackCell || b.HasRallyCell);
        Assert.Equal(DirectiveBias.Plain, b);
        // the helpers are identities
        Assert.Equal(10, b.Wave(10, 2));
        Assert.Equal(25, b.RetreatPct(25));
        Assert.Equal(0, b.RetreatPct(0));
    }

    [Fact]
    public void ABalancedArmyDirectiveChangesNothingButTheSetFlag()
    {
        Assert.Equal(DirectiveBias.Plain with { Set = true }, DirectiveBias.For(D()));
    }

    [Fact]
    public void PostureOrdersTheWaveThreshold()
    {
        int Of(byte p) => DirectiveBias.For(D(p)).Wave(10, 2);
        Assert.True(Of(DirectiveCodec.PostureTurtle) > Of(DirectiveCodec.PostureBalanced));
        Assert.Equal(10, Of(DirectiveCodec.PostureBalanced));
        Assert.True(Of(DirectiveCodec.PostureAggressive) < Of(DirectiveCodec.PostureBalanced));
        Assert.True(Of(DirectiveCodec.PostureAllIn) < Of(DirectiveCodec.PostureAggressive));
        Assert.Equal(2, DirectiveBias.For(D(DirectiveCodec.PostureAllIn)).Wave(3, 2));   // never below the floor
    }

    [Fact]
    public void TurtleGuardsAndRetreatsEarlyAllInNeverRetreats()
    {
        DirectiveBias turtle = DirectiveBias.For(D(DirectiveCodec.PostureTurtle)), allIn = DirectiveBias.For(D(DirectiveCodec.PostureAllIn));
        Assert.True(turtle.HomeGuard > 0);
        Assert.True(turtle.RetreatPct(25) > 25);
        Assert.Equal(0, allIn.HomeGuard);
        Assert.Equal(0, allIn.RetreatPct(25));
        Assert.Equal(100, turtle.RetreatPct(90));   // clamped
        Assert.True(turtle.GarrisonKeep < GarrisonManager.KeepInArmy);
        Assert.True(allIn.GarrisonKeep > GarrisonManager.KeepInArmy);
    }

    [Fact]
    public void FocusCellIsTheAttackPointForAggressivePostureAndTheRallyPointForTurtle()
    {
        var c = new CellCoord(20, 30);
        Assert.Equal(c, DirectiveBias.For(D(DirectiveCodec.PostureAggressive, cell: c)).AttackCell);
        Assert.Equal(c, DirectiveBias.For(D(DirectiveCodec.PostureAllIn, cell: c)).AttackCell);
        DirectiveBias t = DirectiveBias.For(D(DirectiveCodec.PostureTurtle, cell: c));
        Assert.Equal(c, t.RallyCell);
        Assert.False(t.HasAttackCell);
        DirectiveBias bal = DirectiveBias.For(D(DirectiveCodec.PostureBalanced, cell: c));
        Assert.False(bal.HasAttackCell || bal.HasRallyCell);
    }

    [Fact]
    public void FocusSetsTheSpendPriority()
    {
        DirectiveBias eco = DirectiveBias.For(D(focus: DirectiveCodec.FocusEconomy));
        DirectiveBias tech = DirectiveBias.For(D(focus: DirectiveCodec.FocusTech));
        DirectiveBias air = DirectiveBias.For(D(focus: DirectiveCodec.FocusAir));
        DirectiveBias sw = DirectiveBias.For(D(focus: DirectiveCodec.FocusSuperweapon));
        DirectiveBias army = DirectiveBias.For(D(focus: DirectiveCodec.FocusArmy));
        Assert.True(eco.Expand && eco.UnitReserve > 0);
        Assert.True(tech.GatePermille < 1000 && tech.ForceTech && tech.UpgradeMinArmy < UpgradeManager.MinArmy);
        Assert.True(air.EarlyLateTech && air.GatePermille < 1000);
        Assert.True(sw.EarlyLateTech && sw.ForceTech && sw.GatePermille < 1000);
        Assert.Equal(0, army.UnitReserve);
    }

    [Fact]
    public void TargetMixAndExpandPassThrough()
    {
        DirectiveBias b = DirectiveBias.For(D(target: 2, mix: DirectiveCodec.MixAntiAir, expand: true));
        Assert.Equal(2, b.TargetPlayer);
        Assert.Equal(DirectiveCodec.MixAntiAir, b.Mix);
        Assert.True(b.MixPermille > 0 && b.WantsAntiAir);
        Assert.True(b.Expand);
        Assert.True(b.GarrisonRadius > GarrisonManager.RadiusCells);
        Assert.Equal(-1, DirectiveBias.For(D()).TargetPlayer);
        Assert.False(DirectiveBias.For(D()).WantsAntiAir);
    }

    [Fact]
    public void OutOfRangeValuesCannotChangeTheAnswer()
    {
        // the sim clamps before the AI reads; a stray value must still not break the table
        DirectiveBias b = DirectiveBias.For(D(posture: 9, focus: 9, mix: 9));
        Assert.Equal(DirectiveBias.Plain with { Set = true }, b);
    }
}
