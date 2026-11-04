// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Archive;

/// <summary>
/// One Archive entry as authored in data/archive/*.toml. For Unit, Structure and Upgrade entries <see cref="Id"/> is
/// the rules id, and the stats block is pulled live from the rules (so it never goes stale).
/// </summary>
/// <param name="Id">Unique id; the rules id for Unit/Structure/Upgrade entries.</param>
/// <param name="Category">Tab.</param>
/// <param name="Faction">Side (colour theme).</param>
/// <param name="Title">Name shown in the list and reading pane.</param>
/// <param name="Subtitle">Short line under the title (may be empty).</param>
/// <param name="FieldNote">One in-world line shown under the title (a teletype for the Authority, a broadcast for the Directorate); may be empty.</param>
/// <param name="Body">Body text with simple markup (see <see cref="ArchiveMarkup"/>).</param>
/// <param name="Sprite">Rules id of the sprite to preview, or empty for none. Unit/Structure entries preview their own id by default.</param>
/// <param name="Unlock">When the entry becomes readable.</param>
/// <param name="Source">The TOML file the entry came from (diagnostics).</param>
public sealed record ArchiveEntry(string Id, ArchiveCategory Category, ArchiveFaction Faction, string Title,
    string Subtitle, string FieldNote, string Body, string Sprite, ArchiveUnlock Unlock, string Source)
{
    /// <summary>True for entries that show a live stats block.</summary>
    public bool HasStats => Category is ArchiveCategory.Unit or ArchiveCategory.Structure or ArchiveCategory.Upgrade;

    /// <summary>The sprite id to preview: the explicit one, else the entry id for Unit and Structure entries, else empty.</summary>
    public string PreviewSprite => Sprite.Length > 0 ? Sprite
        : Category is ArchiveCategory.Unit or ArchiveCategory.Structure ? Id : "";
}
