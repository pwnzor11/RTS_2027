// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests;

/// <summary>M8-05: the <c>locale</c> key of mission.toml (schema 2) loads the locale file beside it.</summary>
public sealed class MissionLoaderLocaleTests
{
    /// <summary>Writes a schema-2 mission.toml (the auth_01 header plus the given [mission] lines) and an optional locale.toml into a new folder.</summary>
    private static string Folder(string extraMissionKeys, string? locale)
    {
        string source = File.ReadAllText(Path.Combine(MissionLoader.FolderOf(TestData.DataDir, "auth_01"), "mission.toml"));
        string toml = source.Replace("seed = 2027", "seed = 2027\n" + extraMissionKeys, StringComparison.Ordinal);
        string folder = Path.Combine(Path.GetTempPath(), "rts_m805_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "mission.toml"), toml);
        if (locale is not null) File.WriteAllText(Path.Combine(folder, "locale.toml"), locale);
        return folder;
    }

    [Fact]
    public void A_schema_2_locale_key_loads_the_locale_file()
    {
        string folder = Folder("schema = 2\nlocale = \"locale.toml\"", M85Mission.SampleLocale);
        try
        {
            MissionDef def = MissionLoader.Load(folder);
            Assert.Equal("locale.toml", def.LocaleFile);
            Assert.Equal(2, def.Locale.Count);
            Assert.Equal("Hold the junction.", def.Locale.Resolve("m1.line2"));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void A_mission_without_a_locale_key_has_an_empty_locale()
    {
        string folder = Folder("schema = 2", null);
        try
        {
            MissionDef def = MissionLoader.Load(folder);
            Assert.Equal(0, def.Locale.Count);
            Assert.Empty(def.Locale.Warnings);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void A_declared_locale_file_that_is_missing_is_a_warning_not_an_error()
    {
        string folder = Folder("schema = 2\nlocale = \"locale.toml\"", null);
        try
        {
            MissionDef def = MissionLoader.Load(folder);
            Assert.Single(def.Locale.Warnings);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void A_locale_name_with_a_folder_part_is_rejected()
    {
        string folder = Folder("schema = 2\nlocale = \"sub/locale.toml\"", null);
        try
        {
            var e = Assert.Throws<InvalidDataException>(() => MissionLoader.Load(folder));
            Assert.Contains("'locale' must be a file name", e.Message);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
