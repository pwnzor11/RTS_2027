// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Skirmish;

public sealed class SkirmishCommanderSaveTests
{
    private static SkirmishSettings WithCommanderOnSlot(SkirmishSettings s, int slot)
    {
        var slots = s.Slots.ToArray();
        slots[slot] = slots[slot] with { Commander = true };
        return new SkirmishSettings(s.Map, slots, s.SimSeed);
    }

    private static SkirmishSettings FourSlot() => new(
        new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Small, 4, 55)),
        new[]
        {
            new SkirmishSlot(SlotKind.Human, Faction.Authority, 2, 0, 0),
            new SkirmishSlot(SlotKind.Ai, Faction.Directorate, 3, 1, 1, Commander: true),
            new SkirmishSlot(SlotKind.Closed, Faction.Authority, 2, 2, 0),
            new SkirmishSlot(SlotKind.Ai, Faction.Authority, 4, 3, 1, Commander: true),
        }, 55);

    private static GameSaveHeader WithSettings(SkirmishSettings s, int version = GameSaveHeader.CurrentVersion) => new()
    {
        Mode = SaveMode.Skirmish, MapId = "x", MapName = "x", RulesHash = 1, Skirmish = s, Build = "b", Version = version,
    };

    private static byte[] Bytes(GameSaveHeader h)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) h.Write(w);
        return ms.ToArray();
    }

    private static GameSaveHeader Read(byte[] bytes, int version)
    {
        using var r = new BinaryReader(new MemoryStream(bytes));
        return GameSaveHeader.Read(r, version);
    }

    [Fact]
    public void Current_header_version_is_seven()
    {
        Assert.Equal(7, GameSaveHeader.CurrentVersion);   // M7-14 added the ocean byte, M12-05 the generator layout byte, D-205 the control groups; version 4 is still read
    }

    [Fact]
    public void Version_four_round_trips_the_commander_flags()
    {
        SkirmishSettings s = FourSlot();
        GameSaveHeader back = Read(Bytes(WithSettings(s)), GameSaveHeader.CurrentVersion);
        Assert.Equal(7, back.Version);
        Assert.Equal(s, back.Skirmish);
        Assert.False(back.Skirmish!.Slots[0].Commander);
        Assert.True(back.Skirmish.Slots[1].Commander);
        Assert.False(back.Skirmish.Slots[2].Commander);
        Assert.True(back.Skirmish.Slots[3].Commander);
    }

    [Fact]
    public void Version_three_header_still_reads_and_every_slot_is_not_commanded()
    {
        SkirmishSettings s = WithCommanderOnSlot(new SkirmishSettings(77, 3, 2, Faction.Directorate, Faction.Authority), 1);
        byte[] bytes = Bytes(WithSettings(s, version: 3));
        GameSaveHeader back = Read(bytes, 3);
        Assert.Equal(3, back.Version);
        Assert.Equal(2, back.Skirmish!.Slots.Count);
        var expected = new SkirmishSettings(77, 3, 2, Faction.Directorate, Faction.Authority);
        // M12-05: a header before version 6 names generator layout 0 (the pre-M12 generator)
        if (expected.Map.Generated is { } gen) expected = expected with { Map = new MapChoice(null, gen with { Layout = 0 }) };
        Assert.Equal(expected, back.Skirmish);
        Assert.All(back.Skirmish.Slots, slot => Assert.False(slot.Commander));
    }

    [Fact]
    public void Version_four_appends_one_commander_byte_per_slot_after_version_three()
    {
        SkirmishSettings s = FourSlot();
        byte[] v3 = Bytes(WithSettings(s, version: 3));
        byte[] v4 = Bytes(WithSettings(s, version: 4));
        Assert.Equal(v3.Length + s.Slots.Count, v4.Length);
        Assert.Equal(v3, v4[..v3.Length]);
        Assert.Equal(new byte[] { 0, 1, 0, 1 }, v4[v3.Length..]);
    }

    [Fact]
    public void A_version_four_header_without_skirmish_settings_has_nothing_extra()
    {
        var h = new GameSaveHeader { Mode = SaveMode.Mission, MapId = "m", MapName = "m", RulesHash = 2, Build = "b" };
        GameSaveHeader back = Read(Bytes(h), 4);
        Assert.Null(back.Skirmish);
    }

    [Fact]
    public void A_bad_commander_byte_is_refused()
    {
        byte[] bytes = Bytes(WithSettings(FourSlot(), version: 4));
        bytes[^1] = 7;
        Assert.Throws<InvalidDataException>(() => Read(bytes, 4));
    }
}
