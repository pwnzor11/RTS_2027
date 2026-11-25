// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;

namespace Rts.Game.Tests.S45Sys;

/// <summary>U-14 (GAMESYS-05): the menu's poll lists saves cheaply; headers are read again only when a save folder changes.</summary>
public sealed class SaveCatalogCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rts_s45sc_" + Guid.NewGuid().ToString("N"));
    private int _reads;

    public SaveCatalogCacheTests() => Directory.CreateDirectory(Path.Combine(_root, "campaign"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private SaveCatalog Catalog() => new(_root, file =>
    {
        _reads++;
        return ("save " + Path.GetFileNameWithoutExtension(file), true);
    });

    private string Save(string name)
    {
        string file = Path.Combine(_root, "campaign", name + SaveCatalog.Extension);
        File.WriteAllText(file, "x");
        return file;
    }

    [Fact]
    public void Polling_an_unchanged_folder_does_not_read_the_headers_again()
    {
        Save("a");
        Save("b");
        SaveCatalog catalog = Catalog();

        IReadOnlyList<SaveEntry> first = catalog.List(SaveKind.Campaign);
        Assert.Equal(2, _reads);

        for (int i = 0; i < 20; i++)   // twenty polls, as the menu does in forty seconds
        {
            IReadOnlyList<SaveEntry> again = catalog.List(SaveKind.Campaign);
            Assert.Equal(first.Select(e => e.Name), again.Select(e => e.Name));
            Assert.NotNull(catalog.Newest());
        }

        Assert.Equal(2, _reads);
    }

    [Fact]
    public void A_changed_file_or_a_new_file_is_read_again()
    {
        string a = Save("a");
        SaveCatalog catalog = Catalog();
        catalog.List(SaveKind.Campaign);
        Assert.Equal(1, _reads);

        File.SetLastWriteTimeUtc(a, DateTime.UtcNow.AddMinutes(5));
        catalog.List(SaveKind.Campaign);
        Assert.Equal(2, _reads);

        Save("b");
        catalog.List(SaveKind.Campaign);
        Assert.Equal(4, _reads);

        catalog.List(SaveKind.Campaign);
        Assert.Equal(4, _reads);
    }

    [Fact]
    public void A_removed_file_leaves_the_list_without_a_read_error()
    {
        Save("a");
        string b = Save("b");
        SaveCatalog catalog = Catalog();
        Assert.Equal(2, catalog.List(SaveKind.Campaign).Count);

        File.Delete(b);

        Assert.Equal(new[] { "a" }, catalog.List(SaveKind.Campaign).Select(e => e.Name));
    }

    [Fact]
    public void A_missing_folder_is_an_empty_list()
    {
        var catalog = new SaveCatalog(Path.Combine(_root, "nowhere"), _ => ("", true));
        Assert.Empty(catalog.List(SaveKind.Skirmish));
        Assert.Null(catalog.Newest());
    }
}
