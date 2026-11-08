// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Sprites;

/// <summary>
/// The <c>RTS_FX=classic</c> comparison switch: the procedural effect sheets that shipped before the S15 art import live in
/// <c>data/sprites_classic/fx</c> (beside, not inside, <c>data/sprites</c>, so ids never clash). With the switch on,
/// <see cref="SpriteLibrary.Scan"/> lets those manifests replace the same-named effects. View only; nothing in the sim reads it.
/// </summary>
public static class ClassicFx
{
    /// <summary>True when the <c>RTS_FX</c> environment variable is <c>classic</c>.</summary>
    public static bool Requested =>
        string.Equals(Environment.GetEnvironmentVariable("RTS_FX"), "classic", StringComparison.OrdinalIgnoreCase);

    /// <summary>Folder of the classic sheets for a sprite root (<c>data/sprites</c>): <c>data/sprites_classic/fx</c>.</summary>
    public static string FolderFor(string spriteRoot) =>
        Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(spriteRoot))) ?? ".", "sprites_classic", "fx");

    /// <summary>Replaces manifests by the classic ones of the same id when <paramref name="enabled"/> and the folder exists.</summary>
    /// <param name="spriteRoot">The sprite root that was scanned.</param>
    /// <param name="manifests">The scanned manifests, changed in place.</param>
    /// <param name="enabled">Normally <see cref="Requested"/>.</param>
    public static void Apply(string spriteRoot, IDictionary<string, SpriteManifest> manifests, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(spriteRoot);
        ArgumentNullException.ThrowIfNull(manifests);
        string dir = FolderFor(spriteRoot);
        if (!enabled || !Directory.Exists(dir)) return;
        foreach (string path in Directory.EnumerateFiles(dir, "*.sprite.toml").OrderBy(p => p, StringComparer.Ordinal))
        {
            SpriteManifest m = SpriteManifest.Load(path);
            if (manifests.ContainsKey(m.Id)) manifests[m.Id] = m;
        }
    }
}
