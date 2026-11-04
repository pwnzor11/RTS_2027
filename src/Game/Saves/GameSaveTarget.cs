// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Host;
using Rts.Game.Menu;

namespace Rts.Game.Saves;

/// <summary>What the pause menu needs to save the running game: where, as what, and from which host. Created by the game screen.</summary>
public sealed class GameSaveTarget
{
    private readonly SaveCatalog _catalog;
    private readonly GameSaveHeader _template;
    private readonly SimHost _host;
    private readonly string _rulesDir;

    /// <summary>Creates the target.</summary>
    /// <param name="catalog">The save folders.</param>
    /// <param name="template">The game's fixed header (see <see cref="GameSaver.Capture"/>).</param>
    /// <param name="host">The host whose simulation and command queue are saved.</param>
    /// <param name="rulesDir">The data/rules directory (its files are embedded in the save).</param>
    public GameSaveTarget(SaveCatalog catalog, GameSaveHeader template, SimHost host, string rulesDir)
    {
        _catalog = catalog;
        _template = template;
        _host = host;
        _rulesDir = rulesDir;
    }

    /// <summary>Which list this game's saves appear in.</summary>
    public SaveKind Kind => SaveSlots.KindOf(_template.Mode);

    /// <summary>The existing saves of this game's kind, newest first.</summary>
    public IReadOnlyList<SaveEntry> Existing() => _catalog.List(Kind);

    /// <summary>The next free "save N" name.</summary>
    public string NextName() => SaveSlots.NextName(_catalog, Kind);

    /// <summary>Saves into the named slot. Returns null on success, else the reason it failed (shown to the player).</summary>
    public string? Save(string slotName)
    {
        try
        {
            GameSaver.Save(_catalog, _template, _host, _rulesDir, slotName);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "The game could not be saved: " + e.Message;
        }
    }
}
