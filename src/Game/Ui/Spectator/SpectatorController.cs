// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Host;
using Rts.Game.Controls;
using Rts.Game.Input;

namespace Rts.Game.Ui.Spectator;

/// <summary>
/// The spectator's controls over a <see cref="SimHost"/> (design 08 §2): V cycles the viewer (-1, 0, 1, … N-1), F2 toggles full
/// map vision against the fog of the player picked in the stats panel, keys 1 to 4 set 1x/2x/4x/8x, Space pauses. All of it is
/// front-end pacing and view choice; none of it changes the simulation or its hash.
/// </summary>
public sealed class SpectatorController
{
    private readonly SimHost _host;

    /// <summary>Wraps <paramref name="host"/> and marks it <see cref="SimHost.Spectator"/> (input orders are refused).</summary>
    public SpectatorController(SimHost host)
    {
        _host = host;
        host.Spectator = true;
    }

    /// <summary>The player F2 shows when it leaves full vision: the last fog viewed, or the row clicked in the panel.</summary>
    public int PickedPlayer { get; private set; }

    /// <summary>True while the detail table (Tab) is shown.</summary>
    public bool ShowDetail { get; set; }

    /// <summary>V: next viewer in the order -1, 0, 1, … N-1 (see <see cref="SimHost.CycleViewer"/>).</summary>
    public void CycleViewer()
    {
        _host.CycleViewer();
        if (_host.Viewer >= 0) PickedPlayer = _host.Viewer;
    }

    /// <summary>F2: full map vision when a fog is shown, else the fog of <see cref="PickedPlayer"/>.</summary>
    public void ToggleVision()
    {
        if (_host.Viewer < 0)
        {
            _host.SetViewer(PickedPlayer);
        }
        else
        {
            PickedPlayer = _host.Viewer;
            _host.SetViewer(-1);
        }
    }

    /// <summary>Picks a player in the stats panel (the viewer stays as it is; F2 will show this player's fog).</summary>
    public void Pick(int player) => PickedPlayer = Math.Clamp(player, 0, Math.Max(0, _host.ViewerCount - 1));

    /// <summary>Keys 1..4: index 0..3 into <see cref="SimHost.SpeedSteps"/>.</summary>
    public void SetSpeedStep(int index) => _host.SetSpeedStep(index);

    /// <summary>Space.</summary>
    public void TogglePause() => _host.Paused = !_host.Paused;

    /// <summary>The speed as shown on screen, e.g. "4x" or "PAUSED".</summary>
    public string SpeedLabel => _host.Paused ? "PAUSED" : $"{_host.SpeedMultiplier}x";

    /// <summary>The viewer as shown on screen: "full vision" or "P2 fog".</summary>
    public string ViewLabel => _host.Viewer < 0 ? "full vision" : $"P{_host.Viewer + 1} fog";

    /// <summary>Reads V, F2, 1..4, Space and Tab from <paramref name="input"/> and consumes the ones it used.</summary>
    public void Handle(InputFrame input)
    {
        if (input.ConsumePressed(GameAction.SpecView)) CycleViewer();
        if (input.ConsumePressed(GameAction.DebugFog)) ToggleVision();
        if (input.ConsumePressed(GameAction.Pause)) TogglePause();
        if (input.ConsumePressed(GameAction.SpecDetail)) ShowDetail = !ShowDetail;
        GameAction[] speedKeys = { GameAction.SpecSpeed1, GameAction.SpecSpeed2, GameAction.SpecSpeed4, GameAction.SpecSpeed8 };
        for (int i = 0; i < speedKeys.Length; i++)
            if (input.ConsumePressed(speedKeys[i])) SetSpeedStep(i);
    }
}
