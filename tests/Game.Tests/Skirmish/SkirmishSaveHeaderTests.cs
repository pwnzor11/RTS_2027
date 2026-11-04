// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Skirmish;

public sealed class SkirmishSaveHeaderTests
{
    private static GameSaveHeader WithSettings(SkirmishSettings s) => new()
    {
        Mode = SaveMode.Skirmish, MapId = "x", MapName = "x", RulesHash = 1, Skirmish = s, Build = "b",
    };

    private static GameSaveHeader RoundTrip(GameSaveHeader h)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) h.Write(w);
        ms.Position = 0;
        using var r = new BinaryReader(ms);
        return GameSaveHeader.Read(r, GameSaveHeader.CurrentVersion);
    }

    [Fact]
    public void Four_slot_generated_settings_round_trip()
    {
        var s = new SkirmishSettings(
            new MapChoice(null, new MapGenParams(Biome.GlassCoast, MapSize.Large, 3, 99, "named")),
            new[]
            {
                new SkirmishSlot(SlotKind.Human, Faction.Directorate, 1, 3, 2),
                new SkirmishSlot(SlotKind.Closed, Faction.Authority, 2, 0, 0),
                new SkirmishSlot(SlotKind.Ai, Faction.Authority, 5, 4, 1),
                new SkirmishSlot(SlotKind.Ai, Faction.Directorate, 3, 6, 1),
            }, 99);
        Assert.Equal(s, RoundTrip(WithSettings(s)).Skirmish);
    }

    [Fact]
    public void A_map_file_choice_round_trips_and_the_default_stays_the_default()
    {
        var s = new SkirmishSettings(new MapChoice(@"C:\maps\mine.map.toml", null), SkirmishSettings.Default.Slots, 12);
        Assert.Equal(s, RoundTrip(WithSettings(s)).Skirmish);
        Assert.Equal(SkirmishSettings.Default, RoundTrip(WithSettings(SkirmishSettings.Default)).Skirmish);
    }

    [Fact]
    public void A_version_one_header_still_reads_into_two_slot_settings()
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write((byte)SaveMode.Skirmish);
            w.Write("generated:64:77"); w.Write("Gen"); w.Write(0xABCDEFUL); w.Write((byte)0); w.Write(321);
            w.Write(""); w.Write(0UL); w.Write("x");
            w.Write(true);
            w.Write(77UL); w.Write(3); w.Write(2); w.Write((byte)Faction.Directorate); w.Write((byte)Faction.Authority);
            w.Write((byte)0);        // no AIs
            w.Write(0);              // no pending commands
            w.Write((ushort)0);      // no sequences
        }
        ms.Position = 0;
        GameSaveHeader h = GameSaveHeader.Read(new BinaryReader(ms), 1);
        Assert.Equal(1, h.Version);
        Assert.Equal(new SkirmishSettings(77, 3, 2, Faction.Directorate, Faction.Authority), h.Skirmish);
        Assert.Equal(2, h.Skirmish!.Slots.Count);
        Assert.Equal(77UL, h.Skirmish.Map.Generated!.Seed);
    }
}
