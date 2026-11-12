// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Render;
using Rts.Game.Sprites;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEREND-02 (sprite loading is per sheet) and GAMEREND-06 (rules ids without art are an explicit, shrinking list).</summary>
public sealed class RenderSpriteLoadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rts_s18_sprites_" + Guid.NewGuid().ToString("N"));

    public RenderSpriteLoadTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (IOException) { }
    }

    private static string Manifest(int frame, string extra = "") => string.Join("\n",
        "file = \"sheet.png\"", $"frame_w = {frame}", $"frame_h = {frame}", "origin_x = 0", "origin_y = 0", extra, "", "[[state]]", "name = \"idle\"", "facings = 1");

    private void Write(string relative, string text)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void One_broken_manifest_is_skipped_and_logged_and_the_rest_load()
    {
        Write("vehicles/good_a.sprite.toml", Manifest(32));
        Write("vehicles/broken.sprite.toml", "this is = = not toml");
        Write("buildings/good_b.sprite.toml", Manifest(64));
        Write("buildings/orphan_turret.sprite.toml", Manifest(64, "layer = \"turret\""));
        var log = new List<string>();

        using SpriteLibrary library = SpriteLibrary.ScanTolerant(_root, log.Add);

        Assert.Equal(new[] { "good_a", "good_b" }, library.Ids);
        Assert.Contains(log, l => l.Contains("broken.sprite.toml", StringComparison.Ordinal));
        Assert.Contains(log, l => l.Contains("orphan_turret", StringComparison.Ordinal));
        // The strict scan still refuses the folder, as before.
        Assert.Throws<InvalidDataException>(() => SpriteLibrary.Scan(_root));
    }

    [Fact]
    public void A_sheet_that_fails_to_load_is_dropped_and_does_not_stop_the_others()
    {
        Write("a.sprite.toml", Manifest(32));
        Write("b.sprite.toml", Manifest(32));
        var log = new List<string>();
        var tried = new List<string>();
        using SpriteLibrary library = SpriteLibrary.ScanTolerant(_root, log.Add);

        library.LoadAll(m =>
        {
            tried.Add(m.Id);
            throw new InvalidDataException("bad png");
        }, log.Add);

        Assert.Equal(new[] { "a", "b" }, tried);
        Assert.Empty(library.Ids);
        Assert.Equal(2, log.Count(l => l.Contains("bad png", StringComparison.Ordinal)));
        // Without a log the first failure still throws, as before.
        using SpriteLibrary strict = SpriteLibrary.Scan(_root);
        Assert.Throws<InvalidDataException>(() => strict.LoadAll(_ => throw new InvalidDataException("x"), null));
    }

    // GAMEREND-06: rules ids that have no sprite file and so draw as placeholders (designed fallback). New gaps must fail this test;
    // when art arrives, remove the id from the list.
    private static readonly string[] KnownWithoutArt = System.Array.Empty<string>();   // M14-06: the last 26 ids got art (owner's laptop render)

    [Fact]
    public void The_rules_ids_without_a_sprite_are_exactly_the_known_list()
    {
        string rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
        var sim = RulesLoader.Load(rulesDir).Sim;
        using SpriteLibrary library = SpriteLibrary.Scan(Path.Combine(Path.GetDirectoryName(rulesDir)!, "sprites"));
        var have = new HashSet<string>(library.Ids, StringComparer.Ordinal);
        string Sprite(string id) => SpriteResolver.VehicleAliases.TryGetValue(id, out string? alias) ? alias : id;

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var v in sim.Vehicles) if (!have.Contains(Sprite(v.Id))) missing.Add(v.Id);
        foreach (var i in sim.Infantry) if (!have.Contains(i.Id)) missing.Add(i.Id);
        foreach (var b in sim.Buildings) if (!have.Contains(b.Id)) missing.Add(b.Id);

        Assert.Equal(KnownWithoutArt.OrderBy(x => x, StringComparer.Ordinal).ToArray(), missing.ToArray());
    }
}
