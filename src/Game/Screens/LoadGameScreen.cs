// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;
using Rts.Game.Saves;

namespace Rts.Game.Screens;

/// <summary>Builds the Load Campaign / Load Skirmish list and the Replays list over what exists on disk.</summary>
public static class LoadGameScreen
{
    /// <summary>The list of saves of one kind. Choosing one rebuilds the game around it (see <see cref="Start"/>).</summary>
    public static IScreen Create(GameServices s, SaveKind kind, SaveCatalog? catalog = null)
    {
        IReadOnlyList<SaveEntry> saves = (catalog ?? new SaveCatalog(SaveCatalog.DefaultRoot)).List(kind);
        var rows = saves.Select(save => new ListRow(
            $"{save.Name}   {save.Modified.ToLocalTime():yyyy-MM-dd HH:mm}",
            save.Summary,
            () => Start(s, save))).ToList();
        return new ListScreen(s, kind == SaveKind.Campaign ? "Load Campaign" : "Load Skirmish", rows,
            "No saved games yet.", "Pause a game and choose Save game.");
    }

    /// <summary>
    /// Loads <paramref name="save"/> and pushes the game screen over it. A save that cannot be loaded (damaged, other rules or script,
    /// unknown mission) gives a notice with the reason instead; nothing else changes.
    /// </summary>
    public static void Start(GameServices s, SaveEntry save)
    {
        if (!save.Readable)
        {
            s.Screens.Push(new NoticeScreen(s, "Cannot load", save.Summary));
            return;
        }

        try
        {
            LoadedGame loaded = GameLoader.Load(save.Path, s.RulesDir);
            GameScreen screen = GameScreen.CreateFromSave(s, loaded);
            s.Screens.Push(screen);
        }
        catch (Exception e) when (ScreenGuard.IsDataError(e))   // S18 GS-10: not only SaveLoadException (a map or mission file can be damaged too)
        {
            s.Screens.Push(new NoticeScreen(s, "Cannot load", ScreenGuard.Describe(e)));
        }
    }

    /// <summary>The list of recorded replays. Choosing one opens the replay viewer.</summary>
    public static IScreen CreateReplays(GameServices s, ReplayCatalog? catalog = null)
    {
        IReadOnlyList<ReplayEntry> replays = (catalog ?? new ReplayCatalog(ReplayCatalog.DefaultFolder)).List();
        var rows = replays.Select(replay => new ListRow(
            $"{replay.Name}   {replay.Modified.ToLocalTime():yyyy-MM-dd HH:mm}",
            replay.Summary,
            () => ReplayViewerScreen.Start(s, replay))).ToList();
        return new ListScreen(s, "Replays", rows, "No replays recorded yet.", "The last skirmish, mission and sandbox session are recorded.");
    }
}
