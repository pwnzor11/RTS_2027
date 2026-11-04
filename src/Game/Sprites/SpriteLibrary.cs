// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Sprites;

/// <summary>
/// Every <c>*.sprite.toml</c> under a data folder, keyed by id (the file stem). <see cref="Scan"/> reads only the
/// manifests, so the id index can be checked without a graphics device. <see cref="LoadAll"/> then builds the
/// sheets at startup. Buildings and units will look their sprite up by their rules id.
/// </summary>
public sealed class SpriteLibrary : IDisposable
{
    private readonly SortedDictionary<string, SpriteManifest> _manifests;
    private readonly Dictionary<string, SpriteSheet> _sheets = new(StringComparer.Ordinal);
    private bool _disposed;

    private SpriteLibrary(SortedDictionary<string, SpriteManifest> manifests)
    {
        _manifests = manifests;
    }

    /// <summary>Ids of every sprite, in ordinal order.</summary>
    public IReadOnlyCollection<string> Ids => _manifests.Keys;

    /// <summary>
    /// Reads every <c>*.sprite.toml</c> under <paramref name="root"/> (recursively), in path order. No textures are
    /// loaded.
    /// </summary>
    /// <param name="root">Folder to search, normally <c>data/sprites</c>.</param>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    /// <exception cref="InvalidDataException">A manifest is invalid, or two manifests share an id.</exception>
    public static SpriteLibrary Scan(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"sprite folder not found: {root}");

        var manifests = new SortedDictionary<string, SpriteManifest>(StringComparer.Ordinal);
        var paths = Directory.EnumerateFiles(root, "*.sprite.toml", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal);
        foreach (string path in paths)
        {
            SpriteManifest manifest = SpriteManifest.Load(path);
            if (manifests.TryGetValue(manifest.Id, out SpriteManifest? existing))
            {
                throw new InvalidDataException(
                    $"sprite id '{manifest.Id}' is defined twice: {existing.SourcePath} and {path}");
            }
            manifests.Add(manifest.Id, manifest);
        }
        CheckCompanions(manifests);
        return new SpriteLibrary(manifests);
    }

    // A turret or shadow sheet is only meaningful beside its body sheet, so it must be named <base>_<layer> and <base> must exist.
    private static void CheckCompanions(SortedDictionary<string, SpriteManifest> manifests)
    {
        foreach (SpriteManifest manifest in manifests.Values)
        {
            if (manifest.Layer == "body") continue;
            string suffix = "_" + manifest.Layer;
            if (!manifest.Id.EndsWith(suffix, StringComparison.Ordinal) || manifest.Id.Length == suffix.Length)
            {
                throw new InvalidDataException(
                    $"{manifest.SourcePath}: a {manifest.Layer} sheet must be named <base>{suffix}");
            }
            string baseId = manifest.Id[..^suffix.Length];
            if (!manifests.ContainsKey(baseId))
            {
                throw new InvalidDataException(
                    $"{manifest.SourcePath}: no body sprite '{baseId}' for this {manifest.Layer} sheet");
            }
        }
    }

    /// <summary>
    /// The companion sheet of a body sprite: the manifest named <c>&lt;id&gt;_&lt;layer&gt;</c> when it exists and has that layer,
    /// otherwise null.
    /// </summary>
    /// <param name="id">Body sprite id.</param>
    /// <param name="layer">Companion layer, <c>turret</c> or <c>shadow</c>.</param>
    public SpriteManifest? Companion(string id, string layer)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(layer);
        return _manifests.TryGetValue(id + "_" + layer, out SpriteManifest? manifest) && manifest.Layer == layer ? manifest : null;
    }

    /// <summary>The manifest for an id.</summary>
    /// <exception cref="KeyNotFoundException">No sprite has that id. The message lists the known ids.</exception>
    public SpriteManifest Manifest(string id)
    {
        if (_manifests.TryGetValue(id, out SpriteManifest? manifest)) return manifest;
        throw new KeyNotFoundException($"no sprite '{id}' (known: {string.Join(", ", _manifests.Keys)})");
    }

    /// <summary>Loads the GPU sheet of every sprite. Calling it again does nothing.</summary>
    /// <param name="device">Graphics device that owns the textures.</param>
    public void LoadAll(GraphicsDevice device)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (SpriteManifest manifest in _manifests.Values)
        {
            if (!_sheets.ContainsKey(manifest.Id))
            {
                _sheets.Add(manifest.Id, SpriteSheet.Load(device, manifest.SourcePath));
            }
        }
    }

    /// <summary>The loaded sheet for an id. <see cref="LoadAll"/> must have run first.</summary>
    /// <exception cref="KeyNotFoundException">No sprite has that id.</exception>
    /// <exception cref="InvalidOperationException">The sheets have not been loaded yet.</exception>
    public SpriteSheet Sheet(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = Manifest(id);
        if (_sheets.TryGetValue(id, out SpriteSheet? sheet)) return sheet;
        throw new InvalidOperationException($"sprite '{id}' is not loaded; call LoadAll first");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (SpriteSheet sheet in _sheets.Values) sheet.Dispose();
        _sheets.Clear();
    }
}
