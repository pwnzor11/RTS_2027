// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// Text keys of the Left Behind UI (M6-09) with their PLACEHOLDER English text. There is no string table yet (see
/// <c>SuperweaponText</c>, <c>LlmUiText</c>); when one lands, the keys below are what to register and <see cref="Placeholder"/> is the one
/// place to swap. The bitmap font is ASCII.
/// </summary>
public static class LeftBehindText
{
    /// <summary>Hint in the empty sidebar of a Left Behind player whose Mother Rig is still packed.</summary>
    public const string UnpackHintKey = "hud.sidebar.unpack_rig";

    /// <summary>Pack button caption (key).</summary>
    public const string PackKey = "hud.command.pack";

    /// <summary>Unpack button caption (key).</summary>
    public const string UnpackKey = "hud.command.unpack";

    /// <summary>Banner while the unpack footprint follows the cursor (key).</summary>
    public const string UnpackPlacementKey = "hud.unpack.placing";

    /// <summary>Button tooltip while the rig is already packing or unpacking (key).</summary>
    public const string BusyKey = "hud.command.pack_busy";

    /// <summary>The hotkey of the Pack / Unpack command (K is free: A S G X D orders, Q W E R T Y tabs, V in spectate).</summary>
    public const string HotkeyLetter = "K";

    /// <summary>Placeholder text for a key. Unknown keys return the key.</summary>
    public static string Placeholder(string key) => key switch
    {
        UnpackHintKey => "Unpack the Mother Rig to build",
        PackKey => "PACK RIG (" + HotkeyLetter + ")",
        UnpackKey => "UNPACK RIG (" + HotkeyLetter + ")",
        UnpackPlacementKey => "UNPACK: click a clear site (right click or Esc cancels)",
        BusyKey => "The rig is already moving",
        _ => key,
    };

    /// <summary>
    /// True when the sidebar should show <see cref="UnpackHintKey"/>: a Left Behind player with no finished building, so nothing can be
    /// built yet (the start kit is one packed Mother Rig). Pure.
    /// </summary>
    /// <param name="hud">The local player's HUD.</param>
    /// <param name="snapshot">The latest snapshot (own buildings are listed in it).</param>
    /// <param name="player">The local player's slot.</param>
    public static bool ShowUnpackHint(PlayerHud hud, SimSnapshot snapshot, byte player)
    {
        if (hud.Faction != Faction.LeftBehind || hud.Defeated) return false;
        foreach (BuildingView b in snapshot.Buildings)
            if (!b.Ghost && b.Player == player) return false;
        return true;
    }
}
