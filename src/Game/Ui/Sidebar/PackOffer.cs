// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>What the Pack button does to the selected rig.</summary>
public enum PackAction
{
    /// <summary>The selection is an own rig building: Pack starts it packing into its vehicle.</summary>
    Pack,
    /// <summary>The selection is an own packed rig: Pack enters placement mode with the footprint of the building it unpacks into.</summary>
    Unpack,
}

/// <summary>
/// The Pack button (M6-09) for a selected own rig building (pack) or own packed rig vehicle (unpack). Pure: the sim stays the judge,
/// this only mirrors "is it already busy". <see cref="Reason"/> is empty when enabled, otherwise the tooltip key's text.
/// </summary>
/// <param name="Action">Pack or unpack.</param>
/// <param name="Caption">Button label (placeholder text of the caption key).</param>
/// <param name="Enabled">Whether a click / the hotkey does something now.</param>
/// <param name="Reason">Why it is disabled, or empty.</param>
/// <param name="UnpackType">For Unpack: rules index of the building the rig unpacks into; else -1.</param>
public readonly record struct PackOffer(PackAction Action, string Caption, bool Enabled, string Reason, int UnpackType)
{
    /// <summary>The offer for an own finished building of type <paramref name="typeIndex"/>, or null when the type does not pack.</summary>
    /// <param name="rules">Rules.</param>
    /// <param name="typeIndex">Rules index of the building's type.</param>
    /// <param name="progressPermille">Its pack progress (<c>PlayerView.PackProgressPermille</c>; above 0 = already packing).</param>
    public static PackOffer? ForBuilding(SimRules rules, int typeIndex, int progressPermille)
    {
        if ((uint)typeIndex >= (uint)rules.Buildings.Count) return null;
        int into = rules.Buildings[typeIndex].PacksInto;
        if (into < 0 || into >= rules.Vehicles.Count) return null;
        bool busy = progressPermille > 0;
        return new PackOffer(PackAction.Pack, LeftBehindText.Placeholder(LeftBehindText.PackKey), !busy,
            busy ? LeftBehindText.Placeholder(LeftBehindText.BusyKey) : "", -1);
    }

    /// <summary>The offer for an own packed rig whose unpack target is building type <paramref name="unpackType"/>, or null when it has none.</summary>
    /// <param name="rules">Rules.</param>
    /// <param name="unpackType"><c>PlayerView.UnpackTypeOf</c> (-1 = not a packed rig).</param>
    /// <param name="progressPermille">Its unpack progress (above 0 = already unpacking).</param>
    public static PackOffer? ForPackedRig(SimRules rules, int unpackType, int progressPermille)
    {
        if ((uint)unpackType >= (uint)rules.Buildings.Count) return null;
        bool busy = progressPermille > 0;
        return new PackOffer(PackAction.Unpack, LeftBehindText.Placeholder(LeftBehindText.UnpackKey), !busy,
            busy ? LeftBehindText.Placeholder(LeftBehindText.BusyKey) : "", unpackType);
    }

    /// <summary>The Pack command that starts a rig building packing (Subject = the building; ExecTick and Sequence are stamped by the host).</summary>
    public static Command PackCommand(byte player, EntityId building) =>
        new(0, player, 0, CommandKind.Pack, building, EntityId.None, default, (int)ActorKind.Building);

    /// <summary>The Pack command that sends a packed rig to unpack with the footprint's top-left at <paramref name="topLeft"/>.</summary>
    public static Command UnpackCommand(byte player, EntityId rig, CellCoord topLeft) =>
        new(0, player, 0, CommandKind.Pack, rig, EntityId.None, topLeft, (int)ActorKind.Vehicle);

    /// <summary>Width in pixels of the filled part of a progress bar <paramref name="width"/> wide (permille is clamped to 0..1000).</summary>
    public static int BarFill(int progressPermille, int width) => width * Math.Clamp(progressPermille, 0, 1000) / 1000;
}
