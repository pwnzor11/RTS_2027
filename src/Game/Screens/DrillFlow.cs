// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Drills;
using Rts.Game.Profile;
using Rts.Game.Sandbox;
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>S47 U-12: starting a drill and what happens when it is won. A drill records only to the profile; no campaign progress is touched.</summary>
internal static class DrillFlow
{
    /// <summary>The briefing of a drill, then its loading screen and game.</summary>
    public static IScreen Briefing(GameServices s, string id) =>
        new BriefingScreen(s, id, () => LoadingScreen.For(s, "Drill", Array.Empty<string>(), null, () => Create(s, id)));

    /// <summary>The game screen of a drill: a win records the best result and shows the end screen, a loss uses the normal Retry / Quit menu.</summary>
    public static GameScreen Create(GameServices s, string id)
    {
        MissionGame game = MissionGame.Create(s.RulesDir, id, 3);
        game.Host.SetDifficulty(3);
        return GameScreen.CreateCampaignMission(s, game, () => Create(s, id), () => Won(s, game));
    }

    private static IScreen? Won(GameServices s, MissionGame game)
    {
        if (game.Host.Outcome != MissionOutcome.Won || game.Def.Drill is not { } def) return null;
        string path = ProfileRecorder.PathFor(s);
        DrillRun run = DrillScoring.Measure(game.Sim, MissionGame.LocalPlayer);
        DrillMedal medal = DrillScoring.Medal(def, run);
        PlayerProfile old = PlayerProfile.Load(path);
        DrillRecord before = old.Drills.TryGetValue(game.Def.Id, out DrillRecord b) ? b : default;
        DrillRecord after = ProfileRecorder.RecordDrill(path, game.Def.Id, run, medal);
        return new DrillEndScreen(s, DrillEndModel.Build(game.Def.Title, def, run, before, after), () => Leave(s, null), () => Leave(s, game.Def.Id));
    }

    /// <summary>Back to the Drills list (refreshed); with <paramref name="retry"/> the same drill starts again from its briefing.</summary>
    private static void Leave(GameServices s, string? retry) => s.Screens.Post(() =>
    {
        while (s.Screens.Top is not DrillsScreen && s.Screens.Screens.Count() > 1) s.Screens.Pop();
        if (s.Screens.Top is DrillsScreen list)
        {
            list.Refresh();
            if (retry is not null) s.Screens.Push(Briefing(s, retry));
        }
        else
        {
            s.Screens.ReplaceAll(new MainMenuScreen(s));
        }
    });
}
