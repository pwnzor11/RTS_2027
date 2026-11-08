// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Map;

/// <summary>
/// Mutable runtime cell store: land (with override), overlay, density and per-cell occupancy. Registered as
/// the "map" state part. All arrays are row-major, index = y * Width + x. Height and the starting map stay
/// in the immutable <see cref="Data"/>.
/// </summary>
public sealed class MapState : ISimStatePart
{
    /// <summary>Infantry sub-slots per cell: 0 = centre, 1..4 = corners NW, NE, SW, SE.</summary>
    public const int InfantrySlots = 5;

    private readonly LandType[] _land;
    private readonly OverlayKind[] _overlay;
    private readonly byte[] _density;
    private readonly EntityId[] _vehicle;
    private readonly EntityId[] _infantry; // [cell * InfantrySlots + slot]
    private readonly EntityId[] _building;

    public MapState(MapData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
        Width = data.Width;
        Height = data.Height;
        int n = data.Width * data.Height;
        _land = new LandType[n];
        _overlay = new OverlayKind[n];
        _density = new byte[n];
        _vehicle = new EntityId[n];
        _infantry = new EntityId[n * InfantrySlots];
        _building = new EntityId[n];
        for (int i = 0; i < n; i++) { _land[i] = data.Land[i]; _overlay[i] = data.Overlay[i]; _density[i] = data.Density[i]; }
    }

    public string Name => "map";
    public int Width { get; }
    public int Height { get; }

    /// <summary>The immutable starting map this state was copied from.</summary>
    public MapData Data { get; }

    /// <summary>
    /// Increments whenever a building, a blocking overlay (wall) or land changes. Pathfinding cache key.
    /// Unit movement never changes it.
    /// </summary>
    public int Version { get; private set; }

    private bool In(CellCoord c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;
    private int Idx(CellCoord c) => c.Y * Width + c.X;

    public LandType LandAt(CellCoord c) => _land[Idx(c)];

    /// <summary>Overrides the land type of a cell (e.g. rubble, a built wall footprint). Bumps <see cref="Version"/>.</summary>
    public void SetLand(CellCoord c, LandType land)
    {
        int i = Idx(c);
        if (_land[i] == land) return;
        _land[i] = land;
        Version++;
    }

    /// <summary>Invalidates cached paths when passability changes without a cell edit (e.g. a gate changes owner). Bumps <see cref="Version"/>.</summary>
    public void Touch() => Version++;

    public byte HeightAt(CellCoord c) => Data.HeightLevel[Idx(c)];
    public OverlayKind OverlayAt(CellCoord c) => _overlay[Idx(c)];
    public byte DensityAt(CellCoord c) => _density[Idx(c)];

    /// <summary>Sets overlay and density. Bumps <see cref="Version"/> only if a wall or blocking prop appears or disappears.</summary>
    public void SetOverlay(CellCoord c, OverlayKind kind, byte density)
    {
        int i = Idx(c);
        if (OverlayKinds.BlocksGround(_overlay[i]) != OverlayKinds.BlocksGround(kind)) Version++;
        _overlay[i] = kind;
        _density[i] = density;
    }

    public EntityId VehicleAt(CellCoord c) => _vehicle[Idx(c)];
    public void SetVehicle(CellCoord c, EntityId id) => _vehicle[Idx(c)] = id;

    public EntityId InfantryAt(CellCoord c, int subSlot)
    {
        CheckSlot(subSlot);
        return _infantry[Idx(c) * InfantrySlots + subSlot];
    }

    public void SetInfantry(CellCoord c, int subSlot, EntityId id)
    {
        CheckSlot(subSlot);
        _infantry[Idx(c) * InfantrySlots + subSlot] = id;
    }

    /// <summary>First empty infantry sub-slot, centre first then corners in order; -1 if full.</summary>
    public int FreeInfantrySlot(CellCoord c)
    {
        int b = Idx(c) * InfantrySlots;
        for (int s = 0; s < InfantrySlots; s++)
            if (_infantry[b + s].IsNone) return s;
        return -1;
    }

    public EntityId BuildingAt(CellCoord c) => _building[Idx(c)];

    /// <summary>Sets or clears (None) the building on a cell. Bumps <see cref="Version"/> when it changes.</summary>
    public void SetBuilding(CellCoord c, EntityId id)
    {
        int i = Idx(c);
        if (_building[i] == id) return;
        _building[i] = id;
        Version++;
    }

    /// <summary>Speed percent for the locomotion on this cell's land; 0 if outside the map or impassable.</summary>
    public int SpeedPercent(CellCoord c, Locomotion loco, TerrainTable table)
    {
        if (!In(c)) return 0;
        if (loco != Locomotion.Air && OverlayKinds.BlocksGround(_overlay[Idx(c)])) return 0;   // M12-04: walls and blocking props
        if (loco == Pathing.MoveClass.NavalDeep) return _land[Idx(c)] == LandType.Shallow ? 0 : table.SpeedPercent(_land[Idx(c)], Locomotion.Naval);   // M7-03: large ships
        return table.SpeedPercent(_land[Idx(c)], loco);
    }

    /// <summary>Terrain-only passability (walls count as terrain); ignores units and buildings.</summary>
    public bool IsPassableTerrain(CellCoord c, Locomotion loco, TerrainTable table) => SpeedPercent(c, loco, table) > 0;

    /// <summary>Terrain buildable, no overlay, and no building, vehicle or infantry on the cell.</summary>
    public bool IsBuildable(CellCoord c, TerrainTable table)
    {
        if (!In(c)) return false;
        int i = Idx(c);
        if (!table.Buildable(_land[i]) || _overlay[i] != OverlayKind.None) return false;
        if (!_building[i].IsNone || !_vehicle[i].IsNone) return false;
        int b = i * InfantrySlots;
        for (int s = 0; s < InfantrySlots; s++)
            if (!_infantry[b + s].IsNone) return false;
        return true;
    }

    private static void CheckSlot(int s)
    {
        if ((uint)s >= InfantrySlots) throw new ArgumentOutOfRangeException(nameof(s), "Infantry sub-slot must be 0..4.");
    }

    /// <summary>
    /// Layout: Width, Height, Version, then land[], overlay[], density[] (bytes), vehicle[], building[]
    /// (each EntityId as ushort index, ushort generation), infantry[] (cell-major, 5 slots per cell).
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Width); w.Write(Height); w.Write(Version);
        for (int i = 0; i < _land.Length; i++) w.Write((byte)_land[i]);
        for (int i = 0; i < _overlay.Length; i++) w.Write((byte)_overlay[i]);
        w.Write(_density);
        WriteIds(w, _vehicle);
        WriteIds(w, _building);
        WriteIds(w, _infantry);
    }

    public void Read(BinaryReader r)
    {
        if (r.ReadInt32() != Width || r.ReadInt32() != Height) throw new InvalidDataException("Map size mismatch.");
        Version = r.ReadInt32();
        for (int i = 0; i < _land.Length; i++) _land[i] = (LandType)r.ReadByte();
        for (int i = 0; i < _overlay.Length; i++) _overlay[i] = (OverlayKind)r.ReadByte();
        byte[] d = r.ReadBytes(_density.Length);
        if (d.Length != _density.Length) throw new EndOfStreamException();
        d.CopyTo(_density, 0);
        ReadIds(r, _vehicle);
        ReadIds(r, _building);
        ReadIds(r, _infantry);
    }

    private static void WriteIds(BinaryWriter w, EntityId[] ids)
    {
        for (int i = 0; i < ids.Length; i++) { w.Write(ids[i].Index); w.Write(ids[i].Generation); }
    }

    private static void ReadIds(BinaryReader r, EntityId[] ids)
    {
        for (int i = 0; i < ids.Length; i++) { ushort idx = r.ReadUInt16(); ushort gen = r.ReadUInt16(); ids[i] = new EntityId(idx, gen); }
    }
}
