// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Scripting;

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
        string title = start.Game.Def.Title;
        return GameScreen.CreateCampaignMission(s, start.Game, () => CreateMission(s, svc, faction, missionId, tier), () =>
        {
            if (start.Game.Host.Outcome != MissionOutcome.Won) return null;   // a loss uses the normal Retry / Quit menu
            CampaignProgress before = svc.Progress(faction);
            CampaignProgress after = svc.RecordWin(faction, missionId, tier);
            bool final = MissionSelectModel.IsFinal(svc, faction, missionId);
            ArchiveCatalog catalog = LoadCatalog(s);
            var model = DebriefModel.Build(title, missionId, start.Game.Sim.Tick, start.Game.Host.Objectives, before, after, tier, final, catalog);
            return new DebriefScreen(s, model, () => AfterWin(s, svc, faction, final));
        });
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
    public static void AfterWin(GameServices s, CampaignService svc, string faction, bool final) => s.Screens.Post(() =>
    {
        if (!s.Screens.Screens.Any(x => x is MissionSelectScreen)) { GameFlow.QuitToMenu(s); return; }
        while (s.Screens.Top is not MissionSelectScreen) s.Screens.Pop();   // debrief, game, briefing
        ((MissionSelectScreen)s.Screens.Top).Refresh();
        if (final && LoadEnding(s, svc, faction) is { } ending)
            s.Screens.Push(new EndingScreen(s, new EndingModel(ending, svc.Progress(faction)), Path.Combine(DataDir(s), "images", "endings")));
    });
}
