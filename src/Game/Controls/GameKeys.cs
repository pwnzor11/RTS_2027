// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;

namespace Rts.Game.Controls;

/// <summary>
/// The one table of in-game controls (M11-03, D-172 (6)). Every rebindable action is listed in <see cref="Infos"/> with its default key;
/// game code reads keys by <see cref="GameAction"/> through <see cref="BoundInput"/> and <see cref="Active"/>, and a test scans the input
/// code so that no literal <c>Keys.X</c> (other than the few fixed keys) and no unlisted action can creep in. The Controls page and the
/// F1 overlay draw <see cref="Rows"/>.
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

    /// <summary>The order actions <c>GameScreen</c> forwards to <see cref="OrderController.KeyPressed"/>.</summary>
    public static readonly GameAction[] OrderActions =
        { GameAction.AttackMove, GameAction.Stop, GameAction.Guard, GameAction.Scatter, GameAction.Deploy };

    /// <summary>
    /// Keys the input code may name literally because they cannot be rebound: Esc (leave / pause menu), F1 (help), Backspace and Enter
    /// (text editing in the chat box), Shift and Ctrl (modifiers). "Length" is an array length, not a key.
    /// </summary>
    public static readonly IReadOnlySet<string> FixedKeyNames = new HashSet<string>
    {
        "Escape", "F1", "Back", "Enter", "LeftShift", "RightShift", "LeftControl", "RightControl", "Length",
    };

    /// <summary>Every rebindable action with its default key, in <see cref="GameAction"/> order.</summary>
    public static readonly IReadOnlyList<ActionInfo> Infos = new ActionInfo[]
    {
        new(GameAction.ScrollLeft, "scroll_left", Camera, "Scroll the view left", Keys.Left),
        new(GameAction.ScrollRight, "scroll_right", Camera, "Scroll the view right", Keys.Right),
        new(GameAction.ScrollUp, "scroll_up", Camera, "Scroll the view up", Keys.Up),
        new(GameAction.ScrollDown, "scroll_down", Camera, "Scroll the view down", Keys.Down),
        new(GameAction.CentreBase, "centre_base", Camera, "Centre the view on your base", Keys.Home),
        new(GameAction.Bookmark1, "bookmark_1", Camera, "Camera bookmark 1 (Ctrl + key stores it)", Keys.F5),
        new(GameAction.Bookmark2, "bookmark_2", Camera, "Camera bookmark 2 (Ctrl + key stores it)", Keys.F6),
        new(GameAction.Bookmark3, "bookmark_3", Camera, "Camera bookmark 3 (Ctrl + key stores it)", Keys.F7),
        new(GameAction.Bookmark4, "bookmark_4", Camera, "Camera bookmark 4 (Ctrl + key stores it)", Keys.F8),
        new(GameAction.AttackMove, "attack_move", Orders, "Attack-move: press, then click the ground", Keys.A),
        new(GameAction.Stop, "stop", Orders, "Stop", Keys.S),
        new(GameAction.Guard, "guard", Orders, "Guard here", Keys.G),
        new(GameAction.Scatter, "scatter", Orders, "Scatter", Keys.X),
        new(GameAction.Deploy, "deploy", Orders, "Deploy / unload", Keys.D),
        new(GameAction.Pack, "pack", Orders, "Pack or unpack a rig", Keys.K),
        new(GameAction.TabStructures, "tab_structures", System, "Sidebar tab: structures", Keys.Q),
        new(GameAction.TabDefences, "tab_defences", System, "Sidebar tab: defences", Keys.W),
        new(GameAction.TabInfantry, "tab_infantry", System, "Sidebar tab: infantry", Keys.E),
        new(GameAction.TabVehicles, "tab_vehicles", System, "Sidebar tab: vehicles", Keys.R),
        new(GameAction.TabUpgrades, "tab_upgrades", System, "Sidebar tab: upgrades", Keys.T),
        new(GameAction.TabAircraft, "tab_aircraft", System, "Sidebar tab: aircraft (when you have any)", Keys.Y),
        new(GameAction.Pause, "pause", System, "Pause", Keys.Space, KeyContext.Both),
        new(GameAction.Talk, "talk", System, "Talk to an AI commander (when one is in the game)", Keys.Enter),
        new(GameAction.DebugFog, "debug_fog", System, "Sandbox / spectator: fog viewer, full vision", Keys.F2, KeyContext.Both),
        new(GameAction.DebugOccupancy, "debug_occupancy", System, "Sandbox: occupancy overlay", Keys.F3),
        new(GameAction.DebugPaths, "debug_paths", System, "Sandbox: paths overlay", Keys.F4),
        new(GameAction.DebugRanges, "debug_ranges", System, "Sandbox: weapon range overlay", Keys.F10),
        new(GameAction.DebugStep, "debug_step", System, "Sandbox: step the simulation once", Keys.OemPeriod),
        new(GameAction.DebugSpectator, "debug_spectator", System, "Sandbox: start an AI-vs-AI spectator game", Keys.F9),
        new(GameAction.SpecView, "spec_view", System, "Spectator: next view", Keys.V, KeyContext.Spectator),
        new(GameAction.SpecSpeed1, "spec_speed_1", System, "Spectator: speed 1x", Keys.D1, KeyContext.Spectator),
        new(GameAction.SpecSpeed2, "spec_speed_2", System, "Spectator: speed 2x", Keys.D2, KeyContext.Spectator),
        new(GameAction.SpecSpeed4, "spec_speed_4", System, "Spectator: speed 4x", Keys.D3, KeyContext.Spectator),
        new(GameAction.SpecSpeed8, "spec_speed_8", System, "Spectator: speed 8x", Keys.D4, KeyContext.Spectator),
        new(GameAction.SpecDetail, "spec_detail", System, "Spectator: detail panel", Keys.Tab, KeyContext.Spectator),
        new(GameAction.ObjJump, "obj_jump", System, "Mission: jump to the objective", Keys.J),
        new(GameAction.ObjCycle, "obj_cycle", System, "Mission: next objective", Keys.Tab),
        new(GameAction.ObjList, "obj_list", System, "Mission: objective list", Keys.O),
    };

    /// <summary>The Controls page lines, grouped in <see cref="Groups"/> order: rebindable actions and fixed inputs shown for reference.</summary>
    public static readonly IReadOnlyList<ControlRow> Rows = BuildRows();

    private static KeyBindings? _active;

    /// <summary>The bindings in force. <c>GameServices.Settings</c> keeps this in step with the saved settings.</summary>
    public static KeyBindings Active
    {
        get => _active ?? KeyBindings.Default;
        set => _active = value;
    }

    /// <summary>The info row of one action.</summary>
    public static ActionInfo InfoOf(GameAction action) => Infos[(int)action];

    /// <summary>The rows of one group, in table order.</summary>
    public static IEnumerable<ControlRow> InGroup(string group) => Rows.Where(r => r.Group == group);

    /// <summary>
    /// True when the sandbox debug keys (F2 fog viewer, F3/F4/F10 overlays, '.' step, F9) may act: only in the sandbox and spectator games,
    /// never in a skirmish or a campaign mission (K2).
    /// </summary>
    /// <param name="skirmishOrMission">True for a skirmish, a mission or a loaded save of either.</param>
    public static bool DebugKeysAllowed(bool skirmishOrMission) => !skirmishOrMission;

    private static List<ControlRow> BuildRows()
    {
        ControlRow A(GameAction a) => new(InfoOf(a).Group, InfoOf(a).Description, a);
        ControlRow F(string group, string label, string description) => new(group, description, null, label);
        return new List<ControlRow>
        {
            A(GameAction.ScrollLeft), A(GameAction.ScrollRight), A(GameAction.ScrollUp), A(GameAction.ScrollDown),
            F(Camera, "Mouse at screen edge", "Scroll the view (Controls page: Edge scroll)"),
            F(Camera, "Shift (held)", "Scroll twice as fast"),
            F(Camera, "Middle-drag", "Drag the view"),
            F(Camera, "Mouse wheel", "Zoom"),
            F(Camera, "Minimap click or drag", "Move the view"),
            A(GameAction.CentreBase), A(GameAction.Bookmark1), A(GameAction.Bookmark2), A(GameAction.Bookmark3), A(GameAction.Bookmark4),
            F(Selection, "Left click", "Select a unit or structure"),
            F(Selection, "Left drag", "Select units in a box"),
            F(Orders, "Right click", "Move, attack, enter, salvage; set a factory's rally point"),
            F(Orders, "Left click (attack-move armed)", "Attack-move to the cell"),
            A(GameAction.AttackMove), A(GameAction.Stop), A(GameAction.Guard), A(GameAction.Scatter), A(GameAction.Deploy), A(GameAction.Pack),
            A(GameAction.TabStructures), A(GameAction.TabDefences), A(GameAction.TabInfantry), A(GameAction.TabVehicles),
            A(GameAction.TabUpgrades), A(GameAction.TabAircraft),
            F(System, "Esc", "Leave a mode, then open the pause menu"),
            A(GameAction.Pause),
            F(System, "F1 or ?", "This list (F1 opens the console in the sandbox)"),
            A(GameAction.Talk),
            A(GameAction.DebugFog), A(GameAction.DebugOccupancy), A(GameAction.DebugPaths), A(GameAction.DebugRanges),
            A(GameAction.DebugStep), A(GameAction.DebugSpectator),
            A(GameAction.SpecView), A(GameAction.SpecSpeed1), A(GameAction.SpecSpeed2), A(GameAction.SpecSpeed4), A(GameAction.SpecSpeed8),
            A(GameAction.SpecDetail),
            A(GameAction.ObjJump), A(GameAction.ObjCycle), A(GameAction.ObjList),
        };
    }
}
