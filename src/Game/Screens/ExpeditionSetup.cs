// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>
/// The Overseas Expedition rules of the skirmish setup (M7-14, D-127): a generated map gets an ocean edge for an Expedition slot, and a
/// hand-made map without a landing coast cannot take one. Pure, so it is unit-tested without graphics.
/// </summary>
public static class ExpeditionSetup
{
    /// <summary>The one-line note shown when the setup adds an ocean edge for an Expedition slot.</summary>
    public const string OceanNote = "The Expedition needs a coast: one ocean edge added.";

    /// <summary>True when an open slot holds the Overseas Expedition.</summary>
    public static bool HasExpedition(IEnumerable<SkirmishSlot> slots) =>
        slots.Any(s => s.Kind != SlotKind.Closed && s.Faction == Faction.Expedition);

    /// <summary>The ocean choice the generated map really gets: when an Expedition slot needs a coast and none was chosen, one edge (North)
    /// is added and <paramref name="note"/> says so; otherwise <paramref name="chosen"/> stands.</summary>
    /// <param name="chosen">The ocean choice in the setup row.</param>
    /// <param name="expeditionOnGeneratedMap">True when an open slot is the Expedition and the map is generated.</param>
    /// <param name="note">The note to show, or null.</param>
    public static OceanEdges ResolveOcean(OceanEdges chosen, bool expeditionOnGeneratedMap, out string? note)
    {
        // assumed — confirm: the plainest fix is one North edge; the owner may prefer another edge
        if (expeditionOnGeneratedMap && chosen == OceanEdges.None)
        {
            note = OceanNote;
            return OceanEdges.North;
        }
        note = null;
        return chosen;
    }

    /// <summary>The validation error for a hand-made map that cannot take an Expedition slot, or null. <paramref name="mapMeta"/> is the
    /// map file's metadata (null for a generated map, or a file that cannot be read: the load reports that). A map with no landing marker
    /// has no landing coast.</summary>
    public static string? LandingProblem(SkirmishSettings settings, MapMeta? mapMeta)
    {
        if (settings.Map.Generated is not null || settings.Map.File is null) return null;
        int at = settings.Slots.ToList().FindIndex(s => s.Kind != SlotKind.Closed && s.Faction == Faction.Expedition);
        if (at < 0) return null;
        if (mapMeta is null || mapMeta.Landing.Count > 0) return null;
        return $"Slot {at + 1}: the Overseas Expedition needs a map with a landing coast.";
    }

    /// <summary>The metadata of a hand-made map file, or null when it cannot be read.</summary>
    public static MapMeta? ReadMeta(string rulesDir, string file)
    {
        try
        {
            return MapFile.LoadWithMeta(MapPicker.ResolveFile(rulesDir, file)).Meta;
        }
        catch (MapFileException)
        {
            return null;
        }
    }
}
