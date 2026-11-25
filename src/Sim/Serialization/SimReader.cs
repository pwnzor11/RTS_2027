// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Serialization;

/// <summary>Reads what SimWriter wrote. Throws InvalidDataException on any header mismatch.</summary>
public static class SimReader
{
    public readonly record struct State(int Tick, SimRandom Rng);

    /// <summary>Largest save payload accepted (guards the allocation against a damaged length).</summary>
    private const int MaxPayloadBytes = 1 << 30;

    /// <summary>
    /// Reads a whole save from <paramref name="input"/> (magic, version, length, payload, end marker, checksum), refuses a truncated or altered
    /// one with an <see cref="InvalidDataException"/> that says so, and returns a reader over the checked payload (setup header, then state body).
    /// Leaves <paramref name="input"/> just after the checksum, so a caller may read a trailer that follows the sim save.
    /// </summary>
    public static BinaryReader OpenSave(BinaryReader outer)
    {
        try
        {
            byte[] magic = outer.ReadBytes(4);
            if (!magic.AsSpan().SequenceEqual(SimWriter.SaveMagic)) throw new InvalidDataException("Bad save magic.");
            int version = outer.ReadInt32();
            if (version != SimWriter.SaveVersion)
                throw new InvalidDataException($"Unsupported save version {version}; this build reads version {SimWriter.SaveVersion}. Saves from other versions cannot be loaded.");
            int length = outer.ReadInt32();
            if (length < 0 || length > MaxPayloadBytes) throw new InvalidDataException("The save is damaged (bad length).");
            byte[] payload = outer.ReadBytes(length);
            if (payload.Length != length) throw new InvalidDataException("The save is incomplete (it ends early); it was probably cut short while being written or copied.");
            byte[] marker = outer.ReadBytes(4);
            if (!marker.AsSpan().SequenceEqual(SimWriter.EndMarker)) throw new InvalidDataException(marker.Length < 4
                ? "The save is incomplete (the end marker is missing)." : "The save is damaged (the end marker is wrong).");
            ulong stored = outer.ReadUInt64();
            ulong sum = SimWriter.Fnv1a(SimWriter.FnvOffset, magic);
            sum = SimWriter.Fnv1a(sum, BitConverter.GetBytes(version));
            sum = SimWriter.Fnv1a(sum, BitConverter.GetBytes(length));
            sum = SimWriter.Fnv1a(sum, payload);
            if (sum != stored) throw new InvalidDataException("The save is damaged (its checksum does not match); it was changed or corrupted after it was written.");
            return new BinaryReader(new MemoryStream(payload, writable: false), System.Text.Encoding.UTF8);
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("The save is incomplete (it ends early).");
        }
    }

    /// <summary>Reads the setup header from the checked payload made by <see cref="OpenSave"/>.</summary>
    public static SetupHeader ReadHeader(BinaryReader payload) => SetupHeader.Read(payload);

    /// <summary>Reads the state body (tick, RNG, then each part) into <paramref name="parts"/>.</summary>
    public static State ReadState(BinaryReader r, IReadOnlyList<ISimStatePart> parts)
    {
        int tick = r.ReadInt32();
        ulong state = r.ReadUInt64(), inc = r.ReadUInt64();
        for (int i = 0; i < parts.Count; i++)
        {
            string name = r.ReadString();
            if (name != parts[i].Name)
                throw new InvalidDataException($"Expected state part '{parts[i].Name}', found '{name}'.");
            parts[i].Read(r);
        }
        return new State(tick, SimRandom.FromState(state, inc));
    }
}
