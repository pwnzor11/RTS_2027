// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Lan.Lobby;
using Rts.Game.Screens;
using Rts.Sim.Rules;
using Xunit;

namespace Rts.Game.Tests.M9;

/// <summary>M9-05: the lobby, setup and slot-request blobs round-trip, and bad input is always an InvalidDataException.</summary>
public sealed class LobbyBlobTests
{
    private static LobbyData Sample() => new("Hosty", new LobbyMap(null, 0, new MapGenParams(Biome.GlassCoast, MapSize.Medium, 3, 99, "gen", OceanEdges.North | OceanEdges.East, 2)),
        new[]
        {
            new LobbySlotState(new SkirmishSlot(SlotKind.Human, Faction.Directorate, 2, 0, 0, false, 75, 2, true), 0, "Hosty", true),
            new LobbySlotState(new SkirmishSlot(SlotKind.Human, Faction.Authority, 2, 1, 1), 3, "Änn", false),
            new LobbySlotState(new SkirmishSlot(SlotKind.Ai, Faction.LeftBehind, 4, 2, 1, true, 50, 3, false)),
            new LobbySlotState(new SkirmishSlot(SlotKind.Closed, Faction.Authority, 2, 3, 3)),
        },
        new[] { new LobbySpectator(5, "Watcher") }, 4);

    [Fact]
    public void LobbyRoundTrips()
    {
        LobbyData d = Sample();
        Assert.Equal(d, LobbyCodec.Decode(LobbyCodec.Encode(d)));
    }

    [Fact]
    public void CustomMapFileRoundTrips()
    {
        LobbyData d = Sample() with { Map = new LobbyMap("my.map.toml", 0x1234567890UL, null) };
        LobbyData back = LobbyCodec.Decode(LobbyCodec.Encode(d));
        Assert.Equal(d, back);
        Assert.True(back.Map.IsCustom);
        Assert.False(LobbyMap.Curated.IsCustom);
    }

    [Fact]
    public void SetupRoundTrips()
    {
        var d = new SetupData(LobbyMap.Curated, Sample().Slots.Select(s => s.Slot).ToList(), 777UL,
            new Dictionary<int, int> { [0] = 0, [3] = 1 }, new[] { 5 }, new Dictionary<int, string> { [0] = "Hosty", [3] = "Änn", [5] = "Watcher" }, 3);
        SetupData back = SetupCodec.Decode(SetupCodec.Encode(d));
        Assert.Equal(d.Map, back.Map);
        Assert.Equal(d.SimSeed, back.SimSeed);
        Assert.Equal(d.CommandDelay, back.CommandDelay);
        Assert.True(d.Slots.SequenceEqual(back.Slots));
        Assert.Equal(d.PeerSlots.OrderBy(p => p.Key), back.PeerSlots.OrderBy(p => p.Key));
        Assert.Equal(d.Spectators, back.Spectators);
        Assert.Equal(d.Names.OrderBy(p => p.Key), back.Names.OrderBy(p => p.Key));
    }

    [Fact]
    public void SlotChangeRoundTrips()
    {
        var c = new SlotChange(Faction.LeftBehind, 5, 2, 3);
        Assert.Equal(c, SlotChange.Decode(c.Encode()));
        Assert.Equal(-1, SlotChange.Decode(new SlotChange(Faction.Authority, 0, 0).Encode()).MoveTo);
    }

    [Fact]
    public void LongNamesAreCutAtTheLimit()
    {
        LobbyData d = Sample() with { HostName = new string('x', 100) };
        Assert.Equal(LobbyData.MaxNameBytes, LobbyCodec.Decode(LobbyCodec.Encode(d)).HostName.Length);
    }

    [Fact]
    public void WrongVersionTrailingBytesAndTruncationAreRefused()
    {
        byte[] good = LobbyCodec.Encode(Sample());
        var wrong = (byte[])good.Clone();
        wrong[0] = 9;
        Assert.Throws<InvalidDataException>(() => LobbyCodec.Decode(wrong));
        Assert.Throws<InvalidDataException>(() => LobbyCodec.Decode(good.Append((byte)0).ToArray()));
        for (int len = 0; len < good.Length; len++)
        {
            byte[] cut = good[..len];
            Assert.Throws<InvalidDataException>(() => LobbyCodec.Decode(cut));
        }
    }

    [Fact]
    public void RandomBytesAndMutationsOnlyEverThrowInvalidData()
    {
        var rng = new Random(12345);
        byte[] lobby = LobbyCodec.Encode(Sample());
        byte[] setup = SetupCodec.Encode(new SetupData(LobbyMap.Curated, Sample().Slots.Select(s => s.Slot).ToList(), 1UL,
            new Dictionary<int, int> { [0] = 0 }, Array.Empty<int>(), new Dictionary<int, string> { [0] = "H" }, 3));
        byte[] request = new SlotChange(Faction.Authority, 1, 1).Encode();
        for (int i = 0; i < 4000; i++)
        {
            byte[] noise = new byte[rng.Next(0, 80)];
            rng.NextBytes(noise);
            Try(noise);
            foreach (byte[] basis in new[] { lobby, setup, request })
            {
                var m = (byte[])basis.Clone();
                for (int k = rng.Next(1, 4); k > 0; k--) m[rng.Next(m.Length)] = (byte)rng.Next(256);
                Try(m);
            }
        }

        static void Try(byte[] b)
        {
            try { LobbyCodec.Decode(b); } catch (InvalidDataException) { }
            try { SetupCodec.Decode(b); } catch (InvalidDataException) { }
            try { SlotChange.Decode(b); } catch (InvalidDataException) { }
        }
    }

    [Fact]
    public void MapNamesWithFoldersAreRefused()
    {
        foreach (string bad in new[] { "..\\x.map.toml", "a/b.map.toml", "C:evil" })
        {
            byte[] blob = LobbyCodec.Encode(Sample() with { Map = new LobbyMap(bad, 0, null) });
            Assert.Throws<InvalidDataException>(() => LobbyCodec.Decode(blob));
        }
    }

    [Fact]
    public void ValidateAllowsSeveralHumansOnlyWhenAsked()
    {
        var slots = new[]
        {
            new SkirmishSlot(SlotKind.Human, Faction.Authority, 2, 0, 0), new SkirmishSlot(SlotKind.Human, Faction.Authority, 2, 1, 1),
            new SkirmishSlot(SlotKind.Ai, Faction.Authority, 2, 2, 2),
        };
        var s = new SkirmishSettings(new MapChoice(SkirmishSettings.CuratedFile, null), slots, 1);
        Assert.Contains(s.Validate(), e => e.Contains("Only one slot"));
        Assert.Contains(s.Validate(1), e => e.Contains("Only one slot"));
        Assert.DoesNotContain(s.Validate(4), e => e.Contains("Human"));
        Assert.Empty(s.Validate(SkirmishSettings.MaxSlots));
    }

    [Fact]
    public void NamesAreCleanedAndMadeUnique()
    {
        Assert.Equal("Sam Lee", LobbyNames.Clean("  Sam\t  Lee \n"));
        Assert.Equal("", LobbyNames.Clean("   "));
        Assert.Equal("Sam", LobbyNames.Resolve("", "Sam"));
        Assert.Equal("Player", LobbyNames.Resolve("", ""));
        Assert.Equal("Sam 2", LobbyNames.Unique("Sam", new[] { "sam" }));
        Assert.Equal("Sam 3", LobbyNames.Unique("Sam", new[] { "Sam", "Sam 2" }));
    }

    [Fact]
    public void AddressesParse()
    {
        Assert.True(LanAddress.TryParse("192.168.1.20", 47270, out var a));
        Assert.Equal(47270, a.Port);
        Assert.True(LanAddress.TryParse(" 10.0.0.5:5000 ", 47270, out a));
        Assert.Equal(5000, a.Port);
        Assert.True(LanAddress.TryParse("localhost:6000", 47270, out a));
        foreach (string bad in new[] { "", "abc", "1.2.3", "1.2.3.4:0", "1.2.3.4:99999", "1.2.3.4:x", "::1" })
            Assert.False(LanAddress.TryParse(bad, 47270, out _), bad);
    }
}
