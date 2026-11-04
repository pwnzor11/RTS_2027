// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;

namespace Rts.Game.Render;

/// <summary>Where a resolved sprite's pixels come from.</summary>
public enum SpriteSource
{
    /// <summary>A sprite file in the library, named by the rules id.</summary>
    Library,

    /// <summary>A sheet the caller handed in (the single tank sheet of the debug view).</summary>
    Legacy,

    /// <summary>Generated placeholder art.</summary>
    Placeholder,
}

/// <summary>
/// The sprite chosen for one rules id: its manifest (always present, so frame maths works) and where the pixels are.
/// Instances come from <see cref="SpriteResolver"/> and are cached, so the same object is returned for the same key.
/// </summary>
public sealed class ResolvedSprite
{
    internal ResolvedSprite(string id, SpriteKind kind, SpriteSource source, SpriteManifest manifest,
        SpriteLibrary? library, SpriteSheet? legacy, PlaceholderPixels? placeholder,
        ResolvedSprite? turretSheet = null, ResolvedSprite? shadowSheet = null)
    {
        Id = id;
        Kind = kind;
        Source = source;
        Manifest = manifest;
        Library = library;
        Legacy = legacy;
        Placeholder = placeholder;
        TurretSheet = turretSheet;
        ShadowSheet = shadowSheet;
    }

    /// <summary>The rules id this sprite was resolved for (or the placeholder's stand-in id).</summary>
    public string Id { get; }

    /// <summary>What the sprite is drawn for.</summary>
    public SpriteKind Kind { get; }

    /// <summary>Where the pixels come from.</summary>
    public SpriteSource Source { get; }

    /// <summary>Frame layout and states.</summary>
    public SpriteManifest Manifest { get; }

    /// <summary>True when the sprite is generated placeholder art.</summary>
    public bool IsPlaceholder => Source == SpriteSource.Placeholder;

    /// <summary>
    /// The companion turret sheet (<c>&lt;id&gt;_turret</c>) drawn over this body, or null when the sprite has none. Its
    /// <see cref="Manifest"/> has the <c>turret</c> layer and its pivot.
    /// </summary>
    public ResolvedSprite? TurretSheet { get; }

    /// <summary>
    /// The companion shadow sheet (<c>&lt;id&gt;_shadow</c>) drawn under this body, or null when the sprite has none.
    /// </summary>
    public ResolvedSprite? ShadowSheet { get; }

    internal SpriteLibrary? Library { get; }

    internal SpriteSheet? Legacy { get; }

    public PlaceholderPixels? Placeholder { get; }
}
