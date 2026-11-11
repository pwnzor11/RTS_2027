// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Reflection;
using System.Text.RegularExpressions;
using Rts.Game.Archive;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;

namespace Rts.Game.Tests.S19;

/// <summary>S19 MIS-12 (dialogue shows the speaker's Archive name, not the raw id) and the MIS-07 script debug line.</summary>
public sealed class MissionOverlayNamesTests
{
    private static ArchiveCatalog Archive(SaveTestSupport t) => ArchiveCatalog.Load(ArchiveCatalog.DirectoryFor(t.RulesDir));

    [Fact]
    public void The_opening_line_of_auth_01_is_spoken_by_the_Brigadier_and_shown_by_name()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_01");
        game.Sim.Step(Array.Empty<Rts.Sim.Core.Command>());
        var first = game.Host.Dialogue.First();
        Assert.Equal("leader_auth_wrenfield", first.Speaker);
        Assert.Equal("Brigadier Hester Wrenfield", SpeakerNames.NameOf(first.Speaker, SpeakerNames.CatalogFor(game.Host.Def)));   // the catalog beside the mission's data folder
    }

    [Fact]
    public void Names_come_from_the_Archive_with_the_tutorial_voice_and_a_title_case_fallback()
    {
        using var t = new SaveTestSupport();
        ArchiveCatalog archive = Archive(t);
        Assert.Equal("Brigadier Hester Wrenfield", SpeakerNames.NameOf("leader_auth_wrenfield", archive));
        Assert.Equal("Jessup Calloway-Ross", SpeakerNames.NameOf("leader_lb_calloway_ross", archive));
        Assert.Equal("Instructor", SpeakerNames.NameOf("instructor", archive));
        Assert.Equal("Leader Nobody Known", SpeakerNames.NameOf("leader_nobody_known", archive));
        Assert.Equal("Leader Nobody Known", SpeakerNames.NameOf("leader_nobody_known", null));
    }

    [Fact]
    public void Every_speaker_a_mission_script_uses_has_an_Archive_leader_entry()
    {
        using var t = new SaveTestSupport();
        ArchiveCatalog archive = Archive(t);
        string missions = Path.Combine(Path.GetDirectoryName(t.RulesDir)!, "missions");
        var missing = new List<string>();
        foreach (string lua in Directory.GetFiles(missions, "mission.lua", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(lua), "\"(leader_[a-z_]+)\""))
                if (archive.Find(m.Groups[1].Value) is not { Category: ArchiveCategory.Leader }) missing.Add(Path.GetFileName(Path.GetDirectoryName(lua)) + " " + m.Groups[1].Value);
        Assert.Empty(missing.Distinct());
    }

    [Fact]
    public void The_new_Left_Behind_entry_is_open_and_in_the_Leader_tab()
    {
        using var t = new SaveTestSupport();
        ArchiveEntry e = Archive(t).Find("leader_lb_calloway_ross")!;
        Assert.Equal(ArchiveCategory.Leader, e.Category);
        Assert.Equal(ArchiveUnlockKind.Open, e.Unlock.Kind);
    }

    [Fact]
    public void A_healthy_script_shows_no_debug_line_and_a_script_error_shows_one()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "tut_01");
        game.Sim.Step(Array.Empty<Rts.Sim.Core.Command>());
        Assert.Null(MissionDebugLine.Of(game.Host));

        var errors = (List<string>)typeof(Rts.Scripting.MissionHost).GetField("_errors", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game.Host)!;
        errors.Add("CheckRaids: cannot place auth_conyard at (23,25)");
        string? line = MissionDebugLine.Of(game.Host);
        Assert.NotNull(line);
        Assert.StartsWith("SCRIPT ERROR (1): CheckRaids", line, StringComparison.Ordinal);
        Assert.True(line!.Length <= MissionDebugLine.MaxChars);
    }
}
