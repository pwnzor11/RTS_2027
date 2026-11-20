// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Reflection;
using System.Text.RegularExpressions;
using Rts.Game.Archive;
using Rts.Game.Tests.Archive;
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Game.Veterancy;

namespace Rts.Game.Tests.M11_31;

/// <summary>M11-31: no raw id, dotted key or "[placeholder]" may reach the screen through the keyed UI texts or the Archive.</summary>
public sealed partial class PlayerTextTests
{
    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z0-9_]+)+$")]
    private static partial Regex DottedKey();

    private static bool Looks(string text, string key) =>
        text == key || text.Contains('_') || DottedKey().IsMatch(text.Trim()) || (text.Contains('[') && text.Contains(']'));

    private static IEnumerable<(string Key, string Text)> Keyed(Type type, Func<string, string> resolve) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.EndsWith("Key", StringComparison.Ordinal) == (type != typeof(UiText)))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(k => DottedKey().IsMatch(k))
            .Select(k => (k, resolve(k)));

    /// <summary>Every UiText key resolves to words (not to itself) and the words hold no underscore, key shape or bracket text.</summary>
    [Fact]
    public void No_UiText_value_looks_like_a_key_or_an_id()
    {
        var all = Keyed(typeof(UiText), UiText.Get).ToList();
        Assert.True(all.Count > 30, "expected the supply, save, alert and setup keys");
        Assert.All(all, kv => Assert.False(Looks(kv.Text, kv.Key), $"{kv.Key} shows '{kv.Text}'"));
    }

    /// <summary>The Left Behind and veterancy key tables resolve to words too.</summary>
    [Fact]
    public void No_faction_text_switch_returns_its_own_key()
    {
        var lb = Keyed(typeof(LeftBehindText), LeftBehindText.Placeholder).ToList();
        var vet = Keyed(typeof(VeterancyText), k => VeterancyText.Placeholder(k, 10, 20, 1)).ToList();
        Assert.NotEmpty(lb);
        Assert.NotEmpty(vet);
        Assert.All(lb.Concat(vet), kv => Assert.False(Looks(kv.Text, kv.Key), $"{kv.Key} shows '{kv.Text}'"));
    }

    /// <summary>Archive titles, subtitles and field notes are names: no underscore, no dotted key, no bracket placeholder; bodies are real prose.</summary>
    [Fact]
    public void No_Archive_title_or_body_is_a_key_an_id_or_a_placeholder()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();
        foreach (ArchiveEntry e in catalog.Entries)
        {
            Assert.False(Looks(e.Title, e.Id), $"{e.Id}: title '{e.Title}'");
            Assert.False(Looks(e.Subtitle, e.Id), $"{e.Id}: subtitle '{e.Subtitle}'");
            Assert.False(e.Body.TrimStart().StartsWith('['), $"{e.Id}: body is a placeholder");
            Assert.True(e.Body.Length >= 60, $"{e.Id}: body is too short to be a real entry");
        }
    }

    /// <summary>The shared name helper turns every rules id of the shipped data into an Archive title or a tidy name, never the id.</summary>
    [Fact]
    public void Every_rules_id_displays_without_an_underscore()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();
        var rules = ArchiveTestData.Rules();
        IEnumerable<string> ids = rules.Vehicles.Select(v => v.Id).Concat(rules.Infantry.Select(i => i.Id))
            .Concat(rules.Buildings.Select(b => b.Id)).Concat(rules.Upgrades.Select(u => u.Id)).Concat(rules.Weapons.Select(w => w.Id));
        foreach (string id in ids)
        {
            Assert.DoesNotContain('_', CameoNames.Display(id, i => catalog.Find(i)?.Title));
            Assert.DoesNotContain('_', CameoNames.Display(id, null));
        }
    }
}
