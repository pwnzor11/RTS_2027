// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Picks the sprite for a rules id. A sprite file in the library is used when one has the same id (the file stem).
/// Vehicles also accept the alias table below, so an older rules id can keep using a renamed file. Without a file, a
/// vehicle uses the legacy sheet when one was given (the debug view draws every vehicle with the tank), and anything
/// else gets a generated placeholder. Results are cached, so the renderer can ask every frame.
/// </summary>
public sealed class SpriteResolver
{
    /// <summary>Vehicle rules ids that map to a different sprite id. Rules id on the left, sprite id on the right.</summary>
    public static readonly IReadOnlyDictionary<string, string> VehicleAliases =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["auth_tank"] = "tank_v2" };

    private readonly SpriteLibrary? _library;
    private readonly SpriteSheet? _legacyVehicle;
    private readonly Dictionary<(SpriteKind Kind, string? Id, int FootW, int FootH), ResolvedSprite> _cache = new();

    /// <summary>
    /// Creates a resolver over a sprite library (may be null, which means no files: placeholders only).
    /// </summary>
    /// <param name="library">Scanned sprite library, or null.</param>
    /// <param name="legacyVehicle">Sheet every vehicle without a file of its own is drawn with, or null.</param>
    public SpriteResolver(SpriteLibrary? library, SpriteSheet? legacyVehicle = null)
    {
        _library = library;
        _legacyVehicle = legacyVehicle;
    }

    /// <summary>
    /// The sprite for a rules id. <paramref name="id"/> may be null when the rules are not available (the demo source);
    /// that gives the generic placeholder, or the legacy vehicle sheet for a vehicle.
    /// </summary>
    /// <param name="kind">What it is drawn for.</param>
    /// <param name="id">Rules id, for example "auth_refinery", or null.</param>
    /// <param name="footprintW">Footprint width in cells, buildings only.</param>
    /// <param name="footprintH">Footprint height in cells, buildings only.</param>
    public ResolvedSprite Resolve(SpriteKind kind, string? id, int footprintW = 1, int footprintH = 1)
    {
        int fw = kind == SpriteKind.Building ? Math.Max(1, footprintW) : 0;
        int fh = kind == SpriteKind.Building ? Math.Max(1, footprintH) : 0;
        var key = (kind, id, fw, fh);
        if (_cache.TryGetValue(key, out ResolvedSprite? cached)) return cached;

        ResolvedSprite resolved = ResolveUncached(kind, id, fw, fh);
        _cache.Add(key, resolved);
        return resolved;
    }

    /// <summary>The sprite for a building in a snapshot, named by its rules type.</summary>
    /// <param name="rules">Rules, or null.</param>
    /// <param name="building">The building as the renderer sees it.</param>
    public ResolvedSprite ForBuilding(SimRules? rules, BuildingView building)
    {
        string? id = null;
        if (rules is not null && (uint)building.TypeIndex < (uint)rules.Buildings.Count)
        {
            id = rules.Buildings[building.TypeIndex].Id;
        }
        return Resolve(SpriteKind.Building, id, building.FootprintW, building.FootprintH);
    }

    /// <summary>The sprite for a unit in a snapshot, named by its rules type.</summary>
    /// <param name="rules">Rules, or null.</param>
    /// <param name="unit">The unit as the renderer sees it.</param>
    public ResolvedSprite ForUnit(SimRules? rules, UnitView unit)
    {
        string? id = null;
        if (rules is not null)
        {
            if (unit.Kind == UnitKind.Vehicle)
            {
                if ((uint)unit.TypeIndex < (uint)rules.Vehicles.Count) id = rules.Vehicles[unit.TypeIndex].Id;
            }
            else if ((uint)unit.TypeIndex < (uint)rules.Infantry.Count)
            {
                id = rules.Infantry[unit.TypeIndex].Id;
            }
        }
        return Resolve(unit.Kind == UnitKind.Vehicle ? SpriteKind.Vehicle : SpriteKind.Infantry, id);
    }

    /// <summary>
    /// M3-21: whether a resolved building sprite has art for the open-gate state (<see cref="SpriteStates.Open"/>). Placeholders and library
    /// files without that state answer false, and the building renderer then draws its stand-in overlay. This is the one slot the open art
    /// drops into: add an <c>open</c> state to the gate's sprite manifest.
    /// </summary>
    /// <param name="sprite">The sprite from <see cref="ForBuilding"/>.</param>
    public static bool HasOpenArt(ResolvedSprite sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        return SpriteStates.HasState(sprite.Manifest, SpriteStates.Open);
    }

    /// <summary>Whether the library holds a sprite file with this id.</summary>
    public bool LibraryHas(string id) => _library is not null && _library.Ids.Contains(id, StringComparer.Ordinal);

    private ResolvedSprite ResolveUncached(SpriteKind kind, string? id, int fw, int fh)
    {
        if (kind == SpriteKind.Vehicle)
        {
            string? spriteId = id is not null && VehicleAliases.TryGetValue(id, out string? alias) ? alias : id;
            if (spriteId is not null && LibraryHas(spriteId))
            {
                return FromLibrary(spriteId, kind);
            }
            if (_legacyVehicle is not null)
            {
                return new ResolvedSprite(id ?? _legacyVehicle.Id, kind, SpriteSource.Legacy,
                    _legacyVehicle.Manifest, null, _legacyVehicle, null);
            }
            return Placeholder(kind, id, fw, fh);
        }

        if (id is not null && LibraryHas(id))
        {
            return FromLibrary(id, kind);
        }
        return Placeholder(kind, id, fw, fh);
    }

    private ResolvedSprite FromLibrary(string id, SpriteKind kind)
    {
        SpriteLibrary library = _library!;
        return new ResolvedSprite(id, kind, SpriteSource.Library, library.Manifest(id), library, null, null);
    }

    private static ResolvedSprite Placeholder(SpriteKind kind, string? id, int fw, int fh)
    {
        string name = id ?? kind.ToString().ToLowerInvariant();
        PlaceholderPixels pixels = kind switch
        {
            SpriteKind.Building => PlaceholderArt.Building(name, fw, fh),
            SpriteKind.Vehicle => PlaceholderArt.Unit(name, infantry: false),
            _ => PlaceholderArt.Unit(name, infantry: true),
        };
        return new ResolvedSprite(name, kind, SpriteSource.Placeholder, pixels.Manifest, null, null, pixels);
    }
}
