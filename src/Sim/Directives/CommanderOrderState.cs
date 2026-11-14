// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Directives;

/// <summary>
/// The "commander_orders" state part (S24, M5-12, D-241): <see cref="CommanderOrderCodec.Slots"/> standing <see cref="CommanderOrder"/>s per
/// player slot. Appended after "naval" (before extension parts). Written by <see cref="CommandKind.CommanderOrder"/> only; the sim never acts
/// on it, the AI reads it through the player view. Arrays are flat, index = player * Slots + slot. Layout: version, then (only when any slot
/// differs from <see cref="CommanderOrder.Empty"/>) the slot count and every field of every slot, so games without orders write one byte.
/// </summary>
public sealed class CommanderOrderState : ISimStatePart
{
    private const byte VersionEmpty = 0;
    private const byte VersionInUse = 1;

    /// <summary>The <see cref="CommanderOrderKind"/> of each slot.</summary>
    public readonly byte[] Kind;
    /// <summary>Cell X of each slot (-1 = none).</summary>
    public readonly int[] CellX;
    /// <summary>Cell Y of each slot (-1 = none).</summary>
    public readonly int[] CellY;
    /// <summary>Amount of each slot.</summary>
    public readonly int[] Amount;
    /// <summary>DueTick of each slot.</summary>
    public readonly int[] DueTick;
    /// <summary>IssuedTick of each slot.</summary>
    public readonly int[] IssuedTick;

    /// <summary>Creates the part with every slot empty.</summary>
    public CommanderOrderState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int n = setup.PlayerCount * CommanderOrderCodec.Slots;
        Kind = new byte[n]; CellX = new int[n]; CellY = new int[n]; Amount = new int[n]; DueTick = new int[n]; IssuedTick = new int[n];
        for (int i = 0; i < n; i++) Reset(i);
    }

    /// <inheritdoc />
    public string Name => "commander_orders";

    /// <summary>The order in <paramref name="slot"/> of <paramref name="player"/> (<see cref="CommanderOrder.Empty"/> when out of range or unset).</summary>
    public CommanderOrder Of(byte player, int slot)
    {
        if (slot < 0 || slot >= CommanderOrderCodec.Slots) return CommanderOrder.Empty;
        int i = player * CommanderOrderCodec.Slots + slot;
        if (i >= Kind.Length) return CommanderOrder.Empty;
        return new CommanderOrder((CommanderOrderKind)Kind[i], new CellCoord(CellX[i], CellY[i]), Amount[i], DueTick[i], IssuedTick[i]);
    }

    /// <summary>All <see cref="CommanderOrderCodec.Slots"/> orders of <paramref name="player"/>, slot 0 first. Allocates.</summary>
    public CommanderOrder[] AllOf(byte player)
    {
        var all = new CommanderOrder[CommanderOrderCodec.Slots];
        for (int s = 0; s < all.Length; s++) all[s] = Of(player, s);
        return all;
    }

    /// <summary>Stores an already validated order (<see cref="CommanderOrder.Empty"/> clears the slot).</summary>
    internal void Store(byte player, int slot, in CommanderOrder order)
    {
        int i = player * CommanderOrderCodec.Slots + slot;
        Kind[i] = (byte)order.Kind; CellX[i] = order.Cell.X; CellY[i] = order.Cell.Y; Amount[i] = order.Amount;
        DueTick[i] = order.DueTick; IssuedTick[i] = order.IssuedTick;
    }

    private void Reset(int i)
    {
        Kind[i] = 0; CellX[i] = CommanderOrder.Empty.Cell.X; CellY[i] = CommanderOrder.Empty.Cell.Y; Amount[i] = 0; DueTick[i] = 0; IssuedTick[i] = 0;
    }

    private bool InUse()
    {
        for (int i = 0; i < Kind.Length; i++)
            if (Kind[i] != 0 || CellX[i] != -1 || CellY[i] != -1 || Amount[i] != 0 || DueTick[i] != 0 || IssuedTick[i] != 0) return true;
        return false;
    }

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        if (!InUse()) { w.Write(VersionEmpty); return; }
        w.Write(VersionInUse);
        w.Write(Kind.Length);
        for (int i = 0; i < Kind.Length; i++)
        {
            w.Write(Kind[i]); w.Write(CellX[i]); w.Write(CellY[i]); w.Write(Amount[i]); w.Write(DueTick[i]); w.Write(IssuedTick[i]);
        }
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        byte version = r.ReadByte();
        if (version == VersionEmpty) { for (int i = 0; i < Kind.Length; i++) Reset(i); return; }
        if (version != VersionInUse) throw new InvalidDataException("commander_orders version mismatch.");
        if (r.ReadInt32() != Kind.Length) throw new InvalidDataException("commander_orders slot count mismatch.");
        for (int i = 0; i < Kind.Length; i++)
        {
            Kind[i] = r.ReadByte(); CellX[i] = r.ReadInt32(); CellY[i] = r.ReadInt32(); Amount[i] = r.ReadInt32();
            DueTick[i] = r.ReadInt32(); IssuedTick[i] = r.ReadInt32();
        }
    }
}
