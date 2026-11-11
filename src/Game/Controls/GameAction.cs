// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Controls;

/// <summary>
/// Every keyboard action the player can rebind (D-172 (6)). Game input asks for an action, never for a literal key; the key comes from
/// <see cref="KeyBindings"/>. The mouse buttons and the Shift / Ctrl modifiers are fixed and are not actions.
/// </summary>
public enum GameAction
{
    /// <summary>Scroll the view left.</summary>
    ScrollLeft,
    /// <summary>Scroll the view right.</summary>
    ScrollRight,
    /// <summary>Scroll the view up.</summary>
    ScrollUp,
    /// <summary>Scroll the view down.</summary>
    ScrollDown,
    /// <summary>Centre the view on the player's base.</summary>
    CentreBase,
    /// <summary>Camera bookmark 1 (Ctrl + key stores, the key recalls).</summary>
    Bookmark1,
    /// <summary>Camera bookmark 2.</summary>
    Bookmark2,
    /// <summary>Camera bookmark 3.</summary>
    Bookmark3,
    /// <summary>Camera bookmark 4.</summary>
    Bookmark4,
    /// <summary>Arm attack-move.</summary>
    AttackMove,
    /// <summary>Stop.</summary>
    Stop,
    /// <summary>Guard here.</summary>
    Guard,
    /// <summary>Scatter.</summary>
    Scatter,
    /// <summary>Deploy or unload.</summary>
    Deploy,
    /// <summary>Pack or unpack a rig.</summary>
    Pack,
    /// <summary>Sidebar tab: structures.</summary>
    TabStructures,
    /// <summary>Sidebar tab: defences.</summary>
    TabDefences,
    /// <summary>Sidebar tab: infantry.</summary>
    TabInfantry,
    /// <summary>Sidebar tab: vehicles.</summary>
    TabVehicles,
    /// <summary>Sidebar tab: upgrades.</summary>
    TabUpgrades,
    /// <summary>Sidebar tab: aircraft.</summary>
    TabAircraft,
    /// <summary>Pause the game.</summary>
    Pause,
    /// <summary>Talk to an AI commander.</summary>
    Talk,
    /// <summary>Sandbox and spectator: fog viewer / full vision.</summary>
    DebugFog,
    /// <summary>Sandbox: occupancy overlay.</summary>
    DebugOccupancy,
    /// <summary>Sandbox: paths overlay.</summary>
    DebugPaths,
    /// <summary>Sandbox: weapon range overlay.</summary>
    DebugRanges,
    /// <summary>Sandbox: step the simulation once.</summary>
    DebugStep,
    /// <summary>Sandbox: start an AI-vs-AI spectator game.</summary>
    DebugSpectator,
    /// <summary>Spectator: next view.</summary>
    SpecView,
    /// <summary>Spectator: speed 1x.</summary>
    SpecSpeed1,
    /// <summary>Spectator: speed 2x.</summary>
    SpecSpeed2,
    /// <summary>Spectator: speed 4x.</summary>
    SpecSpeed4,
    /// <summary>Spectator: speed 8x.</summary>
    SpecSpeed8,
    /// <summary>Spectator: detail panel.</summary>
    SpecDetail,
    /// <summary>Mission: jump to the guided objective.</summary>
    ObjJump,
    /// <summary>Mission: cycle to the next objective.</summary>
    ObjCycle,
    /// <summary>Mission: expand or collapse the objective list.</summary>
    ObjList,
    /// <summary>Select every own combat unit (not harvesters or builders).</summary>
    SelectAll,
    /// <summary>Control group 1: the key recalls it (twice quickly centres the view on it), Ctrl + key sets it, Shift + key adds the selection.</summary>
    Group1,
    /// <summary>Control group 2: the key recalls it (twice quickly centres the view on it), Ctrl + key sets it, Shift + key adds the selection.</summary>
    Group2,
    /// <summary>Control group 3: the key recalls it (twice quickly centres the view on it), Ctrl + key sets it, Shift + key adds the selection.</summary>
    Group3,
    /// <summary>Control group 4: the key recalls it (twice quickly centres the view on it), Ctrl + key sets it, Shift + key adds the selection.</summary>
    Group4,
    /// <summary>Control group 5: the key recalls it (twice quickly centres the view on it), Ctrl + key sets it, Shift + key adds the selection.</summary>
    Group5,
    /// <summary>Control group 6: the key recalls it (twice quickly centres the view on it), Ctrl + key sets it, Shift + key adds the selection.</summary>
    Group6,
    /// <summary>Control group 7: the key recalls it (twice quickly centres the view on it), Ctrl + key sets it, Shift + key adds the selection.</summary>
    Group7,
    /// <summary>Control group 8: the key recalls it (twice quickly centres the view on it), Ctrl + key sets it, Shift + key adds the selection.</summary>
    Group8,
    /// <summary>Control group 9: the key recalls it (twice quickly centres the view on it), Ctrl + key sets it, Shift + key adds the selection.</summary>
    Group9,
    /// <summary>Camera: jump to the most recent alert with a place; press again to step back through the last few.</summary>
    LastAlert,
    /// <summary>Show or hide the map decor (decals and land-linked sprites) to compare with the bare ground (M12-03).</summary>
    DecorToggle,
    /// <summary>Save the game into the quicksave slot (D-205).</summary>
    QuickSave,
    /// <summary>Load the quicksave slot, after a confirmation (D-205).</summary>
    QuickLoad,
    /// <summary>M13-15: faster single-player game speed (0.5x, 0.75x, 1x, 1.5x, 2x).</summary>
    GameSpeedUp,
    /// <summary>M13-15: slower single-player game speed.</summary>
    GameSpeedDown,
    /// <summary>M13-15: jump to the next idle harvester (and select it); again for the next one.</summary>
    IdleHarvester,
    /// <summary>M13-15: jump to the next idle factory; again for the next one.</summary>
    IdleFactory,
    /// <summary>M13-15: sidebar cameo hotkey 1 (row 1, column 1 of the active tab).</summary>
    CameoSlot1,
    /// <summary>M13-15: sidebar cameo hotkey 2 (row 1, column 2 of the active tab).</summary>
    CameoSlot2,
    /// <summary>M13-15: sidebar cameo hotkey 3 (row 1, column 3 of the active tab).</summary>
    CameoSlot3,
    /// <summary>M13-15: sidebar cameo hotkey 4 (row 2, column 1 of the active tab).</summary>
    CameoSlot4,
    /// <summary>M13-15: sidebar cameo hotkey 5 (row 2, column 2 of the active tab).</summary>
    CameoSlot5,
    /// <summary>M13-15: sidebar cameo hotkey 6 (row 2, column 3 of the active tab).</summary>
    CameoSlot6,
    /// <summary>M13-15: sidebar cameo hotkey 7 (row 3, column 1 of the active tab).</summary>
    CameoSlot7,
    /// <summary>M13-15: sidebar cameo hotkey 8 (row 3, column 2 of the active tab).</summary>
    CameoSlot8,
    /// <summary>M13-15: sidebar cameo hotkey 9 (row 3, column 3 of the active tab).</summary>
    CameoSlot9,
    /// <summary>M13-15: sidebar cameo hotkey 10 (row 4, column 1 of the active tab).</summary>
    CameoSlot10,
    /// <summary>M13-15: sidebar cameo hotkey 11 (row 4, column 2 of the active tab).</summary>
    CameoSlot11,
    /// <summary>M13-15: sidebar cameo hotkey 12 (row 4, column 3 of the active tab).</summary>
    CameoSlot12,
    /// <summary>M13-15: show or hide the weapon range rings of the selected units and defences in play (off by default).</summary>
    RangeRings,
}
