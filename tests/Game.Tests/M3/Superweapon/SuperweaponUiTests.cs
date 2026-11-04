// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui.Superweapon;
using Rts.Sim.Core;

namespace Rts.Game.Tests.M3.Superweapon;

/// <summary>M3-13 game side: button states, targeting mode and the flash/shake model.</summary>
public sealed class SuperweaponUiTests
{
    private static readonly EntityId Id = new(3, 1);

    private static PlayerHud Hud(int charge, bool paused = false) =>
        new(0, 0, 0, 0, 0, Array.Empty<QueueView>(), Array.Empty<(Rts.Sim.Rules.ProductionCategory, int)>(), false)
        {
            Superweapons = new[] { new SuperweaponView(Id, 7, "dir_sunrise", charge, 200, charge >= 200, paused, 8) },
        };

    [Fact]
    public void Buttons_ShowProgressPausedAndReady()
    {
        SuperweaponButton c = Assert.Single(SuperweaponButtons.From(Hud(50)));
        Assert.Equal((SuperweaponState.Charging, "25%", 250, "SUNRISE", 8), (c.State, c.Status, c.ProgressPermille, c.Caption, c.Range));
        Assert.Equal(SuperweaponState.Paused, SuperweaponButtons.From(Hud(50, paused: true))[0].State);
        Assert.Equal("PAUSED", SuperweaponButtons.From(Hud(50, paused: true))[0].Status);
        SuperweaponButton r = SuperweaponButtons.From(Hud(200))[0];
        Assert.Equal((SuperweaponState.Ready, "READY", 1000), (r.State, r.Status, r.ProgressPermille));
    }

    [Fact]
    public void Targeting_OnlyReadyArms_AndClickIssuesSpecial()
    {
        var t = new SuperweaponTargeting();
        t.ClickButton(SuperweaponButtons.From(Hud(100))[0]);
        Assert.False(t.IsArmed);
        t.ClickButton(SuperweaponButtons.From(Hud(200))[0]);
        Assert.True(t.IsArmed);
        Command? cmd = t.ClickMap(2, new CellCoord(9, 4));
        Assert.NotNull(cmd);
        Assert.Equal((CommandKind.Special, Id, new CellCoord(9, 4), (byte)2), (cmd!.Value.Kind, cmd.Value.Subject, cmd.Value.Cell, cmd.Value.Player));
        Assert.False(t.IsArmed);
        Assert.Null(t.ClickMap(2, new CellCoord(1, 1)));
    }

    [Fact]
    public void Targeting_VetoKeepsArmed_CancelAndRefreshDisarm()
    {
        var t = new SuperweaponTargeting();
        SuperweaponButton ready = SuperweaponButtons.From(Hud(200))[0];
        t.ClickButton(ready);
        Assert.Null(t.ClickMap(0, new CellCoord(1, 1), (_, _) => false));
        Assert.True(t.IsArmed);
        t.ClickButton(ready);   // click again disarms
        Assert.False(t.IsArmed);
        t.ClickButton(ready);
        t.Cancel();
        Assert.False(t.IsArmed);
        t.ClickButton(ready);
        t.Refresh(SuperweaponButtons.From(Hud(0)));   // fired or lost power: no longer ready
        Assert.False(t.IsArmed);
    }

    [Fact]
    public void Fx_FlashAndShakeStartOnTheEventAndFade()
    {
        var fx = new SuperweaponFx();
        fx.Observe(10, new[] { new FxEvent(FxKind.Detonation, default, 3) });
        Assert.Equal(0, fx.FlashAlpha(10));
        fx.Observe(11, new[] { new FxEvent(FxKind.Superweapon, default, 5) });
        Assert.Equal(255, fx.FlashAlpha(11));
        Assert.True(fx.FlashAlpha(15) < 255 && fx.FlashAlpha(15) > 0);
        Assert.Equal(0, fx.FlashAlpha(11 + SuperweaponFx.FlashTicks));
        Assert.NotEqual(Vector2.Zero, fx.ShakeOffset(12));
        Assert.Equal(Vector2.Zero, fx.ShakeOffset(11 + SuperweaponFx.ShakeTicks));
        Assert.Equal(fx.ShakeOffset(14), fx.ShakeOffset(14));   // deterministic
    }
}
