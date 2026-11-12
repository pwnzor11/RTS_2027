// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Campaign;

namespace Rts.Game.Screens;

/// <summary>Wraps the building of a campaign mission or the tutorial in a <see cref="LoadingScreen"/> (M10-20).</summary>
internal static class LoadingCampaign
{
    /// <summary>The loading screen of a campaign mission: the mission's act still when its campaign names one, else a still of the faction.</summary>
    public static IScreen ForMission(GameServices s, string faction, string missionId, Func<IScreen> build) =>
        LoadingScreen.For(s, FactionPickModel.NameOf(faction) + " campaign", new[] { faction }, StillOf(s, faction, missionId), build);

    /// <summary>The loading screen of the tutorial (no faction art, no faction tips).</summary>
    public static IScreen ForTutorial(GameServices s, Func<IScreen> build) =>
        LoadingScreen.For(s, "Tutorial", Array.Empty<string>(), null, build);

    private static string? StillOf(GameServices s, string faction, string missionId)
    {
        try
        {
            return CampaignDef.Load(CampaignDef.PathOf(CampaignFlow.DataDir(s), faction)).Missions.FirstOrDefault(m => m.Id == missionId)?.Still;
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            return null;
        }
    }
}
