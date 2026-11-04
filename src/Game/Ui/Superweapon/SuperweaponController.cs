// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Superweapon;

/// <summary>
/// The pure state machine behind the in-game superweapon UI (M3-18): the buttons for the local player's HUD, the targeting mode
/// and the flash. GameScreen feeds it the HUD each frame and the clicks; it hands back the Special command to issue. No drawing,
/// no input types, so it is unit-tested.
/// </summary>
public sealed class SuperweaponController
{
    private IReadOnlyList<SuperweaponButton> _buttons = Array.Empty<SuperweaponButton>();

    /// <summary>The targeting mode.</summary>
    public SuperweaponTargeting Targeting { get; } = new();

    /// <summary>The flash and shake effect.</summary>
    public SuperweaponFx Fx { get; } = new();

    /// <summary>The buttons to show now: one per own superweapon building (charging, paused or ready). Empty = show nothing.</summary>
    public IReadOnlyList<SuperweaponButton> Buttons => _buttons;

    /// <summary>True while a shot is armed and the next map click fires it.</summary>
    public bool IsArmed => Targeting.IsArmed;

    /// <summary>Takes the local player's HUD (null = none) and drops the arm if its building is no longer ready.</summary>
    public void Update(PlayerHud? hud)
    {
        _buttons = hud is null || hud.Superweapons.Count == 0 ? Array.Empty<SuperweaponButton>() : SuperweaponButtons.From(hud);
        Targeting.Refresh(_buttons);
    }

    /// <summary>A click on button <paramref name="index"/>. A ready one arms targeting (again disarms); other states do nothing.</summary>
    public void ClickButton(int index)
    {
        if (index >= 0 && index < _buttons.Count) Targeting.ClickButton(_buttons[index]);
    }

    /// <summary>Left click on the map: the Special command to issue (and disarm), or null when not armed or the sim would refuse it.</summary>
    public Command? ClickMap(byte player, CellCoord cell, Func<EntityId, CellCoord, bool>? canFire = null) =>
        Targeting.ClickMap(player, cell, canFire);

    /// <summary>Right click or Escape. Returns true if a shot was armed (the caller then swallows the key or click).</summary>
    public bool Cancel()
    {
        bool was = Targeting.IsArmed;
        Targeting.Cancel();
        return was;
    }
}
