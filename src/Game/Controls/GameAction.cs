// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Controls;

/// <summary>
/// Every keyboard action the player can rebind (D-172 (6)). Game input asks for an action, never for a literal key; the key comes from
/// <see cref="KeyBindings"/>. The mouse buttons and the Shift / Ctrl modifiers are fixed and are not actions.
/// </summary>
public enum GameAction
{
    /// <summary>Scroll the view left.</summary> ScrollLeft,
    /// <summary>Scroll the view right.</summary> ScrollRight,
    /// <summary>Scroll the view up.</summary> ScrollUp,
    /// <summary>Scroll the view down.</summary> ScrollDown,
    /// <summary>Centre the view on the player's base.</summary> CentreBase,
    /// <summary>Camera bookmark 1 (Ctrl + key stores, the key recalls).</summary> Bookmark1,
    /// <summary>Camera bookmark 2.</summary> Bookmark2,
    /// <summary>Camera bookmark 3.</summary> Bookmark3,
    /// <summary>Camera bookmark 4.</summary> Bookmark4,
    /// <summary>Arm attack-move.</summary> AttackMove,
    /// <summary>Stop.</summary> Stop,
    /// <summary>Guard here.</summary> Guard,
    /// <summary>Scatter.</summary> Scatter,
    /// <summary>Deploy or unload.</summary> Deploy,
    /// <summary>Pack or unpack a rig.</summary> Pack,
    /// <summary>Sidebar tab: structures.</summary> TabStructures,
    /// <summary>Sidebar tab: defences.</summary> TabDefences,
    /// <summary>Sidebar tab: infantry.</summary> TabInfantry,
    /// <summary>Sidebar tab: vehicles.</summary> TabVehicles,
    /// <summary>Sidebar tab: upgrades.</summary> TabUpgrades,
    /// <summary>Sidebar tab: aircraft.</summary> TabAircraft,
    /// <summary>Pause the game.</summary> Pause,
    /// <summary>Talk to an AI commander.</summary> Talk,
    /// <summary>Sandbox and spectator: fog viewer / full vision.</summary> DebugFog,
    /// <summary>Sandbox: occupancy overlay.</summary> DebugOccupancy,
    /// <summary>Sandbox: paths overlay.</summary> DebugPaths,
    /// <summary>Sandbox: weapon range overlay.</summary> DebugRanges,
    /// <summary>Sandbox: step the simulation once.</summary> DebugStep,
    /// <summary>Sandbox: start an AI-vs-AI spectator game.</summary> DebugSpectator,
    /// <summary>Spectator: next view.</summary> SpecView,
    /// <summary>Spectator: speed 1x.</summary> SpecSpeed1,
    /// <summary>Spectator: speed 2x.</summary> SpecSpeed2,
    /// <summary>Spectator: speed 4x.</summary> SpecSpeed4,
    /// <summary>Spectator: speed 8x.</summary> SpecSpeed8,
    /// <summary>Spectator: detail panel.</summary> SpecDetail,
}
