// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests;

/// <summary>M8-05: the locale file (flat keys, dotted keys, [voice]), key fallback and loader warnings.</summary>
public sealed class MissionLocaleTests
{
    [Fact]
    public void Flat_and_quoted_dotted_keys_resolve_to_their_text()
    {
        MissionLocale l = MissionLocale.Parse("plain = \"Alpha\"\n\"m1.warning\" = \"Careful\"\n", "t.toml");
        Assert.Equal("Alpha", l.Resolve("plain"));
        Assert.Equal("Careful", l.Resolve("m1.warning"));
        Assert.Equal(2, l.Count);
        Assert.Empty(l.Warnings);
    }

    [Fact]
    public void Unquoted_dotted_keys_are_the_same_keys()
    {
        MissionLocale l = MissionLocale.Parse("m1.line = \"Hello\"\n", "t.toml");
        Assert.True(l.Contains("m1.line"));
        Assert.Equal("Hello", l.Resolve("m1.line"));
    }

    [Fact]
    public void Voice_table_maps_keys_to_voice_files()
    {
        MissionLocale l = MissionLocale.Parse(M85Mission.SampleLocale, "t.toml");
        Assert.Equal("voice/strand/m1.warning.ogg", l.VoiceOf("m1.warning"));
        Assert.Null(l.VoiceOf("m1.line2"));
        Assert.Empty(l.Warnings);
    }

    [Fact]
    public void A_missing_key_resolves_to_the_key_in_brackets_and_is_listed_once()
    {
        MissionLocale l = M85Mission.Sample();
        Assert.Equal("[no.such]", l.Resolve("no.such"));
        Assert.Equal("[no.such]", l.Resolve("no.such"));
        Assert.Equal(new[] { "no.such" }, l.MissingKeys);
        Assert.Empty(l.Warnings);
    }

    [Fact]
    public void Bad_lines_are_skipped_and_listed_as_warnings()
    {
        const string toml = """
            number = 5
            "has space" = "x"
            [voice]
            "m1.a" = "sounds/x.wav"
            "m1.b" = "voice/strand/m1.c.ogg"
            "m1.ok" = "voice/strand/m1.ok.ogg"
            """;
        MissionLocale l = MissionLocale.Parse(toml, "t.toml");
        Assert.False(l.Contains("number"));
        Assert.False(l.Contains("has space"));
        Assert.Null(l.VoiceOf("m1.a"));
        Assert.Null(l.VoiceOf("m1.b"));
        Assert.Equal("voice/strand/m1.ok.ogg", l.VoiceOf("m1.ok"));
        Assert.Equal(4, l.Warnings.Count);
        Assert.All(l.Warnings, w => Assert.StartsWith("t.toml:", w));
    }

    [Fact]
    public void A_missing_file_gives_an_empty_locale_with_a_warning()
    {
        string path = Path.Combine(Path.GetTempPath(), "rts_m805_" + Guid.NewGuid().ToString("N"), "locale.toml");
        MissionLocale l = MissionLocale.Load(path);
        Assert.Equal(0, l.Count);
        Assert.Single(l.Warnings);
        Assert.Equal("[k]", l.Resolve("k"));
    }

    [Fact]
    public void Text_that_is_not_toml_is_an_error_that_names_the_file()
    {
        var e = Assert.Throws<InvalidDataException>(() => MissionLocale.Parse("a = = 1", "broken.toml"));
        Assert.StartsWith("broken.toml:", e.Message);
    }

    [Fact]
    public void Empty_gives_a_fresh_locale_so_missing_lists_are_not_shared()
    {
        MissionLocale.Empty.Resolve("a.b");
        Assert.Empty(MissionLocale.Empty.MissingKeys);
    }

    [Theory]
    [InlineData("m1.ok", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123x", false)]
    public void Key_rules_match_the_script_id_rules(string key, bool valid) =>
        Assert.Equal(valid, MissionLocale.IsValidKey(key));
}
