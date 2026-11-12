// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Tests.M10;

public sealed class LoadingTests
{
    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");

    private static readonly string[] Factions = { "authority", "directorate", "left_behind", "expedition" };

    [Fact]
    public void The_shipped_tips_load_and_are_short_plain_and_distinct()
    {
        IReadOnlyList<LoadingTip> tips = LoadingTips.Load(RulesDir);
        Assert.InRange(tips.Count, 20, 60);
        Assert.Equal(tips.Count, tips.Select(t => t.Text).Distinct().Count());
        foreach (LoadingTip tip in tips)
        {
            Assert.InRange(tip.Text.Length, 20, 160);
            Assert.All(tip.Text, c => Assert.InRange(c, ' ', '~'));   // the bitmap font draws plain ASCII
            if (tip.Faction is not null) Assert.Contains(tip.Faction, Factions);
        }
    }

    [Fact]
    public void Parse_reads_text_and_faction_and_skips_rows_without_text()
    {
        const string toml = "[[tip]]\ntext = \"One.\"\n\n[[tip]]\nfaction = \"directorate\"\ntext = \"Two.\"\n\n[[tip]]\nfaction = \"directorate\"\n";
        IReadOnlyList<LoadingTip> tips = LoadingTips.Parse(toml);
        Assert.Equal(new[] { new LoadingTip("One.", null), new LoadingTip("Two.", "directorate") }, tips);
        Assert.Empty(LoadingTips.Parse("this is [not toml"));
        Assert.Empty(LoadingTips.Parse("x = 1"));
    }

    [Fact]
    public void A_missing_tips_file_gives_no_tips_and_no_pick()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts2027-tips-" + Guid.NewGuid().ToString("N"), "rules");
        Assert.Empty(LoadingTips.Load(dir));
        Assert.Null(LoadingTips.Pick(Array.Empty<LoadingTip>(), Array.Empty<string>(), 5));
    }

    [Fact]
    public void Pick_is_repeatable_and_only_offers_tips_for_factions_in_the_game()
    {
        var tips = new[] { new LoadingTip("all", null), new LoadingTip("dir", "directorate"), new LoadingTip("lb", "left_behind") };
        Assert.Equal(LoadingTips.Pick(tips, new[] { "authority" }, 41), LoadingTips.Pick(tips, new[] { "authority" }, 41));
        for (int seed = 0; seed < 12; seed++)
            Assert.Equal("all", LoadingTips.Pick(tips, new[] { "authority" }, seed)!.Text);
        var seen = Enumerable.Range(0, 12).Select(s => LoadingTips.Pick(tips, new[] { "directorate" }, s)!.Text).ToHashSet();
        Assert.Equal(new[] { "all", "dir" }.Order(), seen.Order());
        Assert.NotNull(LoadingTips.Pick(tips, Array.Empty<string>(), -7));   // a negative seed still picks
    }

    // The numbers a tip states are checked against the rules data, so a retune cannot leave a tip wrong.
    [Fact]
    public void Tip_facts_match_the_rules_data()
    {
        TomlTable game = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(Path.Combine(RulesDir, "game.toml")))!;
        var economy = (TomlTable)game["economy"];
        Assert.Equal(500L, economy["sell_refund_permille"]);                                                   // "Selling a building returns half of its cost."
        Assert.Equal(2 * (long)economy["credits_ashfall"], (long)economy["credits_pitchblende"]);              // "Pitchblende is worth twice as much as ashfall."
        Assert.True(((TomlArray)economy["low_power_slowdown_permille"]).Count > 0);                            // "Low power slows production."
        Assert.True((bool)((TomlTable)game["supply"])["enabled"]);                                             // the supply-cap tip
        Assert.True((bool)((TomlTable)game["veterancy"])["enabled"]);                                          // Veteran, Elite, Hero
        Assert.Equal(3, ((TomlArray)((TomlTable)game["veterancy"])["thresholds"]).Count);
        Assert.True((long)((TomlTable)game["radiation"])["ashfall_damage"] > 0);                               // soldiers on a field take damage
        Assert.Equal("", ((TomlTable)game["faction"])["left_behind"] is TomlTable lb ? lb["conyard"] : null);  // no Construction Yard
        Assert.Contains("lb_mother_rig_packed", ((TomlArray)((TomlTable)((TomlTable)game["faction"])["left_behind"])["units"]).Select(x => (string)x!));
    }

    [Fact]
    public void Tip_key_bindings_match_the_how_to_play_pages()
    {
        string guide = string.Join("\n", Rts.Game.Menu.HowToPlayContent.Pages.SelectMany(p => p.Lines));
        foreach (string phrase in new[] { "Q Structures, W Defences, E Infantry, R Vehicles, T Upgrades", "Press A, then right-click ground", "S stops. G guards", "X scatters",
                     "Right-click a cameo to cancel", "Home centres on your base", "Ctrl+F5..F8 store a view", "F1 or ? lists every key", "Space pause the game", "press D, then right-click the cell" })
            Assert.Contains(phrase, guide);
    }

    [Fact]
    public void A_single_step_load_runs_the_build_once_and_gives_its_screen()
    {
        int built = 0;
        var screen = new FakeScreen();
        LoadingModel model = LoadingModel.Single("Building", () => { built++; return screen; });
        Assert.False(model.HasProgress);
        Assert.Equal("Building", model.CurrentLabel);
        Assert.False(model.IsOver);
        model.Advance();
        Assert.True(model.IsOver);
        Assert.Same(screen, model.Result);
        model.Advance();
        Assert.Equal(1, built);
    }

    [Fact]
    public void A_stepped_load_moves_the_bar_one_step_per_advance_then_finishes()
    {
        var log = new List<string>();
        var screen = new FakeScreen();
        var model = new LoadingModel(new[]
        {
            new LoadingStep("Map", () => log.Add("map")), new LoadingStep("Rules", () => log.Add("rules")), new LoadingStep("Sprites", () => log.Add("sprites")),
        }, () => { log.Add("finish"); return screen; });
        Assert.True(model.HasProgress);
        Assert.Equal(0f, model.Fraction);
        model.Advance();
        Assert.Equal(1f / 3f, model.Fraction, 3);
        Assert.Equal("Rules", model.CurrentLabel);
        model.Advance();
        model.Advance();
        Assert.Equal(new[] { "map", "rules", "sprites", "finish" }, log);   // the finish runs in the advance after the last step
        Assert.Same(screen, model.Result);
        Assert.Equal(1f, model.Fraction);
    }

    [Fact]
    public void A_step_that_throws_leaves_the_model_failed_and_stops()
    {
        var model = new LoadingModel(new[] { new LoadingStep("Map", () => throw new InvalidOperationException("bad map")), new LoadingStep("Never", () => Assert.Fail("ran")) },
            () => new FakeScreen());
        model.Advance();
        Assert.True(model.IsOver);
        Assert.IsType<InvalidOperationException>(model.Failed);
        Assert.Null(model.Result);
        model.Advance();
        Assert.Equal(0, model.Done);
    }

    [Fact]
    public void An_empty_load_goes_straight_to_the_finish()
    {
        var screen = new FakeScreen();
        var model = new LoadingModel(Array.Empty<LoadingStep>(), () => screen);
        Assert.Equal(0f, model.Fraction);
        model.Advance();
        Assert.Same(screen, model.Result);
    }

    [Fact]
    public void Art_prefers_the_missions_own_still_then_a_faction_still_then_nothing()
    {
        string data = Path.GetFullPath(Path.Combine(RulesDir, ".."));
        string own = LoadingArt.Choose(data, "authority", "still_directorate_act3", 0)!;
        Assert.EndsWith("still_directorate_act3.png", own);
        for (int seed = 0; seed < 6; seed++)
            Assert.Contains("still_left_behind_act", LoadingArt.Choose(data, "left_behind", "no_such_still", seed)!);
        Assert.Equal(LoadingArt.Choose(data, "expedition", null, 4), LoadingArt.Choose(data, "expedition", null, 4));
        Assert.Null(LoadingArt.Choose(data, null, null, 0));
        Assert.Null(LoadingArt.Choose(data, "no_such_faction", null, 0));
    }

    [Fact]
    public void Faction_ids_follow_the_still_file_names()
    {
        string data = Path.GetFullPath(Path.Combine(RulesDir, ".."));
        foreach (Faction f in new[] { Faction.Authority, Faction.Directorate, Faction.LeftBehind, Faction.Expedition })
            Assert.True(File.Exists(Path.Combine(data, "images", "stills", $"still_{LoadingArt.IdOf(f)}_act2.png")), f.ToString());
        Assert.Null(LoadingArt.IdOf(Faction.Neutral));
    }

    private sealed class FakeScreen : IScreen
    {
        public bool BlocksUpdateBelow => true;
        public bool BlocksDrawBelow => true;
        public void Enter() { }
        public void Exit() { }
        public void Update(Rts.Game.Input.InputFrame input, Microsoft.Xna.Framework.GameTime t) { }
        public void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch sb) { }
    }
}
