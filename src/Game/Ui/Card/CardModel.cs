// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Card;

/// <summary>
/// Everything the selection card shows, as plain data (no graphics), built by <see cref="CardModelBuilder"/> and drawn by
/// <see cref="SelectionCard"/>. Which fields are filled depends on <see cref="Kind"/>: a unit has stats and a rank line, a group has
/// cameos, a building has power, production and rally lines.
/// </summary>
public sealed class CardModel
{
    /// <summary>The no-card model.</summary>
    public static readonly CardModel Empty = new();

    /// <summary>What the card shows.</summary>
    public CardKind Kind { get; init; }

    /// <summary>Unit or building name; for a group the header text ("5 selected  -  4 Rifleman, 1 Scout").</summary>
    public string Title { get; init; } = "";

    /// <summary>Second line: type and faction ("Infantry - Authority"); empty for a group.</summary>
    public string SubLine { get; init; } = "";

    /// <summary>Rules id for the portrait (unit or building).</summary>
    public string PortraitId { get; init; } = "";

    /// <summary>Current health (single unit or building).</summary>
    public int Health { get; init; }

    /// <summary>Maximum health.</summary>
    public int MaxHealth { get; init; }

    /// <summary>Rank and kills to the next rank; null when veterancy is off or the thing cannot rank.</summary>
    public string? RankLine { get; init; }

    /// <summary>Damage, range, speed, armour, sight (a unit).</summary>
    public IReadOnlyList<CardStat> Stats { get; init; } = Array.Empty<CardStat>();

    /// <summary>Further text lines under the stats or the building lines (carrier hangar, power, production, rally).</summary>
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

    /// <summary>The selected units, grouped by type (a group).</summary>
    public IReadOnlyList<CardCameo> Cameos { get; init; } = Array.Empty<CardCameo>();

    /// <summary>The counts per type (a group).</summary>
    public IReadOnlyList<CardGroup> Groups { get; init; } = Array.Empty<CardGroup>();

    /// <summary>The order buttons, in draw order.</summary>
    public IReadOnlyList<CardButton> Buttons { get; init; } = Array.Empty<CardButton>();

    /// <summary>True when the card is to be shown at all.</summary>
    public bool Visible => Kind != CardKind.None;
}
