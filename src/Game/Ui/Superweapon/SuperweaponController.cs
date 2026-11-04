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

    /// <summary>How long the "ready" HUD message stays up, in ticks (4 s).</summary>
    public const int MessageTicks = 80;

    private readonly List<EntityId> _wasReady = new();
    private bool _seeded;
    private string _messageCaption = "";
    private int _messageUntil = int.MinValue;

    /// <summary>The buttons to show now: one per own superweapon building (charging, paused or ready). Empty = show nothing.</summary>
    public IReadOnlyList<SuperweaponButton> Buttons => _buttons;

    /// <summary>True while a shot is armed and the next map click fires it.</summary>
    public bool IsArmed => Targeting.IsArmed;

    /// <summary>
    /// Takes the local player's HUD (null = none) and drops the arm if its building is no longer ready. M3-21: a button that turns READY
    /// while the player watches raises the short "ready" message for <see cref="MessageTicks"/> ticks from <paramref name="tick"/>. The first
    /// update only learns the state, so a game loaded with a charged weapon does not announce it again.
    /// </summary>
    public void Update(PlayerHud? hud, int tick = 0)
    {
        _buttons = hud is null || hud.Superweapons.Count == 0 ? Array.Empty<SuperweaponButton>() : SuperweaponButtons.From(hud);
        Targeting.Refresh(_buttons);
        foreach (SuperweaponButton b in _buttons)
        {
            if (b.State != SuperweaponState.Ready || _wasReady.Contains(b.Building)) continue;
            if (_seeded) { _messageCaption = b.Caption; _messageUntil = tick + MessageTicks; }
        }
        _wasReady.Clear();
        foreach (SuperweaponButton b in _buttons) if (b.State == SuperweaponState.Ready) _wasReady.Add(b.Building);
        _seeded = hud is not null || _seeded;
    }

    /// <summary>M3-21: the text key of the HUD message to show at <paramref name="tick"/> (<see cref="SuperweaponText.ReadyKey"/>), or null when none.</summary>
    public string? MessageKey(int tick) => tick < _messageUntil ? SuperweaponText.ReadyKey : null;

    /// <summary>M3-21: the placeholder text of the current HUD message at <paramref name="tick"/>, or null when none.</summary>
    public string? MessageText(int tick) => MessageKey(tick) is { } key ? SuperweaponText.Placeholder(key, _messageCaption) : null;

    /// <summary>M3-21: blast radius in cells of the armed superweapon (0 when none is armed or the view gave no radius).</summary>
    public int ArmedBlastRadius
    {
        get
        {
            if (!Targeting.IsArmed) return 0;
            foreach (SuperweaponButton b in _buttons) if (b.Building == Targeting.Armed) return b.BlastRadius;
            return 0;
        }
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
