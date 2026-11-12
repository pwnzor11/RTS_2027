// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Game.Tests.Audio;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M10;

/// <summary>M10-09: the tracks each music state may play, per faction, with a fake library (all files, some missing, none).</summary>
public sealed class MusicStateTests
{
    private static readonly string[] Named = { "authority", "directorate", "leftbehind", "expedition" };

    private static readonly Faction[] NamedFactions = { Faction.Authority, Faction.Directorate, Faction.LeftBehind, Faction.Expedition };

    private static readonly MusicState[] AllStates = Enum.GetValues<MusicState>();

    // The 20 shipped track names (docs/audio/music_brief.md) for the four factions, plus the shared four.
    private static IEnumerable<string> AllTracks()
    {
        yield return "menu";
        yield return "credits";
        yield return "victory";
        yield return "defeat";
        foreach (string f in Named)
        {
            yield return "battle_" + f + "_1";
            yield return "battle_" + f + "_2";
            yield return "tension_" + f;
            yield return "briefing_" + f;
        }
    }

    private static MusicLibrary Library(params string[] ids) => MusicLibrary.FromIds(ids);

    // "id,id @ group" (the ids in order, then the fade group): compared as text, so the lists are checked by value.
    private static string Pack(string[] ids, string group) => string.Join(",", ids) + " @ " + group;

    private static string Show(MusicLibrary library, MusicState state, Faction faction)
    {
        var player = new MusicPlayer(new RecordingAudio(), library, new Random(1));
        IReadOnlyList<string> ids = player.Candidates(state, faction, out string group);
        return Pack(ids.ToArray(), group);
    }

    [Fact]
    public void WithEveryFileEachStateTakesItsOwnTracks()
    {
        MusicLibrary all = Library(AllTracks().ToArray());
        for (int i = 0; i < Named.Length; i++)
        {
            string f = Named[i];
            Faction faction = NamedFactions[i];
            Assert.Equal(Pack(new[] { "menu" }, "menu"), Show(all, MusicState.Menu, faction));
            Assert.Equal(Pack(new[] { "battle_" + f + "_1", "battle_" + f + "_2" }, "battle_" + f), Show(all, MusicState.Battle, faction));
            Assert.Equal(Pack(new[] { "tension_" + f }, "tension_" + f), Show(all, MusicState.Tension, faction));
            Assert.Equal(Pack(new[] { "briefing_" + f }, "briefing_" + f), Show(all, MusicState.Briefing, faction));
            Assert.Equal(Pack(new[] { "victory" }, "victory"), Show(all, MusicState.Victory, faction));
            Assert.Equal(Pack(new[] { "defeat" }, "defeat"), Show(all, MusicState.Defeat, faction));
            Assert.Equal(Pack(new[] { "credits" }, "credits"), Show(all, MusicState.Credits, faction));
        }
    }

    [Fact]
    public void NeutralHasNoFactionTracksSoItFallsBackToTheSharedOnes()
    {
        MusicLibrary all = Library(AllTracks().ToArray());
        Assert.Equal(Pack(new[] { "menu" }, "menu"), Show(all, MusicState.Briefing, Faction.Neutral));
        Assert.Equal(Pack(new[] { "credits" }, "credits"), Show(all, MusicState.Credits, Faction.Neutral));

        // Battle and tension for a neutral player: no faction files, the generic battle set is none, so the tension tracks of all factions.
        Assert.Equal(Pack(new[] { "tension_authority", "tension_directorate", "tension_expedition", "tension_leftbehind" }, "tension"),
            Show(all, MusicState.Battle, Faction.Neutral));
        Assert.Equal(Pack(new[] { "tension_authority", "tension_directorate", "tension_expedition", "tension_leftbehind" }, "tension"),
            Show(all, MusicState.Tension, Faction.Neutral));
    }

    [Fact]
    public void SomeFilesMissingFallBackOneStepAtATime()
    {
        MusicLibrary some = Library("menu", "battle_authority_1", "battle_directorate_1", "battle_leftbehind_1", "tension_directorate", "briefing_authority");

        Assert.Equal(Pack(new[] { "battle_leftbehind_1" }, "battle_leftbehind"), Show(some, MusicState.Battle, Faction.LeftBehind));

        // No expedition battle: the generic battle set is empty (every battle file is faction-named), so tension.
        Assert.Equal(Pack(new[] { "tension_directorate" }, "tension"), Show(some, MusicState.Battle, Faction.Expedition));

        // No tension_authority: any tension file.
        Assert.Equal(Pack(new[] { "tension_directorate" }, "tension"), Show(some, MusicState.Tension, Faction.Authority));

        Assert.Equal(Pack(new[] { "briefing_authority" }, "briefing_authority"), Show(some, MusicState.Briefing, Faction.Authority));
        Assert.Equal(Pack(new[] { "menu" }, "menu"), Show(some, MusicState.Briefing, Faction.Directorate));
        Assert.Equal(Pack(new[] { "menu" }, "menu"), Show(some, MusicState.Credits, Faction.Authority));
        Assert.Equal(Pack(Array.Empty<string>(), ""), Show(some, MusicState.Victory, Faction.Authority));
    }

    [Fact]
    public void GenericBattleNeverBorrowsAnyNamedFactionsTrack()
    {
        // A generic battle file exists and is used by a faction with no battle files of its own.
        MusicLibrary generic = Library("battle_authority_1", "battle_directorate_1", "battle_leftbehind_1", "battle_expedition_1", "battle_extra");
        Assert.Equal(Pack(new[] { "battle_extra" }, "battle"), Show(generic, MusicState.Battle, Faction.Neutral));

        // Each named faction keeps its own; the generic set does not pick up another faction's file.
        Assert.Equal(Pack(new[] { "battle_expedition_1" }, "battle_expedition"), Show(generic, MusicState.Battle, Faction.Expedition));

        // Only another faction's battle file: no borrowing, so no battle track and no tension track: silence.
        MusicLibrary other = Library("battle_authority_1");
        Assert.Equal(Pack(Array.Empty<string>(), ""), Show(other, MusicState.Battle, Faction.LeftBehind));
    }

    [Fact]
    public void AnEmptyLibraryIsSilentForEveryStateAndFaction()
    {
        foreach (MusicState state in AllStates)
        {
            foreach (Faction faction in NamedFactions.Append(Faction.Neutral))
            {
                Assert.Equal(Pack(Array.Empty<string>(), ""), Show(MusicLibrary.Empty, state, faction));
            }
        }
    }

    [Fact]
    public void EveryStateHasACandidateListForEveryFactionWithAllFiles()
    {
        MusicLibrary all = Library(AllTracks().ToArray());
        foreach (MusicState state in AllStates)
        {
            foreach (Faction faction in NamedFactions.Append(Faction.Neutral))
            {
                Assert.NotEqual(Pack(Array.Empty<string>(), ""), Show(all, state, faction));
            }
        }
    }
}
