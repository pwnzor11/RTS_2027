// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;

namespace Rts.Scripting.Tests.S22;

/// <summary>
/// M13-21: a static cross-check of every mission folder. The campaign bots only reach the keys their tier path runs, so a key used only
/// by a tier-gated event (a second bomber sortie, a second relief group) could be missing without any runtime locale warning.
/// </summary>
public sealed partial class MissionScriptKeysTests
{
    // A locale key as written in mission.lua: a lower-case prefix with an optional digit, then dotted lower-case parts.
    [GeneratedRegex("\"([a-z]+[0-9]?(?:\\.[a-z0-9_]+)+)\"(\\s*\\.\\.)?")]
    private static partial Regex KeyLiteral();

    [GeneratedRegex("^\\s*\"?([A-Za-z0-9_.]+)\"?\\s*=", RegexOptions.Multiline)]
    private static partial Regex LocaleKey();

    public static IEnumerable<object[]> MissionFolders()
    {
        string root = Path.Combine(TestData.DataDir, "missions");
        foreach (string dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
            if (File.Exists(Path.Combine(dir, "mission.lua")) && File.Exists(Path.Combine(dir, "locale.toml")))
                yield return new object[] { Path.GetFileName(dir) };
    }

    private static HashSet<string> LocaleKeysOf(string text, out HashSet<string> voiceKeys)
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        voiceKeys = new HashSet<string>(StringComparer.Ordinal);
        int voiceAt = text.IndexOf("\n[voice]", StringComparison.Ordinal);
        foreach (Match m in LocaleKey().Matches(text))
        {
            if (voiceAt >= 0 && m.Index > voiceAt) voiceKeys.Add(m.Groups[1].Value);
            else all.Add(m.Groups[1].Value);
        }
        return all;
    }

    /// <summary>Every locale key a mission.lua names (messages, radio, objectives, timers, fail texts) has a text in the mission's locale.toml.</summary>
    [Theory]
    [MemberData(nameof(MissionFolders))]
    public void Every_key_the_script_uses_has_a_locale_text(string id)
    {
        string dir = Path.Combine(TestData.DataDir, "missions", id);
        HashSet<string> keys = LocaleKeysOf(File.ReadAllText(Path.Combine(dir, "locale.toml")), out _);
        var missing = new List<string>();
        foreach (Match m in KeyLiteral().Matches(File.ReadAllText(Path.Combine(dir, "mission.lua"))))
        {
            string key = m.Groups[1].Value;
            bool prefix = m.Groups[2].Success;   // "lb2.radio.post" .. n builds its key at run time
            if (prefix ? !keys.Any(k => k.StartsWith(key, StringComparison.Ordinal)) : !keys.Contains(key)) missing.Add(key);
        }
        Assert.True(missing.Count == 0, $"{id}: keys used by mission.lua but missing from locale.toml: {string.Join(", ", missing.Distinct())}");
    }

    /// <summary>Every [voice] entry names a key that has a text line.</summary>
    [Theory]
    [MemberData(nameof(MissionFolders))]
    public void Every_voice_entry_belongs_to_a_text_line(string id)
    {
        string dir = Path.Combine(TestData.DataDir, "missions", id);
        HashSet<string> keys = LocaleKeysOf(File.ReadAllText(Path.Combine(dir, "locale.toml")), out HashSet<string> voice);
        Assert.True(voice.All(keys.Contains), $"{id}: voice entries without a text line: {string.Join(", ", voice.Where(v => !keys.Contains(v)))}");
    }

    /// <summary>The scripted enemy arrivals added in M13-21 are announced: the key pair exists and the script plays both.</summary>
    [Theory]
    [InlineData("exp_03", "e3.raid2", "e3.radio.raid2")]
    [InlineData("exp_03", "e3.third", "e3.radio.third")]
    [InlineData("exp_04", "e4.finale", "e4.radio.finale")]
    [InlineData("dir_02", "d2.reserve2", "d2.radio.reserve2")]
    [InlineData("lb_01", "lb1.relief2", "lb1.radio.relief2")]
    [InlineData("lb_04", "lb4.landing2", "lb4.radio.landing2")]
    [InlineData("exp_03", "e3.auth2", "e3.radio.auth2")]
    [InlineData("exp_04", "e4.col2", "e4.radio.col2")]
    [InlineData("exp_04", "e4.fuel", "e4.radio.march")]
    [InlineData("auth_01", "a1.raid1", "a1.radio.raid1")]
    [InlineData("auth_01", "a1.raid2", "a1.radio.raid2")]
    [InlineData("auth_02", "a2.wave2", "a2.radio.wave2")]
    [InlineData("auth_02", "a2.wave3", "a2.radio.wave3")]
    [InlineData("auth_03", "a3.home2", "a3.radio.home2")]
    [InlineData("auth_03", "a3.home3", "a3.radio.home3")]
    [InlineData("auth_04", "a4.raid2", "a4.radio.raid2")]
    [InlineData("dir_01", "d1.wave2", "d1.radio.wave2")]
    [InlineData("dir_02", "d2.convoy2", "d2.radio.convoy2")]
    [InlineData("dir_04", "d4.wave2", "d4.radio.wave2")]
    [InlineData("dir_04", "d4.wave3", "d4.radio.wave3")]
    [InlineData("exp_01", "e1.wave2", "e1.radio.wave2")]
    [InlineData("exp_02", "e2.wave2", "e2.radio.wave2")]
    public void Previously_silent_arrivals_have_a_message_and_a_radio_line(string id, string message, string radio)
    {
        string dir = Path.Combine(TestData.DataDir, "missions", id);
        string lua = File.ReadAllText(Path.Combine(dir, "mission.lua"));
        HashSet<string> keys = LocaleKeysOf(File.ReadAllText(Path.Combine(dir, "locale.toml")), out _);
        Assert.Contains(message, keys);
        Assert.Contains(radio, keys);
        Assert.Contains($"Media.MessageKey(\"{message}\")", lua);
        Assert.Contains($"\"{radio}\")", lua);
    }
}
