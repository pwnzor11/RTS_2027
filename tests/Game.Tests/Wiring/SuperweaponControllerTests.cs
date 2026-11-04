// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Superweapon;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Wiring;

/// <summary>State machine of the in-game Sunrise Device UI (M3-18).</summary>
public sealed class SuperweaponControllerTests
{
    private static readonly EntityId Id = new(3, 1);

    private static PlayerHud Hud(int charge, bool owns = true) =>
        new(0, 0, 0, 0, 0, Array.Empty<QueueView>(), Array.Empty<(Rts.Sim.Rules.ProductionCategory, int)>(), false)
        {
            Superweapons = owns ? new[] { new SuperweaponView(Id, 7, "dir_sunrise", charge, 200, charge >= 200, false, 8) }
                                : Array.Empty<SuperweaponView>(),
        };

    [Fact]
    public void No_buttons_without_a_superweapon_or_a_hud()
    {
        var c = new SuperweaponController();
        c.Update(null);
        Assert.Empty(c.Buttons);
        c.Update(Hud(0, owns: false));
        Assert.Empty(c.Buttons);
    }

    [Fact]
    public void Charging_shows_a_button_but_a_click_does_not_arm()
    {
        var c = new SuperweaponController();
        c.Update(Hud(50));
        Assert.Single(c.Buttons);
        c.ClickButton(0);
        Assert.False(c.IsArmed);
        Assert.Null(c.ClickMap(0, new CellCoord(4, 4)));
    }

    [Fact]
    public void Ready_click_arms_then_left_click_issues_Special_and_disarms()
    {
        var c = new SuperweaponController();
        c.Update(Hud(200));
        c.ClickButton(0);
        Assert.True(c.IsArmed);

        Command? cmd = c.ClickMap(0, new CellCoord(9, 7));

        Assert.NotNull(cmd);
        Assert.Equal(CommandKind.Special, cmd!.Value.Kind);
        Assert.Equal(Id, cmd.Value.Subject);
        Assert.Equal(new CellCoord(9, 7), cmd.Value.Cell);
        Assert.False(c.IsArmed);
    }

    [Fact]
    public void A_vetoed_click_keeps_the_arm_and_cancel_clears_it()
    {
        var c = new SuperweaponController();
        c.Update(Hud(200));
        c.ClickButton(0);
        Assert.Null(c.ClickMap(0, new CellCoord(1, 1), (_, _) => false));
        Assert.True(c.IsArmed);
        Assert.True(c.Cancel());
        Assert.False(c.IsArmed);
        Assert.False(c.Cancel());   // nothing armed: the caller lets Esc open the pause menu
    }

    [Fact]
    public void The_arm_drops_when_the_building_stops_being_ready()
    {
        var c = new SuperweaponController();
        c.Update(Hud(200));
        c.ClickButton(0);
        c.Update(Hud(0));   // fired: charge restarted
        Assert.False(c.IsArmed);
        c.Update(Hud(200));
        c.ClickButton(0);
        c.Update(Hud(0, owns: false));   // destroyed
        Assert.False(c.IsArmed);
    }

    [Fact]
    public void A_superweapon_event_starts_the_flash()
    {
        var c = new SuperweaponController();
        c.Fx.Observe(100, new[] { new FxEvent(FxKind.Superweapon, default, 0) });
        Assert.True(c.Fx.FlashAlpha(100) > 0);
        Assert.Equal(0, c.Fx.FlashAlpha(100 + SuperweaponFx.FlashTicks));
    }
}
