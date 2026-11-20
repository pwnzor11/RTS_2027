// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;

namespace Rts.Scripting.Tests.M11_31;

/// <summary>
/// M11-31: a missing locale key shows the player "[key]" on screen, so none may ship. These run each mission's locale through the real
/// <see cref="MissionLocale"/> resolver for every key literal in its script and for the keys the script builds at run time
/// (lb_02 plays <c>"lb2.radio.post" .. n</c> for n = 1..3). <c>S22.MissionScriptKeysTests</c> already checks the static key list; this adds the resolver, the
/// built-key runs and the shape of the texts.
/// </summary>
public sealed partial class MissionLocaleCompleteTests
{
    [GeneratedRegex("\"([a-z]+[0-9]?(?:\\.[a-z0-9_]+)+)\"(\\s*\\.\\.)?")]
    private static partial Regex KeyLiteral();

    public static IEnumerable<object[]> Missions() => S22.MissionScriptKeysTests.MissionFolders();

    private static MissionLocale LocaleOf(string id) => MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, id)).Locale;

    /// <summary>Every key literal of the script resolves through the loader's locale: no "[key]" text and nothing recorded as missing.</summary>
    [Theory]
    [MemberData(nameof(Missions))]
    public void Every_script_key_resolves_to_real_text(string id)
    {
        MissionLocale locale = LocaleOf(id);
        string lua = File.ReadAllText(Path.Combine(MissionLoader.FolderOf(TestData.DataDir, id), "mission.lua"));
        var shown = new List<string>();
        foreach (Match m in KeyLiteral().Matches(lua))
        {
            if (m.Groups[2].Success) continue;   // a prefix: the built keys are checked below
            shown.Add(locale.Resolve(m.Groups[1].Value));
        }
        Assert.Empty(locale.MissingKeys);
        Assert.All(shown, t => Assert.False(t.StartsWith('[') || string.IsNullOrWhiteSpace(t), $"{id}: a script key shows '{t}'"));
    }

    /// <summary>A key the script builds ("prefix" .. n) has a gap-free numbered run from 1 in the locale, so no n plays a missing line.</summary>
    [Theory]
    [MemberData(nameof(Missions))]
    public void Built_keys_have_every_number_from_one_to_the_highest(string id)
    {
        MissionLocale locale = LocaleOf(id);
        string lua = File.ReadAllText(Path.Combine(MissionLoader.FolderOf(TestData.DataDir, id), "mission.lua"));
        foreach (string prefix in KeyLiteral().Matches(lua).Where(m => m.Groups[2].Success).Select(m => m.Groups[1].Value).Distinct())
        {
            int top = 0;
            while (locale.Contains(prefix + (top + 1))) top++;
            Assert.True(top >= 1, $"{id}: no locale key {prefix}1 for the keys the script builds from \"{prefix}\"");
            for (int n = top + 1; n <= top + 3; n++)
                Assert.False(locale.Contains(prefix + n), $"{id}: {prefix}{n} exists but {prefix}{top + 1} does not");
        }
    }

    /// <summary>Left Behind 02 builds "lb2.radio.post" .. state.posts for 1..3 posts; each must have its line.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Left_behind_02_has_a_radio_line_for_every_post(int posts)
    {
        MissionLocale locale = LocaleOf("lb_02");
        Assert.True(locale.Contains("lb2.radio.post" + posts), $"lb2.radio.post{posts} is missing");
        Assert.DoesNotContain('[', locale.Resolve("lb2.radio.post" + posts));
        Assert.Empty(locale.MissingKeys);
    }
}
