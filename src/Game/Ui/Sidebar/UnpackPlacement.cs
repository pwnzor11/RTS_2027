// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// Placement mode of an unpacking rig (M6-09): the footprint of the building the packed rig turns into follows the cursor, green where the
/// sim would accept the Pack command, red elsewhere; a left click on a valid site issues it. Pure: the validity test is passed in.
/// </summary>
public sealed class UnpackPlacement
{
    private readonly Func<CellCoord, bool> _valid;

    /// <summary>Starts placement for packed rig <paramref name="rig"/> that unpacks into building type <paramref name="type"/>.</summary>
    /// <param name="rig">The packed rig vehicle.</param>
    /// <param name="type">Rules index of the building it unpacks into.</param>
    /// <param name="footprintW">Footprint width in cells.</param>
    /// <param name="footprintH">Footprint height in cells.</param>
    /// <param name="valid">Whether the sim accepts the footprint with this top-left (<c>PlayerView.CanUnpackAt</c>).</param>
    public UnpackPlacement(EntityId rig, int type, int footprintW, int footprintH, Func<CellCoord, bool> valid)
    {
        Rig = rig;
        Type = type;
        FootprintW = footprintW;
        FootprintH = footprintH;
        _valid = valid;
    }

    /// <summary>The packed rig being placed.</summary>
    public EntityId Rig { get; }

    /// <summary>Rules index of the building it unpacks into.</summary>
    public int Type { get; }

    /// <summary>Footprint width in cells.</summary>
    public int FootprintW { get; }

    /// <summary>Footprint height in cells.</summary>
    public int FootprintH { get; }

    /// <summary>Top-left cell of the ghost (valid once <see cref="HasGhost"/>).</summary>
    public CellCoord TopLeft { get; private set; }

    /// <summary>True when the ghost is on an acceptable site.</summary>
    public bool Valid { get; private set; }

    /// <summary>True once the cursor has been over the map.</summary>
    public bool HasGhost { get; private set; }

    /// <summary>Moves the ghost to the cursor cell (centred like a normal placement).</summary>
    public void Hover(CellCoord cursorCell)
    {
        TopLeft = SidebarCommands.TopLeftFor(cursorCell, FootprintW, FootprintH);
        Valid = _valid(TopLeft);
        HasGhost = true;
    }

    /// <summary>The command a left click issues now, or null when the ghost is not on a valid site.</summary>
    public Command? Click(byte player) => HasGhost && Valid ? PackOffer.UnpackCommand(player, Rig, TopLeft) : null;
}
