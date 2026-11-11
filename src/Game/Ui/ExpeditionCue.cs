// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui;

/// <summary>
/// The Expedition start cue (D-205, S18 UXG-09): at the start of a match the Expedition player has no building, only a Command Ship that
/// must land to make the base. The cue centres the view on the ship and shows a one-line hint, as the Left Behind sidebar hint does.
/// </summary>
public static class ExpeditionCue
{
    /// <summary>The rules id of the Expedition Command Ship.</summary>
    public const string CommandShipId = "exp_command_ship";

    /// <summary>How long the hint stays up, in milliseconds.</summary>
    public const int ToastMs = 12000;

    /// <summary>True when the hint applies: the player is an undefeated Expedition player with no building yet.</summary>
    public static bool Applies(PlayerHud hud, SimSnapshot snapshot, byte player)
    {
        if (hud.Faction != Faction.Expedition || hud.Defeated) return false;
        foreach (BuildingView b in snapshot.Buildings)
            if (!b.Ghost && b.Player == player) return false;
        return true;
    }

    /// <summary>The player's Command Ship in the snapshot, or null.</summary>
    public static UnitView? FindShip(SimSnapshot snapshot, SimRules rules, byte player)
    {
        foreach (UnitView u in snapshot.Units)
        {
            if (u.Player != player || u.Kind != UnitKind.Vehicle || (uint)u.TypeIndex >= (uint)rules.Vehicles.Count) continue;
            if (rules.Vehicles[u.TypeIndex].Id == CommandShipId) return u;
        }

        return null;
    }

    /// <summary>The hint line, naming the current Deploy key.</summary>
    public static string Text() => UiText.Format(UiText.ExpeditionStart, "key", KeyNames.Of(GameKeys.Active.Get(GameAction.Deploy)));
}
