// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Economy;

namespace Rts.Sim.Tests.S20;

/// <summary>S20: the economy part is written as version 4 (old bytes) unless a harvester has an active field skip, then as version 5.</summary>
public sealed class EconomySkipSaveTests
{
    private static byte[] Bytes(EconomyState e)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) e.Write(w);
        return ms.ToArray();
    }

    private static EconomyState RoundTrip(Simulation from, Simulation into, byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var r = new BinaryReader(ms);
        into.Economy.Read(r);
        Assert.Equal(bytes, Bytes(into.Economy));
        return into.Economy;
    }

    [Fact]
    public void NoSkip_WritesVersion4_AndRoundTrips()
    {
        var (sim, h) = EconomyFixture.Basic();
        EconomyFixture.Run(sim, 200);
        byte[] b = Bytes(sim.Economy);
        Assert.Equal(4, b[0]);
        var (other, _) = EconomyFixture.Basic();
        EconomyState e = RoundTrip(sim, other, b);
        Assert.Equal(0, e.SkipUntil[h.Index]);
    }

    [Fact]
    public void ActiveSkip_WritesVersion5_AndKeepsTheSkip()
    {
        var (sim, h) = EconomyFixture.Basic();
        EconomyFixture.Run(sim, 5);
        sim.Economy.SkipCell[h.Index] = new CellCoord(14, 8);
        sim.Economy.SkipUntil[h.Index] = 1234;
        byte[] b = Bytes(sim.Economy);
        Assert.Equal(5, b[0]);
        var (other, _) = EconomyFixture.Basic();
        EconomyState e = RoundTrip(sim, other, b);
        Assert.Equal(new CellCoord(14, 8), e.SkipCell[h.Index]);
        Assert.Equal(1234, e.SkipUntil[h.Index]);
    }
}
