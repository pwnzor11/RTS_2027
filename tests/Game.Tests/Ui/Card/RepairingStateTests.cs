// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Card;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Ui.Card;

/// <summary>GAMEUI-06: a building whose repair is switched on shows "Repairing" on the selection card (read-only view path).</summary>
public sealed class RepairingStateTests
{
    private readonly string _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private readonly SimSnapshotSource _source;

    public RepairingStateTests()
    {
        _source = new SimSnapshotSource(SkirmishGame.Create(_rulesDir, 0, 1), _rulesDir);
    }

    [Fact]
    public void A_building_under_repair_is_flagged_on_the_snapshot_and_shown_on_the_card()
    {
        BuildingView yard = _source.Current.Buildings.First(b => b.Player == 0 && !b.Ghost);
        Assert.False(yard.Repairing);
        _source.Sim.Buildings.Health[yard.Id.Index] = 1;   // damaged, so the repair does not finish at once
        _source.Host.Issue(SidebarCommands.Repair(0, yard.Id));
        _source.StepTicks(1);

        BuildingView after = _source.Current.Buildings.First(b => b.Id == yard.Id);
        Assert.True(after.Repairing);

        CardModel m = CardModelBuilder.Build(_source, new Selection(), 0, yard.Id);
        Assert.Contains(m.Lines, l => l == "Repairing");
        Assert.Contains(m.Buttons, b => b.Kind == CardButtonKind.Repair && b.Active);
    }

    [Fact]
    public void A_building_not_under_repair_shows_no_repairing_state()
    {
        BuildingView yard = _source.Current.Buildings.First(b => b.Player == 0 && !b.Ghost);
        CardModel m = CardModelBuilder.Build(_source, new Selection(), 0, yard.Id);
        Assert.DoesNotContain(m.Lines, l => l == "Repairing");
        Assert.All(m.Buttons.Where(b => b.Kind == CardButtonKind.Repair), b => Assert.False(b.Active));
    }
}
