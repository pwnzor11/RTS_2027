// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Archive;

/// <summary>
/// The navigation state of the Archive screen with no graphics: the selected tab, the entries of that tab, the selected
/// entry and what is locked. The screen draws it; tests drive it directly.
/// </summary>
public sealed class ArchiveBrowser
{
    private static readonly ArchiveCategory[] Tabs = Enum.GetValues<ArchiveCategory>();
    private ArchiveEntry[] _entries = Array.Empty<ArchiveEntry>();
    private ArchiveCategory _tab;
    private int _selected;

    /// <summary>Creates a browser on the first tab.</summary>
    public ArchiveBrowser(ArchiveCatalog catalog, ArchiveProgress progress)
    {
        Catalog = catalog;
        Progress = progress;
        Rebuild();
    }

    /// <summary>The authored entries.</summary>
    public ArchiveCatalog Catalog { get; }

    /// <summary>What the player has unlocked.</summary>
    public ArchiveProgress Progress { get; }

    /// <summary>All tabs in display order.</summary>
    public IReadOnlyList<ArchiveCategory> AllTabs => Tabs;

    /// <summary>The selected tab.</summary>
    public ArchiveCategory Tab
    {
        get => _tab;
        set
        {
            if (value == _tab) return;
            _tab = value;
            _selected = 0;
            Rebuild();
        }
    }

    /// <summary>Entries of the selected tab in authored order.</summary>
    public IReadOnlyList<ArchiveEntry> Entries => _entries;

    /// <summary>Index of the selected entry within <see cref="Entries"/>.</summary>
    public int Selected
    {
        get => _selected;
        set => _selected = _entries.Length == 0 ? 0 : Math.Clamp(value, 0, _entries.Length - 1);
    }

    /// <summary>The selected entry, or null when the tab is empty.</summary>
    public ArchiveEntry? Current => _selected < _entries.Length ? _entries[_selected] : null;

    /// <summary>Whether an entry can be read now.</summary>
    public bool IsUnlocked(ArchiveEntry entry) => Progress.IsUnlocked(entry);

    /// <summary>Unlocked and total entry counts of a tab.</summary>
    public (int Unlocked, int Total) Count(ArchiveCategory tab)
    {
        int total = 0, open = 0;
        foreach (ArchiveEntry e in Catalog.InCategory(tab))
        {
            total++;
            if (Progress.IsUnlocked(e)) open++;
        }
        return (open, total);
    }

    /// <summary>Moves to the next (+1) or previous (-1) tab, wrapping.</summary>
    public void MoveTab(int delta) => Tab = Tabs[((int)_tab + delta % Tabs.Length + Tabs.Length) % Tabs.Length];

    /// <summary>Moves the entry selection by <paramref name="delta"/>, wrapping.</summary>
    public void MoveEntry(int delta)
    {
        if (_entries.Length == 0) return;
        _selected = ((_selected + delta) % _entries.Length + _entries.Length) % _entries.Length;
    }

    /// <summary>The hint for a locked entry (empty when it is open); ids are named by their catalog title when there is one.</summary>
    public string Hint(ArchiveEntry entry) => entry.Unlock.Hint(id => Catalog.Find(id)?.Title ?? Rts.Game.Ui.DisplayNames.Mission(id));

    private void Rebuild() => _entries = Catalog.InCategory(_tab).ToArray();
}
