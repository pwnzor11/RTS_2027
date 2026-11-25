// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Serialization;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.S45;

/// <summary>M-27 (SIMCORE-04): a save carries a length, an end marker and a checksum; a cut-short or changed save is refused, never half-loaded.</summary>
public class SaveIntegrityTests
{
    private static byte[] MakeSave(out SimSetup setup, out ulong hash)
    {
        setup = ScriptedGame.Setup();
        var script = ScriptedGame.Script(200);
        var sim = ScriptedGame.NewSim(setup);
        for (int i = 0; i < 100; i++) sim.Step(script);
        hash = sim.ComputeStateHash();
        var ms = new MemoryStream();
        sim.Save(ms);
        return ms.ToArray();
    }

    [Fact]
    public void IntactSave_LoadsToTheSameStateHash()
    {
        byte[] bytes = MakeSave(out var setup, out ulong hash);
        Assert.Equal(hash, Simulation.Load(new MemoryStream(bytes), setup).ComputeStateHash());
        Assert.Equal("RTSE", System.Text.Encoding.ASCII.GetString(bytes, bytes.Length - 12, 4));   // end marker, then the 8-byte checksum
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(5000)]
    public void TruncatedSave_IsRefusedWithAMessage(int cut)
    {
        byte[] bytes = MakeSave(out var setup, out _);
        var ex = Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes[..^cut]), setup));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        Assert.Contains("incomplete", ex.Message);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.9)]
    [InlineData(0.99)]
    public void OneByteAlteredSave_IsRefusedWithAMessage(double where)
    {
        byte[] bytes = MakeSave(out var setup, out _);
        int at = 12 + (int)((bytes.Length - 24) * where);   // inside the payload
        bytes[at] ^= 0x01;
        var ex = Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes), setup));
        Assert.Contains("checksum", ex.Message);
    }

    [Fact]
    public void AlteredChecksumOrMarker_IsRefused()
    {
        byte[] bytes = MakeSave(out var setup, out _);
        byte[] sumBad = (byte[])bytes.Clone(); sumBad[^1] ^= 0xFF;
        Assert.Contains("checksum", Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(sumBad), setup)).Message);
        byte[] markBad = (byte[])bytes.Clone(); markBad[^12] = (byte)'X';
        Assert.Contains("end marker", Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(markBad), setup)).Message);
    }

    [Fact]
    public void BytesAfterTheSave_AreLeftForTheCaller()
    {
        byte[] bytes = MakeSave(out var setup, out ulong hash);
        var ms = new MemoryStream();
        ms.Write(bytes); ms.WriteByte(0x5A);
        ms.Position = 0;
        Assert.Equal(hash, Simulation.Load(ms, setup).ComputeStateHash());
        Assert.Equal(bytes.Length, ms.Position);
        Assert.Equal(0x5A, ms.ReadByte());
    }

    [Fact]
    public void SaveVersion_Is16()
    {
        Assert.Equal(16, SimWriter.SaveVersion);
    }
}
