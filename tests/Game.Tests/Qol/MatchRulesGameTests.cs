// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Lan.Lobby;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Qol;

/// <summary>QOL-07 (D-313): the skirmish rules (superweapon limit, reveal map) reach the settings, travel in the LAN lobby and setup blobs and are
/// kept by a skirmish save header; the default is today's game.</summary>
public sealed class MatchRulesGameTests
{
    [Fact]
    public void The_limit_steps_through_unlimited_none_one_two_and_wraps()
    {
        int limit = MatchRules.Default.SuperweaponLimit;
        var seen = new List<int>();
        for (int i = 0; i < 5; i++) { seen.Add(limit); limit = MatchRules.StepLimit(limit, 1); }
        Assert.Equal(new[] { -1, 0, 1, 2, -1 }, seen);
        Assert.Equal(2, MatchRules.StepLimit(-1, -1));
        Assert.Equal(0, MatchRules.StepLimit(77, 1));   // an unknown value counts as the first step
    }

    [Fact]
    public void The_row_texts()
    {
        Assert.Equal("Superweapons: unlimited", MatchRules.LimitLabel(-1));
        Assert.Equal("Superweapons: none", MatchRules.LimitLabel(0));
        Assert.Equal("Superweapons: 1 each", MatchRules.LimitLabel(1));
        Assert.Equal("Map: fog of war", MatchRules.RevealLabel(false));
        Assert.Equal("Map: revealed", MatchRules.RevealLabel(true));
        Assert.True(MatchRules.Default.ToggleReveal().RevealMap);
        Assert.Equal(new MatchRules(0, false), MatchRules.Default.StepSuperweapons(1));
    }

    [Fact]
    public void Settings_with_different_rules_are_not_equal()
    {
        SkirmishSettings plain = SkirmishSettings.Default;
        Assert.Equal(plain, plain with { });
        Assert.NotEqual(plain, plain with { Rules = new MatchRules(0, false) });
        Assert.NotEqual(plain, plain with { Rules = new MatchRules(-1, true) });
        Assert.Contains(new SkirmishSettings(plain.Map, plain.Slots, plain.SimSeed) { Rules = new MatchRules(500, false) }.Validate(), e => e.Contains("Superweapon"));
    }

    private static LobbyData Lobby(MatchRules rules) => new("Hosty", LobbyMap.FromGenerated(new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 5)),
        new[]
        {
            new LobbySlotState(new SkirmishSlot(SlotKind.Human, Faction.Directorate, 2, 0, 0), 0, "Hosty", true),
            new LobbySlotState(new SkirmishSlot(SlotKind.Ai, Faction.Authority, 2, 1, 1)),
        },
        Array.Empty<LobbySpectator>(), 4) { Rules = rules };

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(2, true)]
    [InlineData(-1, true)]
    public void The_lobby_and_setup_blobs_carry_the_rules(int limit, bool reveal)
    {
        var rules = new MatchRules(limit, reveal);
        LobbyData d = Lobby(rules);
        LobbyData back = LobbyCodec.Decode(LobbyCodec.Encode(d));
        Assert.Equal(rules, back.Rules);
        Assert.Equal(d, back);

        var setup = new SetupData(d.Map, d.Slots.Select(s => s.Slot).ToList(), 9UL, new Dictionary<int, int> { [0] = 0 }, Array.Empty<int>(),
            new Dictionary<int, string> { [0] = "Hosty" }, 3) { Rules = rules };
        SetupData setupBack = SetupCodec.Decode(SetupCodec.Encode(setup));
        Assert.Equal(rules, setupBack.Rules);
        Assert.Equal(rules, setupBack.ToSettings(new MapChoice("x.map.toml", null)).Rules);   // the match starts with what the host chose
    }

    [Fact]
    public void A_bad_limit_in_a_blob_is_refused()
    {
        byte[] blob = LobbyCodec.Encode(Lobby(new MatchRules(3, false)));
        // find the limit byte: it follows the command delay (4) and is followed by the reveal flag (0) and the slot count (2)
        int at = -1;
        for (int i = 0; i + 3 < blob.Length; i++) if (blob[i] == 4 && blob[i + 1] == 3 && blob[i + 2] == 0 && blob[i + 3] == 2) at = i + 1;
        Assert.True(at > 0);
        blob[at] = 200;   // not a limit and not 255
        Assert.Throws<InvalidDataException>(() => LobbyCodec.Decode(blob));
    }

    [Fact]
    public void The_lobby_blob_has_a_new_version_so_an_older_build_refuses_it()
    {
        Assert.Equal(2, LobbyCodec.Version);
        byte[] blob = LobbyCodec.Encode(Lobby(MatchRules.Default));
        blob[0] = 1;
        Assert.Throws<InvalidDataException>(() => LobbyCodec.Decode(blob));
    }

    private static GameSaveHeader Header(SkirmishSettings s, int version) => new()
    {
        Mode = SaveMode.Skirmish, MapId = "file:rustbelt_m2.map.toml", MapName = "x", RulesHash = 1, Skirmish = s, Build = "b", Version = version,
    };

    private static GameSaveHeader RoundTrip(GameSaveHeader h)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) h.Write(w);
        ms.Position = 0;
        return GameSaveHeader.Read(new BinaryReader(ms), h.Version);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public void The_save_header_keeps_the_rules_and_an_older_header_reads_the_defaults(int limit, bool reveal)
    {
        var s = new SkirmishSettings(new MapChoice("rustbelt_m2.map.toml", null), SkirmishSettings.Default.Slots, 1) { Rules = new MatchRules(limit, reveal) };
        Assert.Equal(new MatchRules(limit, reveal), RoundTrip(Header(s, GameSaveHeader.CurrentVersion)).Skirmish!.Rules);
        Assert.Equal(MatchRules.Default, RoundTrip(Header(s, 9)).Skirmish!.Rules);   // version 9 carries no rules
    }
}
