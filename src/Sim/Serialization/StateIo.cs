// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

/// <summary>Small binary helpers shared by the M3 state parts (ids, refs, cells, checked slot indices and list counts).</summary>
internal static class StateIo
{
    /// <summary>Largest list length a part will read back (guards against corrupt saves).</summary>
    public const int MaxListCount = 1 << 20;

    public static void WriteId(BinaryWriter w, EntityId id) { w.Write(id.Index); w.Write(id.Generation); }
    public static EntityId ReadId(BinaryReader r) => new(r.ReadUInt16(), r.ReadUInt16());
    public static void WriteRef(BinaryWriter w, ActorRef a) { w.Write((byte)a.Kind); WriteId(w, a.Id); }
    public static ActorRef ReadRef(BinaryReader r) => new((ActorKind)r.ReadByte(), ReadId(r));
    public static void WriteCell(BinaryWriter w, CellCoord c) { w.Write(c.X); w.Write(c.Y); }
    public static CellCoord ReadCell(BinaryReader r) => new(r.ReadInt32(), r.ReadInt32());

    /// <summary>Checks a sparse slot index read from a save.</summary>
    public static int Slot(int i, int length, string part) =>
        i > 0 && i < length ? i : throw new InvalidDataException($"{part} slot out of range.");

    /// <summary>Reads and checks a list count.</summary>
    public static int Count(BinaryReader r, string part)
    {
        int n = r.ReadInt32();
        return n >= 0 && n <= MaxListCount ? n : throw new InvalidDataException($"{part} list count out of range.");
    }
}
