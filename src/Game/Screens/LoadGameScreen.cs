// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;

namespace Rts.Game.Screens;

/// <summary>Builds the Load Campaign / Load Skirmish list and the Replays list over what exists on disk.</summary>
public static class LoadGameScreen
{
    /// <summary>The list of saves of one kind. Choosing one explains why it cannot be started yet (see <see cref="SaveCatalog.CannotResumeReason"/>).</summary>
    public static IScreen Create(GameServices s, SaveKind kind, SaveCatalog? catalog = null)
    {
        IReadOnlyList<SaveEntry> saves = (catalog ?? new SaveCatalog(SaveCatalog.DefaultRoot)).List(kind);
        var rows = saves.Select(save => new ListRow(
            $"{save.Name}   {save.Modified.ToLocalTime():yyyy-MM-dd HH:mm}",
            save.Summary,
            () => s.Screens.Push(new NoticeScreen(s, "Cannot load yet", save.Readable ? SaveCatalog.CannotResumeReason : save.Summary)))).ToList();
        return new ListScreen(s, kind == SaveKind.Campaign ? "Load Campaign" : "Load Skirmish", rows,
            "No saved games yet.", "The game cannot save yet.");
    }

    /// <summary>The list of recorded replays. Choosing one says there is no playback viewer yet and shows how to check the file.</summary>
    public static IScreen CreateReplays(GameServices s, ReplayCatalog? catalog = null)
    {
        IReadOnlyList<ReplayEntry> replays = (catalog ?? new ReplayCatalog(ReplayCatalog.DefaultFolder)).List();
        var rows = replays.Select(replay => new ListRow(
            $"{replay.Name}   {replay.Modified.ToLocalTime():yyyy-MM-dd HH:mm}",
            replay.Summary,
            () => s.Screens.Push(new NoticeScreen(s, "Replay", replay.Readable
                ? "Playing a replay back in the game is not built yet. The recording is valid; check it from a terminal with:\n\n" + replay.VerifyCommand
                : replay.Summary)))).ToList();
        return new ListScreen(s, "Replays", rows, "No replays recorded yet.", "Every skirmish and sandbox session is recorded.");
    }
}
