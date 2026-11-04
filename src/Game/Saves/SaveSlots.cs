// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;

namespace Rts.Game.Saves;

/// <summary>Slot names and file paths of saves: one quicksave slot and numbered named slots, in the folder <see cref="SaveCatalog"/> scans.</summary>
public static class SaveSlots
{
    /// <summary>Name of the quicksave slot.</summary>
    public const string Quick = "quicksave";

    /// <summary>The catalog folder kind a save of this mode goes to.</summary>
    public static SaveKind KindOf(SaveMode mode) => mode == SaveMode.Mission ? SaveKind.Campaign : SaveKind.Skirmish;

    /// <summary>Path of the slot <paramref name="name"/> (made safe for a file name) of <paramref name="kind"/>.</summary>
    public static string PathFor(SaveCatalog catalog, SaveKind kind, string name) =>
        Path.Combine(catalog.FolderOf(kind), Clean(name) + SaveCatalog.Extension);

    /// <summary>The first unused name "save 1", "save 2", ... in the folder of <paramref name="kind"/>.</summary>
    public static string NextName(SaveCatalog catalog, SaveKind kind)
    {
        for (int i = 1; ; i++)
        {
            string name = "save " + i;
            if (!File.Exists(PathFor(catalog, kind, name))) return name;
        }
    }

    /// <summary>Keeps letters, digits, space, '-' and '_' (other characters become '_'); never empty.</summary>
    public static string Clean(string name)
    {
        var chars = name.Trim().Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : '_').ToArray();
        return chars.Length == 0 ? "save" : new string(chars);
    }
}
