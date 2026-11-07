// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;

namespace Rts.Game.Controls;

/// <summary>
/// The one table of in-game controls (M11-03). The Controls page and the F1 overlay draw it, the order keys and the camera bookmark keys
/// that <c>GameScreen</c> reads come from it, and a test scans the input code for every <c>Keys.X</c> it handles and fails when the table
/// and the code disagree, so the on-screen list cannot drift from the real keys.
/// </summary>
public static class GameKeys
{
    /// <summary>Group name: camera and view.</summary>
    public const string Camera = "Camera";

    /// <summary>Group name: selecting units.</summary>
    public const string Selection = "Selection";

    /// <summary>Group name: giving orders.</summary>
    public const string Orders = "Orders";

    /// <summary>Group name: building, pause, debug and everything else.</summary>
    public const string System = "Build & System";

    /// <summary>The group names in display order.</summary>
    public static readonly IReadOnlyList<string> Groups = new[] { Camera, Selection, Orders, System };

    /// <summary>The order keys <c>GameScreen</c> forwards to <see cref="OrderController.KeyPressed"/>: A, S, G, X, D.</summary>
    public static readonly Keys[] OrderKeys = { Keys.A, Keys.S, Keys.G, Keys.X, Keys.D };

    /// <summary>The table, grouped in <see cref="Groups"/> order.</summary>
    public static readonly IReadOnlyList<GameKeyBinding> Table = new GameKeyBinding[]
    {
        new(Camera, "Arrow keys", "Scroll the view", Keys.Left, Keys.Right, Keys.Up, Keys.Down),
        new(Camera, "Mouse at screen edge", "Scroll the view (Settings > Controls: Edge scroll)"),
        new(Camera, "Shift (held)", "Scroll twice as fast", Keys.LeftShift, Keys.RightShift),
        new(Camera, "Middle-drag", "Drag the view"),
        new(Camera, "Mouse wheel", "Zoom"),
        new(Camera, "Minimap click or drag", "Move the view"),
        new(Selection, "Left click", "Select a unit or structure"),
        new(Selection, "Left drag", "Select units in a box"),
        new(Orders, "Right click", "Move, attack, enter, salvage; set a factory's rally point"),
        new(Orders, "A", "Attack-move: press A, then right-click the ground", Keys.A),
        new(Orders, "S", "Stop", Keys.S),
        new(Orders, "G", "Guard here", Keys.G),
        new(Orders, "X", "Scatter", Keys.X),
        new(Orders, "D", "Deploy / unload", Keys.D),
        new(Orders, "K", "Pack or unpack a rig", Keys.K),
        new(System, "Q W E R T Y", "Sidebar tabs (Y: aircraft, when you have any)", Keys.Q, Keys.W, Keys.E, Keys.R, Keys.T, Keys.Y),
        new(System, "Esc", "Leave a mode, then open the pause menu", Keys.Escape),
        new(System, "Space", "Pause", Keys.Space),
        new(System, "F1 or ?", "This list (F1 opens the console in the sandbox)", Keys.F1),
        new(System, "Enter", "Talk to an AI commander (when one is in the game)", Keys.Enter),
        new(System, "Sandbox/spectator: F2", "Fog viewer / full vision", Keys.F2),
        new(System, "Sandbox: F3 F4 F5", "Occupancy, paths and range overlays", Keys.F3, Keys.F4, Keys.F5),
        new(System, "Sandbox: .", "Step the simulation once", Keys.OemPeriod),
        new(System, "Sandbox: F9", "Start an AI-vs-AI spectator game", Keys.F9),
        new(System, "Spectator: V 1-4 Tab", "Next view, speed 1x 2x 4x 8x, detail", Keys.V, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.Tab),
    };

    /// <summary>Every key named in <see cref="Table"/>.</summary>
    public static IReadOnlySet<Keys> AllKeys { get; } = new HashSet<Keys>(Table.SelectMany(b => b.Handled));

    /// <summary>The rows of one group, in table order.</summary>
    public static IEnumerable<GameKeyBinding> InGroup(string group) => Table.Where(b => b.Group == group);

    /// <summary>
    /// True when the sandbox debug keys (F2 fog viewer, F3/F4/F10 overlays, '.' step, F9) may act: only in the sandbox and spectator games,
    /// never in a skirmish or a campaign mission (K2).
    /// </summary>
    /// <param name="skirmishOrMission">True for a skirmish, a mission or a loaded save of either.</param>
    public static bool DebugKeysAllowed(bool skirmishOrMission) => !skirmishOrMission;
}
