// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Scripting;
using Rts.Game.Sandbox;

namespace Rts.Game.Screens;

/// <summary>Wiring shared by the campaign screens (plan 3.4): the service, starting a mission, and what happens after a win.</summary>
internal static class CampaignFlow
{
    /// <summary>The data folder beside the rules folder.</summary>
    public static string DataDir(GameServices s) => Path.GetFullPath(Path.Combine(s.RulesDir, ".."));

    /// <summary>A campaign service on the player's profile.</summary>
    public static CampaignService Service(GameServices s) => new(DataDir(s));

    /// <summary>Builds the game screen of a campaign mission: starts it through the service, and on a win records it and shows the debrief.</summary>
    public static GameScreen CreateMission(GameServices s, CampaignService svc, string faction, string missionId, int tier)
    {
        CampaignStart start = svc.Start(s.RulesDir, faction, missionId, tier);
        return GameScreen.CreateCampaignMission(s, start.Game, () => CreateMission(s, svc, faction, missionId, tier), WinScreen(s, svc, faction, missionId, tier, start.Game));
    }

    /// <summary>The game-over hook of a campaign mission: on a win, records it and returns the debrief; on a loss, null (the normal Retry / Quit menu).</summary>
    private static Func<IScreen?> WinScreen(GameServices s, CampaignService svc, string faction, string missionId, int tier, MissionGame game) => () =>
    {
        if (game.Host.Outcome != MissionOutcome.Won) return null;   // a loss uses the normal Retry / Quit menu
        CampaignProgress before = svc.Progress(faction);
        CampaignProgress after = svc.RecordWin(faction, missionId, tier);
        bool final = MissionSelectModel.IsFinal(svc, faction, missionId);
        ArchiveCatalog catalog = LoadCatalog(s);
        var model = DebriefModel.Build(game.Def.Title, missionId, game.Sim.Tick, game.Host.Objectives, before, after, tier, final, catalog);
        bool saved = svc.LastWinSaved;   // S18 GS-05: a failed progress write is told after the debrief
        return new DebriefScreen(s, model, () => AfterWin(s, svc, faction, final, saved));
    };

    /// <summary>
    /// S18 MIS-02 / GS-02: the campaign wiring of a mission restored from a save: Restart through the campaign (same tier, the campaign tech) and the
    /// win hook (RecordWin, debrief, unlocks, ending). The tutorial restarts as the tutorial with the normal end menu. Null for any other mission.
    /// </summary>
    public static (Func<GameScreen> Restart, Func<IScreen?> GameOver)? Resume(GameServices s, MissionGame game)
    {
        if (game.Def.Id == TutorialId) return (() => CreateTutorial(s), static () => null);
        CampaignService svc = Service(s);
        if (svc.CampaignMissionOf(game.Host) is not { } link) return null;
        return (() => CreateMission(s, svc, link.Faction, link.MissionId, link.Tier), WinScreen(s, svc, link.Faction, link.MissionId, link.Tier, game));
    }

    /// <summary>Folder name of the shared tutorial (M8-09) under data/missions. It is not part of any faction's campaign.</summary>
    public const string TutorialId = "tut_01";

    /// <summary>The tutorial mission at tier 1 (M8-09). No campaign progress is recorded; the end uses the normal game-over menu.</summary>
    public static GameScreen CreateTutorial(GameServices s)
    {
        MissionGame game = MissionGame.Create(s.RulesDir, TutorialId, 1);
        game.Host.SetDifficulty(1);
        return GameScreen.CreateCampaignMission(s, game, () => CreateTutorial(s), () => null);
    }

    /// <summary>The Archive catalog (empty when it cannot be read).</summary>
    public static ArchiveCatalog LoadCatalog(GameServices s)
    {
        try { return ArchiveCatalog.Load(ArchiveCatalog.DirectoryFor(s.RulesDir)); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { return ArchiveCatalog.Empty; }
    }

    /// <summary>The ending for a faction, or null when the campaign names none or the file is missing or unreadable.</summary>
    public static EndingDef? LoadEnding(GameServices s, CampaignService svc, string faction)
    {
        string stem = svc.Campaign(faction).Ending;
        if (stem.Length == 0) return null;
        try { return EndingDef.Load(Path.Combine(DataDir(s), "campaigns", stem + ".toml")); }
        catch (InvalidDataException) { return null; }
    }

    /// <summary>Leaves the debrief: back to the mission select (refreshed), then the ending when the faction's last mission was won.</summary>
    public static void AfterWin(GameServices s, CampaignService svc, string faction, bool final, bool saved = true) => s.Screens.Post(() =>
    {
        AfterWinScreens(s, svc, faction, final);
        if (!saved) s.Screens.Push(new NoticeScreen(s, ProgressNotSavedTitle, ProgressNotSavedText));
    });

    // S18 GS-05: shown when RecordWin could not write the campaign progress file.
    private const string ProgressNotSavedTitle = "Progress not saved";
    private const string ProgressNotSavedText = "The campaign progress file could not be written, so this win is not kept. Check the free disk space and that the settings folder can be written.";

    private static void AfterWinScreens(GameServices s, CampaignService svc, string faction, bool final)
    {
        if (!s.Screens.Screens.Any(x => x is MissionSelectScreen))
        {
            // S18 MIS-02: a mission loaded from a save has no mission select under it: back to the main menu, then the ending if it was the last.
            s.Screens.ReplaceAll(new MainMenuScreen(s));
            if (final && LoadEnding(s, svc, faction) is { } end)
                s.Screens.Push(new EndingScreen(s, new EndingModel(end, svc.Progress(faction)), Path.Combine(DataDir(s), "images", "endings")));
            return;
        }

        while (s.Screens.Top is not MissionSelectScreen) s.Screens.Pop();   // debrief, game, briefing
        ((MissionSelectScreen)s.Screens.Top).Refresh();
        foreach (CampaignScreen c in s.Screens.Screens.OfType<CampaignScreen>()) c.Refresh();   // S18 GS-04: newly opened factions, counts
        if (final && LoadEnding(s, svc, faction) is { } ending)
            s.Screens.Push(new EndingScreen(s, new EndingModel(ending, svc.Progress(faction)), Path.Combine(DataDir(s), "images", "endings")));
    }
}
