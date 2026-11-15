// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Lan.Lobby;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Sim.Rules;
using Xunit;

namespace Rts.Game.Tests.S28;

/// <summary>M12-13b: the "Map features" lobby switch reaches <see cref="MapGenParams.Stamps"/>, travels in the LAN lobby and setup blobs and
/// is kept by a skirmish save header.</summary>
public sealed class StampChoiceTests
{
    [Fact]
    public void Off_is_the_default_and_stamps_reach_the_generator_params()
    {
        Assert.False(new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 1).Stamps);
        Assert.False(StampChoice.Params(Biome.RustBelt, MapSize.Small, 2, 1, false).Stamps);
        MapGenParams on = StampChoice.Params(Biome.GlassCoast, MapSize.Medium, 3, 9, true);
        Assert.True(on.Stamps);
        Assert.Equal(new MapGenParams(Biome.GlassCoast, MapSize.Medium, 3, 9, Stamps: true), on);
    }

    [Fact]
    public void The_row_text_and_step()
    {
        Assert.Equal("Map features: Off", StampChoice.Label(false));
        Assert.Equal("Map features: Stamps (ruins, groves, craters)", StampChoice.Label(true));
        Assert.True(StampChoice.Step(false, 1));
        Assert.False(StampChoice.Step(true, -1));
        Assert.True(StampChoice.Step(true, 0));
    }

    private static LobbyData Lobby(bool stamps) => new("Hosty", LobbyMap.FromGenerated(StampChoice.Params(Biome.RustBelt, MapSize.Small, 2, 5, stamps)),
        new[]
        {
            new LobbySlotState(new SkirmishSlot(SlotKind.Human, Faction.Directorate, 2, 0, 0), 0, "Hosty", true),
            new LobbySlotState(new SkirmishSlot(SlotKind.Ai, Faction.Authority, 2, 1, 1)),
        },
        Array.Empty<LobbySpectator>(), 4);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_lobby_blob_round_trips_the_stamp_switch(bool stamps)
    {
        LobbyData back = LobbyCodec.Decode(LobbyCodec.Encode(Lobby(stamps)));
        Assert.Equal(stamps, back.Map.Generated!.Stamps);
        Assert.Equal(Lobby(stamps), back);
    }

    [Fact]
    public void The_setup_blob_round_trips_the_stamp_switch_and_the_guest_sees_it()
    {
        LobbyData d = Lobby(true);
        var setup = new SetupData(d.Map, d.Slots.Select(s => s.Slot).ToList(), 777UL, new Dictionary<int, int> { [0] = 0 }, Array.Empty<int>(),
            new Dictionary<int, string> { [0] = "Hosty" }, 3);
        Assert.True(SetupCodec.Decode(SetupCodec.Encode(setup)).Map.Generated!.Stamps);
        Assert.Contains("stamps", LobbyText.MapLabel(d.Map));
        Assert.DoesNotContain("stamps", LobbyText.MapLabel(Lobby(false).Map));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_save_header_keeps_the_stamp_switch_and_an_older_header_reads_it_as_off(bool stamps)
    {
        var s = new SkirmishSettings(new MapChoice(null, StampChoice.Params(Biome.RustBelt, MapSize.Small, 2, 7, stamps)), SkirmishSettings.Default.Slots, 7);
        Assert.Equal(stamps, RoundTrip(Header(s, GameSaveHeader.CurrentVersion)).Skirmish!.Map.Generated!.Stamps);
        Assert.False(RoundTrip(Header(s, 8)).Skirmish!.Map.Generated!.Stamps);   // version 8 carries no stamps byte
    }

    private static GameSaveHeader RoundTrip(GameSaveHeader h)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) h.Write(w);
        ms.Position = 0;
        return GameSaveHeader.Read(new BinaryReader(ms), h.Version);
    }

    private static GameSaveHeader Header(SkirmishSettings s, int version) => new()
    {
        Mode = SaveMode.Skirmish, MapId = "gen:rust_belt:0:2:7", MapName = "x", RulesHash = 1, Skirmish = s, Build = "b", Version = version,
    };

    [Fact]
    public void A_stamped_choice_resolves_to_a_stamped_generator()
    {
        var s = new SkirmishSettings(new MapChoice(null, StampChoice.Params(Biome.RustBelt, MapSize.Small, 2, 7, true)), SkirmishSettings.Default.Slots, 7);
        Assert.True(s.ResolvedGenerator!.Stamps);
    }
}
