// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;
using Rts.Tools;

namespace Rts.Tools.Tests;

/// <summary>s41 (M13-43): every unit-v-unit fixture in data/balance/duels.toml stays inside its band, and the duel harness is deterministic.</summary>
public sealed class DuelFixtureTests
{
    private static readonly string RulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private static readonly RuleSet Rules = RulesLoader.Load(RulesDir);

    public static TheoryData<int> Indices()
    {
        var d = new TheoryData<int>();
        for (int i = 0; i < DuelFixture.Load(DuelFixture.PathBesideRules(RulesDir)).Count; i++) d.Add(i);
        return d;
    }

    [Theory]
    [MemberData(nameof(Indices))]
    public void FixtureHoldsItsBand(int index)
    {
        DuelFixture f = DuelFixture.Load(DuelFixture.PathBesideRules(RulesDir))[index];
        DuelMatrix.Summary s = DuelMatrix.Play(Rules.Sim, Rules.RulesHash, f.A, f.B, f.Budget, seeds: 3);
        Assert.True(s.MeanScore >= f.Min && s.MeanScore <= f.Max,
            $"{f.Why}: score {s.MeanScore} outside [{f.Min}, {f.Max}] ({f.A} v {f.B}, {s.WinsA}-{s.WinsB}-{s.Draws}, spent {s.SpentA} v {s.SpentB})");
    }

    [Fact]
    public void SidesSpendWithinHalfAUnitOfTheBudget()
    {
        var a = DuelMatrix.Fight(Rules.Sim, Rules.RulesHash, "auth_tank", "auth_rifleman", 3000, 0, false);
        Assert.Equal(3200, a.SpentA);   // 4 tanks (3 200) is nearer 3 000 than 3 (2 400)
        Assert.Equal(3000, a.SpentB);
    }

    [Fact]
    public void ADuelIsDeterministic()
    {
        var x = DuelMatrix.Fight(Rules.Sim, Rules.RulesHash, "auth_rocketeer", "dir_rocket_truck", 4000, 1, true);
        var y = DuelMatrix.Fight(Rules.Sim, Rules.RulesHash, "auth_rocketeer", "dir_rocket_truck", 4000, 1, true);
        Assert.Equal(x, y);
    }
}
