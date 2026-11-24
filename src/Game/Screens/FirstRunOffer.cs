// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Screens;

/// <summary>
/// QOL-13 (D-313): the first-run offer to play the tutorial. Shown once, on top of the main menu at start-up, for a player who has never been
/// asked (<see cref="Settings.TutorialOffered"/>) and has no saved game; the answer (yes or no) is remembered by marking the offer as shown, so it
/// never comes back. The tutorial stays under Campaign either way.
/// </summary>
public static class FirstRunOffer
{
    /// <summary>Title of the question.</summary>
    public const string Title = "New here?";

    /// <summary>Text of the question.</summary>
    public const string Text = "Play the 5-minute tutorial first? It is always under Campaign.";

    /// <summary>The confirming row.</summary>
    public const string Yes = "Play the tutorial";

    /// <summary>True when the offer should be shown: never shown before, and no saved game (a player with saves is not new).</summary>
    public static bool ShouldOffer(Settings settings, bool hasSave) => !settings.TutorialOffered && !hasSave;

    /// <summary>These settings with the offer marked as shown.</summary>
    public static Settings MarkOffered(Settings settings) => settings with { TutorialOffered = true };

    /// <summary>
    /// Shows the offer over the current screen when <see cref="ShouldOffer"/> says so, and marks it shown (and saves the settings) at once, so
    /// closing the question any other way does not bring it back. Returns true when it was shown.
    /// </summary>
    public static bool Show(GameServices services, bool hasSave, string? settingsPath = null)
    {
        if (!ShouldOffer(services.Settings, hasSave)) return false;
        services.Settings = MarkOffered(services.Settings);
        services.Settings.Save(settingsPath);
        GameServices s = services;
        services.Screens.Push(new ConfirmScreen(s, Title, Text, Yes,
            () => s.Screens.Push(new BriefingScreen(s, CampaignFlow.TutorialId, () => LoadingCampaign.ForTutorial(s, () => CampaignFlow.CreateTutorial(s))))));
        return true;
    }
}
