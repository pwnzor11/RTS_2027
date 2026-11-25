// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Rts.Data;
using Rts.Game.Stats;
using Rts.Sim;
using Rts.Sim.Serialization;

namespace Rts.Game.Saves;

/// <summary>
/// The save file envelope: magic <c>RTSG</c>, a u16 envelope version, an i32 length and the <see cref="GameSaveHeader"/> body, then the
/// simulation's own save bytes (<see cref="Simulation.Save"/>, which for a mission includes the Lua state through the mission state part)
/// up to the end of the sim's own end marker and checksum. U-09: an optional trailing block (magic <c>RTSX</c>, u16 version, i32 length, the
/// end-of-match stats samples, u64 FNV-1a of the payload) follows; it lives outside the sim, so no sim state hash depends on it.
/// </summary>
public static class GameSaveFile
{
    private static readonly byte[] Magic = "RTSG"u8.ToArray();
    private static readonly byte[] StatsMagic = "RTSX"u8.ToArray();
    private const int StatsBlockVersion = 1;
    private const int MaxHeaderBytes = 1 << 24;
    private const int MaxStatsBytes = 1 << 28;

    /// <summary>Writes a save: <paramref name="header"/> then the whole state of <paramref name="sim"/>.</summary>
    public static void Write(Stream output, GameSaveHeader header, Simulation sim, IReadOnlyList<(string Name, string Text)> rulesFiles,
        MatchTimeline? timeline = null)
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
        if (timeline is { Count: > 0 }) WriteStatsBlock(output, timeline);   // U-09
    }

    private static void WriteStatsBlock(Stream output, MatchTimeline timeline)
    {
        using var payload = new MemoryStream();
        using (var pw = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true)) StatsSampler.Write(pw, timeline);
        using var w = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        w.Write(StatsMagic);
        w.Write((ushort)StatsBlockVersion);
        w.Write((int)payload.Length);
        w.Write(payload.GetBuffer(), 0, (int)payload.Length);
        w.Write(SimWriter.Fnv1a(SimWriter.FnvOffset, payload.GetBuffer().AsSpan(0, (int)payload.Length)));
        w.Flush();
    }

    /// <summary>
    /// Reads the optional trailing stats block that follows the sim bytes (U-09): null when the save has none (the stream is at its end), else the
    /// restored samples.
    /// </summary>
    /// <exception cref="SaveLoadException">Bytes follow the sim save but are not an intact stats block.</exception>
    public static MatchTimeline? ReadStatsBlock(Stream input)
    {
        try
        {
            using var r = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
            byte[] magic = r.ReadBytes(4);
            if (magic.Length == 0) return null;
            if (!magic.AsSpan().SequenceEqual(StatsMagic)) throw new SaveLoadException("The save is damaged (unknown data after the game state).");
            int version = r.ReadUInt16();
            if (version != StatsBlockVersion) throw new SaveLoadException($"The save's graph data has an unknown format ({version}).");
            int length = r.ReadInt32();
            if (length < 0 || length > MaxStatsBytes) throw new SaveLoadException("The save is damaged (bad graph data length).");
            byte[] payload = r.ReadBytes(length);
            if (payload.Length != length) throw new SaveLoadException("The save is incomplete (the graph data ends early).");
            if (r.ReadUInt64() != SimWriter.Fnv1a(SimWriter.FnvOffset, payload)) throw new SaveLoadException("The save is damaged (the graph data's checksum does not match).");
            if (r.BaseStream.ReadByte() >= 0) throw new SaveLoadException("The save is damaged (extra data at its end).");
            using var br = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
            return StatsSampler.Read(br);
        }
        catch (Exception e) when (e is EndOfStreamException or InvalidDataException or IOException or ArgumentException or OverflowException)
        {
            throw new SaveLoadException("The save is damaged: " + e.Message, e);
        }
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
