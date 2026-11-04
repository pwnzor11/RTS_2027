// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Rts.Data;
using Rts.Sim;

namespace Rts.Game.Saves;

/// <summary>
/// The save file envelope: magic <c>RTSG</c>, a u16 envelope version, an i32 length and the <see cref="GameSaveHeader"/> body, then the
/// simulation's own save bytes (<see cref="Simulation.Save"/>, which for a mission includes the Lua state through the mission state part)
/// up to the end of the file.
/// </summary>
public static class GameSaveFile
{
    private static readonly byte[] Magic = "RTSG"u8.ToArray();
    private const int MaxHeaderBytes = 1 << 24;

    /// <summary>Writes a save: <paramref name="header"/> then the whole state of <paramref name="sim"/>.</summary>
    public static void Write(Stream output, GameSaveHeader header, Simulation sim, IReadOnlyList<(string Name, string Text)> rulesFiles)
    {
        using var body = new MemoryStream();
        using (var bw = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true)) header.Write(bw);
        using var w = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        w.Write(Magic);
        w.Write((ushort)header.Version);
        w.Write((int)body.Length);
        w.Write(body.GetBuffer(), 0, (int)body.Length);
        w.Flush();
        sim.Save(output, BuildInfo.Version, rulesFiles);
    }

    /// <summary>
    /// Reads the header and leaves <paramref name="input"/> at the start of the simulation bytes.
    /// </summary>
    /// <exception cref="SaveLoadException">Not a save, damaged, or written by a newer build.</exception>
    public static GameSaveHeader ReadHeader(Stream input)
    {
        try
        {
            using var r = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
            if (!r.ReadBytes(4).AsSpan().SequenceEqual(Magic)) throw new SaveLoadException("This is not a saved game of this game.");
            int version = r.ReadUInt16();
            if (version < 1 || version > GameSaveHeader.CurrentVersion)
                throw new SaveLoadException(version > GameSaveHeader.CurrentVersion
                    ? $"This save was made by a newer version of the game (save format {version}; this build reads up to {GameSaveHeader.CurrentVersion})."
                    : $"This save has an unknown format ({version}).");
            int length = r.ReadInt32();
            if (length < 0 || length > MaxHeaderBytes) throw new SaveLoadException("The save is damaged (bad header).");
            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length) throw new SaveLoadException("The save is damaged (it ends early).");
            using var br = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
            return GameSaveHeader.Read(br, version);
        }
        catch (Exception e) when (e is EndOfStreamException or InvalidDataException or IOException or ArgumentException or OverflowException)
        {
            throw new SaveLoadException("The save is damaged: " + e.Message, e);
        }
    }
}
