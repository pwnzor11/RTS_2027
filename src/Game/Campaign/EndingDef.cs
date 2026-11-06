// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Campaign;

/// <summary>When an ending slide is shown (plan 3.8). Kinds: "completed" (Mission was completed) and "difficulty_min" (Mission was won on tier Value or higher).</summary>
/// <param name="Kind">"completed" or "difficulty_min".</param>
/// <param name="Mission">Mission id the condition looks at.</param>
/// <param name="Value">Tier 1..5 for "difficulty_min"; 0 otherwise.</param>
public sealed record EndingCondition(string Kind, string Mission, int Value)
{
    /// <summary>Whether the progress satisfies the condition.</summary>
    public bool Matches(CampaignProgress p) => Kind switch
    {
        "completed" => p.IsCompleted(Mission),
        "difficulty_min" => p.Difficulty.TryGetValue(Mission, out int tier) && tier >= Value,
        _ => false,
    };
}

/// <summary>One captioned slide: a still (or reused map view) and a locale key for 2-4 sentences.</summary>
/// <param name="Image">Image id (resolved by the EndingScreen, M8-16).</param>
/// <param name="Key">Locale key of the caption.</param>
/// <param name="Condition">Show only when this holds; null = always.</param>
public sealed record EndingSlide(string Image, string Key, EndingCondition? Condition);

/// <summary>
/// <c>data/campaigns/&lt;faction&gt;_ending.toml</c> (plan 3.8): ordered slides plus the captions by key. Slide text is owner-approved
/// text only (D-086); until then every caption reads "PENDING owner text".
/// <code>
/// [ending]
/// faction = "authority"
/// [[slides]]
/// image = "ending_authority_01"
/// key = "ending.authority.01"
/// condition = { kind = "difficulty_min", mission = "auth_01", value = 3 }   # optional
/// [text]
/// "ending.authority.01" = "PENDING owner text"
/// </code>
/// </summary>
/// <param name="Faction">Faction id.</param>
/// <param name="Slides">Slides in order.</param>
/// <param name="Text">Captions by locale key.</param>
public sealed record EndingDef(string Faction, IReadOnlyList<EndingSlide> Slides, IReadOnlyDictionary<string, string> Text)
{
    /// <summary>The path of <c>&lt;faction&gt;_ending.toml</c> under a data folder.</summary>
    public static string PathOf(string dataDir, string faction) => Path.Combine(dataDir, "campaigns", faction + "_ending.toml");

    /// <summary>The slides to show for a progress record, in order.</summary>
    public IReadOnlyList<EndingSlide> SlidesFor(CampaignProgress progress) =>
        Slides.Where(s => s.Condition == null || s.Condition.Matches(progress)).ToList();

    /// <summary>Loads an ending file. Throws <see cref="InvalidDataException"/> with the file name on any problem.</summary>
    public static EndingDef Load(string path)
    {
        try { return Parse(File.ReadAllText(path)); }
        catch (Exception e) when (e is InvalidDataException or IOException) { throw new InvalidDataException(path + ": " + e.Message, e); }
    }

    /// <summary>Parses ending text. Throws <see cref="InvalidDataException"/>.</summary>
    public static EndingDef Parse(string text)
    {
        TomlTable root = CampaignToml.Root(text);
        string faction = CampaignToml.Str(CampaignToml.Table(root, "ending"), "faction");
        if (!Rts.Scripting.MissionLoader.Factions.Contains(faction)) throw new InvalidDataException($"unknown faction '{faction}'");
        if (!root.TryGetValue("slides", out object? so) || so is not TomlTableArray sa || sa.Count == 0) throw new InvalidDataException("missing [[slides]]");
        var slides = new List<EndingSlide>();
        foreach (TomlTable t in sa)
        {
            EndingCondition? cond = null;
            if (t.TryGetValue("condition", out object? co))
            {
                if (co is not TomlTable ct) throw new InvalidDataException("condition must be a table { kind = ..., mission = ... }");
                string kind = CampaignToml.Str(ct, "kind");
                int value = 0;
                if (kind == "difficulty_min")
                {
                    if (!ct.TryGetValue("value", out object? vo) || vo is not long tier || tier < 1 || tier > 5) throw new InvalidDataException("difficulty_min needs value = 1..5");
                    value = (int)tier;
                }
                else if (kind != "completed") throw new InvalidDataException($"unknown condition kind '{kind}' (completed, difficulty_min)");
                cond = new EndingCondition(kind, CampaignToml.Str(ct, "mission"), value);
            }

            slides.Add(new EndingSlide(CampaignToml.Str(t, "image"), CampaignToml.Str(t, "key"), cond));
        }

        var captions = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetValue("text", out object? to))
        {
            if (to is not TomlTable tt) throw new InvalidDataException("[text] must be a table");
            foreach (var kv in tt) captions[kv.Key] = kv.Value as string ?? throw new InvalidDataException($"text '{kv.Key}' must be a string");
        }

        foreach (EndingSlide s in slides)
            if (!captions.ContainsKey(s.Key)) throw new InvalidDataException($"slide key '{s.Key}' has no entry in [text]");
        return new EndingDef(faction, slides, captions);
    }
}
