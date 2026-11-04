// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Llm.Tests;

/// <summary>M5-06: the shipped leader personas load, keep their voices clean of the lore's secrets, and rotate by slot.</summary>
public class PersonaCatalogTests
{
    private static readonly string[] ExpectedIds =
    {
        "leader_auth_wrenfield", "leader_auth_pell", "leader_auth_dray", "leader_auth_fenn",
        "leader_dir_harrowgate", "leader_dir_larkin", "leader_dir_thrall", "leader_dir_lund",
    };

    // Words from each leader's "Secret" paragraph in docs/lore/leaders.md. None may appear in that leader's voice.
    private static readonly Dictionary<string, string[]> SecretWords = new()
    {
        ["leader_auth_wrenfield"] = new[] { "brother", "evacuees", "memo" },
        ["leader_auth_pell"] = new[] { "Annex", "digging", "1974" },
        ["leader_auth_dray"] = new[] { "Sunrise", "warheads", "tunnel head" },
        ["leader_auth_fenn"] = new[] { "depot", "stockpile", "unrecorded" },
        ["leader_dir_harrowgate"] = new[] { "committee", "rewritten", "1974" },
        ["leader_dir_larkin"] = new[] { "lullabies", "battalion", "disappear" },
        ["leader_dir_thrall"] = new[] { "Order Seven", "Sunrise", "detonated" },
        ["leader_dir_lund"] = new[] { "decade", "way up", "without a war" },
    };

    private static string ShippedFile()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "data", "llm", "personas.toml");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException("data/llm/personas.toml");
    }

    private static IReadOnlyList<CommanderPersona> Shipped() => PersonaCatalog.Load(ShippedFile());

    [Fact]
    public void Shipped_file_gives_eight_personas_in_file_order_with_unique_ids()
    {
        IReadOnlyList<CommanderPersona> all = Shipped();
        Assert.Equal(ExpectedIds, all.Select(p => p.Id).ToArray());
        Assert.Equal(all.Count, all.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Shipped_file_has_four_personas_per_faction()
    {
        IReadOnlyList<CommanderPersona> all = Shipped();
        Assert.Equal(4, all.Count(p => p.Faction == "Authority"));
        Assert.Equal(4, all.Count(p => p.Faction == "Directorate"));
    }

    [Fact]
    public void Every_voice_is_short_plain_second_person_and_named()
    {
        foreach (CommanderPersona p in Shipped())
        {
            Assert.InRange(p.Voice.Length, 1, 600);
            Assert.StartsWith("You are ", p.Voice);
            Assert.All(p.Voice, c => Assert.True(c < 128, $"non-ASCII in {p.Id}"));
            Assert.False(string.IsNullOrWhiteSpace(p.DisplayName));
        }
    }

    [Fact]
    public void No_voice_contains_a_secret_word()
    {
        Assert.Equal(ExpectedIds.Length, SecretWords.Count);
        foreach (CommanderPersona p in Shipped())
        {
            foreach (string word in SecretWords[p.Id])
            {
                Assert.DoesNotContain(word, p.Voice, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Pick_rotates_by_player_slot_within_the_faction()
    {
        IReadOnlyList<CommanderPersona> all = Shipped();
        Assert.Equal("leader_auth_wrenfield", PersonaCatalog.Pick(all, "Authority", 0).Id);
        Assert.Equal("leader_auth_pell", PersonaCatalog.Pick(all, "Authority", 1).Id);
        Assert.Equal("leader_auth_dray", PersonaCatalog.Pick(all, "authority", 2).Id);
        Assert.Equal("leader_auth_wrenfield", PersonaCatalog.Pick(all, "Authority", 4).Id);
        Assert.Equal("leader_dir_harrowgate", PersonaCatalog.Pick(all, "Directorate", 0).Id);
        Assert.Equal("leader_dir_lund", PersonaCatalog.Pick(all, "DIRECTORATE", 7).Id);
        Assert.NotEqual(PersonaCatalog.Pick(all, "Authority", 0), PersonaCatalog.Pick(all, "Authority", 1));
    }

    [Fact]
    public void Pick_falls_back_to_plain_when_nothing_matches()
    {
        IReadOnlyList<CommanderPersona> all = Shipped();
        Assert.Equal(CommanderPersona.Plain("Neutral"), PersonaCatalog.Pick(all, "Neutral", 0));
        Assert.Equal(CommanderPersona.Plain("Authority"), PersonaCatalog.Pick(Array.Empty<CommanderPersona>(), "Authority", 3));
    }

    [Fact]
    public void Missing_or_bad_file_gives_an_empty_list()
    {
        string missing = Path.Combine(Path.GetTempPath(), "no_such_personas_" + Guid.NewGuid().ToString("N") + ".toml");
        Assert.Empty(PersonaCatalog.Load(missing));
        Assert.Empty(PersonaCatalog.Parse("this is = = not [[ toml"));
    }

    [Fact]
    public void A_table_missing_faction_name_or_voice_is_skipped_and_the_rest_kept()
    {
        const string text = """
            [good_one]
            faction = "Authority"
            name = "Good One"
            voice = "You are good."

            [no_voice]
            faction = "Authority"
            name = "No Voice"

            [blank_name]
            faction = "Directorate"
            name = "   "
            voice = "You are blank."

            [wrong_type]
            faction = 3
            name = "Wrong"
            voice = "You are wrong."

            [good_two]
            faction = "Directorate"
            name = "Good Two"
            voice = "You are also good."
            """;
        IReadOnlyList<CommanderPersona> got = PersonaCatalog.Parse(text);
        Assert.Equal(new[] { "good_one", "good_two" }, got.Select(p => p.Id).ToArray());
        Assert.Equal("Good Two", got[1].DisplayName);
    }
}
