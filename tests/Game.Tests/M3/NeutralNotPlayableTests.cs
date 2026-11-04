// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M3;

/// <summary>Q-37: Faction.Neutral is never offered on the skirmish setup screen and is rejected by slot validation.</summary>
public sealed class NeutralNotPlayableTests
{
    [Fact]
    public void Faction_pick_list_cycles_Authority_and_Directorate_only()
    {
        var seen = new HashSet<Faction>();
        Faction f = Faction.Authority;
        for (int i = 0; i < 6; i++) { seen.Add(f); f = SkirmishSetupScreen.NextFaction(f, 1); }
        Assert.Equal(new[] { Faction.Authority, Faction.Directorate }, seen.OrderBy(x => x));
        Assert.Equal(Faction.Authority, SkirmishSetupScreen.NextFaction(Faction.Directorate, 1));
        Assert.Equal(Faction.Directorate, SkirmishSetupScreen.NextFaction(Faction.Authority, -1));
    }

    [Fact]
    public void A_neutral_slot_faction_fails_validation()
    {
        SkirmishSettings s = SkirmishSettings.Default;
        var slots = s.Slots.ToArray();
        slots[0] = slots[0] with { Faction = Faction.Neutral };
        var bad = s with { Slots = slots };
        Assert.Contains(bad.Validate(), e => e.Contains("faction"));
    }
}
