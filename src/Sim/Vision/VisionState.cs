// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Vision;

/// <summary>
/// Per-player vision: shroud (never explored), fog (explored, not visible now) and visible, plus last-seen
/// building ghosts for each viewer. Each actor reveals a disc of cells around its last revealed centre; the
/// disc is added and removed incrementally, so moving an actor costs one disc, not a full recompute.
/// A disc holds the cells with dx*dx + dy*dy &lt;= sight*sight + sight, clipped to the map. Integer math only.
/// Per-cell layers are row-major (y * width + x). Save layout is documented on <see cref="Write"/>.
/// </summary>
public sealed class VisionState : ISimStatePart
{
    /// <summary>Largest sight radius, in cells, an actor may have.</summary>
    public const int MaxSightCells = 64;

    /// <summary>Building slots per viewer that can hold a ghost.</summary>
    public const int GhostCapacity = 512;

    private const int KindCount = (int)ActorKind.Projectile + 1;
    private const long MaxCells = 1 << 24;

    private readonly int _width;
    private readonly int _height;
    private readonly int _cells;
    private readonly int _playerCount;
    private readonly int _capacity;

    // Per player, per cell: how many of the player's actors currently see it.
    private readonly ushort[][] _counts;
    // Per player: bitset over cells, set once a cell has ever been visible.
    private readonly ulong[][] _explored;

    // Per actor slot (kind * capacity + index): the current reveal record.
    private readonly bool[] _actorActive;
    private readonly byte[] _actorOwner;
    private readonly CellCoord[] _actorCell;
    private readonly int[] _actorSight;

    // Per viewer, per building slot: last-seen ghost and a presence flag.
    private readonly BuildingGhost[][] _ghosts;
    private readonly bool[][] _ghostPresent;

    /// <summary>Creates empty vision (all shroud) for a map of the given size and player count.</summary>
    /// <param name="width">Map width in cells, at least 1.</param>
    /// <param name="height">Map height in cells, at least 1.</param>
    /// <param name="playerCount">Number of players, 1 to 255.</param>
    /// <param name="actorCapacityPerKind">Slots per <see cref="ActorKind"/>, 1 to 65536.</param>
    public VisionState(int width, int height, int playerCount, int actorCapacityPerKind = 2048)
    {
        if (width < 1 || height < 1 || (long)width * height > MaxCells)
            throw new ArgumentOutOfRangeException(nameof(width), "Map size out of range.");
        if (playerCount < 1 || playerCount > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        if (actorCapacityPerKind < 1 || actorCapacityPerKind > ushort.MaxValue + 1)
            throw new ArgumentOutOfRangeException(nameof(actorCapacityPerKind));

        _width = width;
        _height = height;
        _cells = width * height;
        _playerCount = playerCount;
        _capacity = actorCapacityPerKind;

        int words = (_cells + 63) >> 6;
        _counts = new ushort[playerCount][];
        _explored = new ulong[playerCount][];
        _ghosts = new BuildingGhost[playerCount][];
        _ghostPresent = new bool[playerCount][];
        for (int p = 0; p < playerCount; p++)
        {
            _counts[p] = new ushort[_cells];
            _explored[p] = new ulong[words];
            _ghosts[p] = new BuildingGhost[GhostCapacity];
            _ghostPresent[p] = new bool[GhostCapacity];
        }

        int slots = KindCount * _capacity;
        _actorActive = new bool[slots];
        _actorOwner = new byte[slots];
        _actorCell = new CellCoord[slots];
        _actorSight = new int[slots];
    }

    /// <summary>Stable part name, persisted in saves.</summary>
    public string Name => "vision";

    /// <summary>Map width in cells.</summary>
    public int Width => _width;

    /// <summary>Map height in cells.</summary>
    public int Height => _height;

    /// <summary>Number of players this state holds vision for.</summary>
    public int PlayerCount => _playerCount;

    /// <summary>
    /// Makes an actor reveal the disc of <paramref name="sightCells"/> around <paramref name="cell"/> for
    /// <paramref name="player"/>. A repeat call with the same player, cell and sight does nothing. Otherwise the
    /// actor's previous disc is removed first. Revealed cells become explored.
    /// </summary>
    public void Reveal(byte player, ActorKind kind, int index, CellCoord cell, int sightCells)
    {
        CheckPlayer(player);
        CheckSight(sightCells);
        int s = Slot(kind, index);
        if (_actorActive[s] && _actorOwner[s] == player && _actorCell[s] == cell && _actorSight[s] == sightCells)
            return;
        if (_actorActive[s]) ApplyDisc(_actorOwner[s], _actorCell[s], _actorSight[s], -1);
        ApplyDisc(player, cell, sightCells, +1);
        _actorActive[s] = true;
        _actorOwner[s] = player;
        _actorCell[s] = cell;
        _actorSight[s] = sightCells;
    }

    /// <summary>Removes an actor's reveal (on death or removal). Explored cells stay explored. No-op if none.</summary>
    public void Conceal(ActorKind kind, int index)
    {
        int s = Slot(kind, index);
        if (!_actorActive[s]) return;
        ApplyDisc(_actorOwner[s], _actorCell[s], _actorSight[s], -1);
        _actorActive[s] = false;
        _actorOwner[s] = 0;
        _actorCell[s] = default;
        _actorSight[s] = 0;
    }

    /// <summary>0 = shroud (never seen), 1 = fog (seen before, not now), 2 = visible now. Off-map cells are shroud.</summary>
    public byte VisionAt(byte player, CellCoord cell)
    {
        CheckPlayer(player);
        if (!InBounds(cell)) return 0;
        int i = CellIndex(cell.X, cell.Y);
        if (_counts[player][i] > 0) return 2;
        return IsExplored(player, i) ? (byte)1 : (byte)0;
    }

    /// <summary>True when <see cref="VisionAt"/> reports 2 for the cell.</summary>
    public bool IsVisible(byte player, CellCoord cell) => VisionAt(player, cell) == 2;

    /// <summary>Writes the player's per-cell vision layer (row-major, 0/1/2) into <paramref name="dest"/>.</summary>
    public void FillVision(byte player, byte[] dest)
    {
        CheckPlayer(player);
        if (dest.Length < _cells) throw new ArgumentException("Destination is too small.", nameof(dest));
        ushort[] counts = _counts[player];
        for (int i = 0; i < _cells; i++)
        {
            if (counts[i] > 0) dest[i] = 2;
            else dest[i] = IsExplored(player, i) ? (byte)1 : (byte)0;
        }
    }

    // ---- team queries (D-054) ------------------------------------------------------------------
    // Allies share vision. The per-player counts and explored bitsets above stay exactly as they were (they are what is
    // saved and hashed); a team view is computed at query time as the OR over the team's members. Teams hold at most a
    // handful of players, so this costs a few array reads per query and needs no extra state or hashing. Neutral slots
    // pass a one-member list and so share with nobody.

    /// <summary>Like <see cref="VisionAt"/> for a team: 2 if any member sees the cell now, else 1 if any member explored it, else 0.</summary>
    public byte VisionAtAny(ReadOnlySpan<byte> players, CellCoord cell)
    {
        if (!InBounds(cell)) return 0;
        int i = CellIndex(cell.X, cell.Y);
        byte best = 0;
        for (int k = 0; k < players.Length; k++)
        {
            byte p = players[k];
            CheckPlayer(p);
            if (_counts[p][i] > 0) return 2;
            if (IsExplored(p, i)) best = 1;
        }
        return best;
    }

    /// <summary>True if any of the players currently sees the cell.</summary>
    public bool IsVisibleToAny(ReadOnlySpan<byte> players, CellCoord cell)
    {
        if (!InBounds(cell)) return false;
        int i = CellIndex(cell.X, cell.Y);
        for (int k = 0; k < players.Length; k++)
        {
            byte p = players[k];
            CheckPlayer(p);
            if (_counts[p][i] > 0) return true;
        }
        return false;
    }

    /// <summary>Like <see cref="FillVision"/> for a team (0/1/2 as in <see cref="VisionAtAny"/>).</summary>
    public void FillVisionAny(ReadOnlySpan<byte> players, byte[] dest)
    {
        if (dest.Length < _cells) throw new ArgumentException("Destination is too small.", nameof(dest));
        for (int i = 0; i < _cells; i++)
        {
            byte best = 0;
            for (int k = 0; k < players.Length && best < 2; k++)
            {
                byte p = players[k];
                CheckPlayer(p);
                if (_counts[p][i] > 0) best = 2;
                else if (IsExplored(p, i)) best = 1;
            }
            dest[i] = best;
        }
    }

    /// <summary>Marks every cell explored for the player (debug or campaign reveal). Does not make anything visible.</summary>
    public void ExploreAll(byte player)
    {
        CheckPlayer(player);
        ulong[] explored = _explored[player];
        for (int i = 0; i < _cells; i++) explored[i >> 6] |= 1UL << (i & 63);
    }

    /// <summary>Stores or updates the ghost of a building the viewer last saw. Buildings are indexed 0 to 511.</summary>
    public void RecordBuildingSeen(byte viewer, int buildingIndex, BuildingGhost data)
    {
        CheckPlayer(viewer);
        CheckGhostIndex(buildingIndex);
        _ghosts[viewer][buildingIndex] = data;
        _ghostPresent[viewer][buildingIndex] = true;
    }

    /// <summary>Drops the viewer's ghost of a building (it was seen again, or destroyed). No-op if none.</summary>
    public void ForgetBuilding(byte viewer, int buildingIndex)
    {
        CheckPlayer(viewer);
        CheckGhostIndex(buildingIndex);
        _ghosts[viewer][buildingIndex] = default;
        _ghostPresent[viewer][buildingIndex] = false;
    }

    /// <summary>The viewer's ghosts as (building index, ghost) pairs in ascending index order.</summary>
    public IReadOnlyList<(int Index, BuildingGhost Ghost)> GhostsFor(byte viewer)
    {
        CheckPlayer(viewer);
        var list = new List<(int Index, BuildingGhost Ghost)>();
        for (int g = 0; g < GhostCapacity; g++)
        {
            if (_ghostPresent[viewer][g]) list.Add((g, _ghosts[viewer][g]));
        }
        return list;
    }

    /// <summary>
    /// Save layout, in order (integers as BinaryWriter writes them, little-endian):
    /// width, height, playerCount, actorCapacityPerKind (int32 each);
    /// then for each player 0..n-1: one uint16 count per cell, row-major;
    /// then for each player: the explored bitset as uint64 words;
    /// then for each actor slot (kind 0..3, index 0..capacity-1): bool active, byte owner, cell X and Y, sight;
    /// then for each player, for each building slot 0..511: bool present, and if present the ghost fields
    /// (byte Player, int TypeIndex, int Cell.X, int Cell.Y, int FootprintW, int FootprintH, int Health).
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(_width);
        w.Write(_height);
        w.Write(_playerCount);
        w.Write(_capacity);

        for (int p = 0; p < _playerCount; p++)
        {
            ushort[] counts = _counts[p];
            for (int i = 0; i < _cells; i++) w.Write(counts[i]);
        }

        for (int p = 0; p < _playerCount; p++)
        {
            foreach (ulong word in _explored[p]) w.Write(word);
        }

        for (int s = 0; s < _actorActive.Length; s++)
        {
            w.Write(_actorActive[s]);
            w.Write(_actorOwner[s]);
            w.Write(_actorCell[s].X);
            w.Write(_actorCell[s].Y);
            w.Write(_actorSight[s]);
        }

        for (int p = 0; p < _playerCount; p++)
        {
            for (int g = 0; g < GhostCapacity; g++)
            {
                bool present = _ghostPresent[p][g];
                w.Write(present);
                if (!present) continue;
                BuildingGhost gh = _ghosts[p][g];
                w.Write(gh.Player);
                w.Write(gh.TypeIndex);
                w.Write(gh.Cell.X);
                w.Write(gh.Cell.Y);
                w.Write(gh.FootprintW);
                w.Write(gh.FootprintH);
                w.Write(gh.Health);
            }
        }
    }

    /// <summary>Restores state written by <see cref="Write"/>. The header must match this instance's dimensions.</summary>
    public void Read(BinaryReader r)
    {
        int width = r.ReadInt32();
        int height = r.ReadInt32();
        int players = r.ReadInt32();
        int capacity = r.ReadInt32();
        if (width != _width || height != _height || players != _playerCount || capacity != _capacity)
            throw new InvalidDataException($"{Name} dimensions mismatch.");

        for (int p = 0; p < _playerCount; p++)
        {
            ushort[] counts = _counts[p];
            for (int i = 0; i < _cells; i++) counts[i] = r.ReadUInt16();
        }

        for (int p = 0; p < _playerCount; p++)
        {
            ulong[] explored = _explored[p];
            for (int k = 0; k < explored.Length; k++) explored[k] = r.ReadUInt64();
        }

        for (int s = 0; s < _actorActive.Length; s++)
        {
            _actorActive[s] = r.ReadBoolean();
            _actorOwner[s] = r.ReadByte();
            int x = r.ReadInt32();
            int y = r.ReadInt32();
            _actorCell[s] = new CellCoord(x, y);
            _actorSight[s] = r.ReadInt32();
        }

        for (int p = 0; p < _playerCount; p++)
        {
            for (int g = 0; g < GhostCapacity; g++)
            {
                bool present = r.ReadBoolean();
                _ghostPresent[p][g] = present;
                if (!present)
                {
                    _ghosts[p][g] = default;
                    continue;
                }
                byte player = r.ReadByte();
                int typeIndex = r.ReadInt32();
                int cx = r.ReadInt32();
                int cy = r.ReadInt32();
                int fw = r.ReadInt32();
                int fh = r.ReadInt32();
                int health = r.ReadInt32();
                _ghosts[p][g] = new BuildingGhost(player, typeIndex, new CellCoord(cx, cy), fw, fh, health);
            }
        }
    }

    // Adds (delta = +1) or removes (delta = -1) one disc for a player. Explored is only ever set on add.
    private void ApplyDisc(byte player, CellCoord centre, int sight, int delta)
    {
        int limit = sight * sight + sight;
        ushort[] counts = _counts[player];
        ulong[] explored = _explored[player];
        for (int dy = -sight; dy <= sight; dy++)
        {
            int y = centre.Y + dy;
            if ((uint)y >= (uint)_height) continue;
            int half = HalfWidth(limit, dy);
            int x0 = Math.Max(0, centre.X - half);
            int x1 = Math.Min(_width - 1, centre.X + half);
            int row = y * _width;
            for (int x = x0; x <= x1; x++)
            {
                int i = row + x;
                if (delta > 0)
                {
                    counts[i] = (ushort)(counts[i] + 1);
                    explored[i >> 6] |= 1UL << (i & 63);
                }
                else
                {
                    if (counts[i] == 0) throw new InvalidOperationException("Vision count underflow.");
                    counts[i] = (ushort)(counts[i] - 1);
                }
            }
        }
    }

    // Largest dx >= 0 with dx*dx + dy*dy <= limit. Callers pass |dy| <= sight, so dy*dy <= limit holds.
    private static int HalfWidth(int limit, int dy)
    {
        int rem = limit - dy * dy;
        int dx = 0;
        while ((dx + 1) * (dx + 1) <= rem) dx++;
        return dx;
    }

    private bool IsExplored(byte player, int i) => (_explored[player][i >> 6] & (1UL << (i & 63))) != 0;

    private bool InBounds(CellCoord c) => (uint)c.X < (uint)_width && (uint)c.Y < (uint)_height;

    private int CellIndex(int x, int y) => y * _width + x;

    private int Slot(ActorKind kind, int index)
    {
        if ((int)kind >= KindCount) throw new ArgumentOutOfRangeException(nameof(kind));
        if ((uint)index >= (uint)_capacity) throw new ArgumentOutOfRangeException(nameof(index));
        return (int)kind * _capacity + index;
    }

    private void CheckPlayer(int player)
    {
        if ((uint)player >= (uint)_playerCount) throw new ArgumentOutOfRangeException(nameof(player));
    }

    private static void CheckSight(int sight)
    {
        if ((uint)sight > MaxSightCells) throw new ArgumentOutOfRangeException(nameof(sight));
    }

    private static void CheckGhostIndex(int buildingIndex)
    {
        if ((uint)buildingIndex >= (uint)GhostCapacity) throw new ArgumentOutOfRangeException(nameof(buildingIndex));
    }
}
