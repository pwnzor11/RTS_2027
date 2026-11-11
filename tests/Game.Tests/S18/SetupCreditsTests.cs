// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;

namespace Rts.Game.Tests.S18;

/// <summary>S18 TESTS-REPO-02: what the About page loads names every pack in docs/CREDITS.md, and the file ships with the game.</summary>
public sealed class SetupCreditsTests
{
    private static string Root()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "RTS_2027.slnx"))) dir = Path.GetDirectoryName(dir)!;
        Assert.False(string.IsNullOrEmpty(dir), "repository root not found");
        return dir;
    }

    private static string Clean(string c) => c.Replace("`", "").Replace("**", "").Trim();

    [Fact]
    public void Every_credits_row_is_in_the_about_list_with_its_full_name()
    {
        string md = File.ReadAllText(Path.Combine(Root(), "docs", "CREDITS.md"));
        IReadOnlyList<string> about = AboutContent.ParseCredits(md);
        Assert.NotSame(AboutContent.FallbackCredits, about);

        int rows = 0;
        foreach (string raw in md.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith('|') || line.StartsWith("|---")) continue;
            string first = Clean(line.Trim('|').Split('|')[0]);
            if (first is "Library" or "Asset" or "(none yet)" || first.Length == 0) continue;
            rows++;
            Assert.Contains(about, a => a.StartsWith(first + " - ", StringComparison.Ordinal));
        }
        Assert.True(rows >= 30, "expected the libraries, the art packs, the audio rows and the stills");
    }

    [Theory]
    [InlineData("Skorpio")]
    [InlineData("Sharm")]
    [InlineData("Kenney")]
    [InlineData("0 A.D.")]
    [InlineData("Liberated Pixel Cup")]
    [InlineData("Noto Sans")]
    [InlineData("MoonSharp")]
    public void Named_packs_and_authors_appear_in_what_about_loads(string name)
    {
        string md = File.ReadAllText(Path.Combine(Root(), "docs", "CREDITS.md"));
        Assert.Contains(name, md);
        Assert.Contains(AboutContent.ParseCredits(md), a => a.Contains(name, StringComparison.Ordinal));
    }

    [Fact]
    public void The_share_alike_licence_of_the_zero_ad_props_is_shown()
    {
        string md = File.ReadAllText(Path.Combine(Root(), "docs", "CREDITS.md"));
        Assert.Contains(AboutContent.ParseCredits(md), a => a.Contains("0 A.D.", StringComparison.Ordinal) && a.Contains("CC BY-SA 3.0", StringComparison.Ordinal));
    }

    [Fact]
    public void The_game_project_ships_credits_where_the_about_screen_looks()
    {
        // AboutScreen.ReadCredits: <folder above data>/docs/CREDITS.md, and the build output puts data/ beside the exe.
        string csproj = File.ReadAllText(Path.Combine(Root(), "src", "Game", "Game.csproj"));
        Assert.Contains(@"Include=""..\..\docs\CREDITS.md"" Link=""docs\CREDITS.md""", csproj);
        int at = csproj.IndexOf(@"Link=""docs\CREDITS.md""", StringComparison.Ordinal);
        string item = csproj[at..csproj.IndexOf("</ItemGroup>", at, StringComparison.Ordinal)];
        Assert.Contains("CopyToPublishDirectory", item);
        Assert.Contains("CopyToOutputDirectory", item);
    }

    [Fact]
    public void A_built_game_folder_has_the_credits_file_next_to_data()
    {
        string data = Path.Combine(AppContext.BaseDirectory, "data");
        Assert.True(Directory.Exists(data), "the test host runs from the Game build output");
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "docs", "CREDITS.md")));
    }
}
