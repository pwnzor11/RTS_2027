// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>What a main-menu entry does when activated.</summary>
public enum MenuAction
{
    /// <summary>Resume the most recent save.</summary>
    Continue,
    /// <summary>Campaign (New / Load).</summary>
    Campaign,
    /// <summary>Skirmish (New / Load).</summary>
    Skirmish,
    /// <summary>LAN game (not built yet).</summary>
    LanGame,
    /// <summary>The map editor.</summary>
    MapEditor,
    /// <summary>List saved replays.</summary>
    Replays,
    /// <summary>The archive (lore) screen.</summary>
    Archive,
    /// <summary>Medals and records (M10-19).</summary>
    Profile,
    /// <summary>Paged controls and basics guide.</summary>
    HowToPlay,
    /// <summary>Settings and About.</summary>
    Settings,
    /// <summary>Leave the game.</summary>
    Quit,
}

/// <summary>One row of the main menu.</summary>
/// <param name="Action">What it does.</param>
/// <param name="Label">The text shown.</param>
/// <param name="Enabled">False greys the row out and makes Enter and click do nothing.</param>
/// <param name="Tooltip">Shown next to a disabled row when it is hovered or selected; null for enabled rows.</param>
public sealed record MainMenuEntry(MenuAction Action, string Label, bool Enabled, string? Tooltip = null);

/// <summary>The main menu's item list and which items are available. Pure, so the order and states are unit-tested.</summary>
public static class MainMenuModel
{
    /// <summary>Tooltip of the features that are not built yet.</summary>
    public const string ComingLater = "Coming in a later update";

    /// <summary>Tooltip of Continue when there is no save to resume.</summary>
    public const string NoSaves = "No saved game to continue";

    /// <summary>The entries in display order. Continue is enabled only when <paramref name="hasSave"/> is true.</summary>
    public static IReadOnlyList<MainMenuEntry> Build(bool hasSave) => new[]
    {
        new MainMenuEntry(MenuAction.Continue, "Continue", hasSave, hasSave ? null : NoSaves),
        new MainMenuEntry(MenuAction.Campaign, "Campaign", true),
        new MainMenuEntry(MenuAction.Skirmish, "Skirmish", true),
        new MainMenuEntry(MenuAction.LanGame, "LAN Game", false, ComingLater),
        new MainMenuEntry(MenuAction.MapEditor, "Map Editor", true),
        new MainMenuEntry(MenuAction.Replays, "Replays", true),
        new MainMenuEntry(MenuAction.Archive, "Archive", true),
        new MainMenuEntry(MenuAction.Profile, "Profile", true),
        new MainMenuEntry(MenuAction.HowToPlay, "How to Play", true),
        new MainMenuEntry(MenuAction.Settings, "Settings", true),
        new MainMenuEntry(MenuAction.Quit, "Quit", true),
    };

    /// <summary>The row that starts selected: Continue when it is usable, otherwise the first enabled row.</summary>
    public static int InitialSelection(IReadOnlyList<MainMenuEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Enabled) return i;
        return 0;
    }

    /// <summary>The next row up or down (<paramref name="step"/> is -1 or +1), wrapping round. Disabled rows can be selected so their tooltip can be read.</summary>
    public static int Move(int selected, int step, int count) => count == 0 ? 0 : ((selected + step) % count + count) % count;
}
