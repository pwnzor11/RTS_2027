// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game;

/// <summary>
/// Player settings, stored as <c>%APPDATA%/RTS_2027/settings.toml</c> (review C-9). Versioned; loading is tolerant:
/// a missing, unreadable or malformed file gives the defaults, an unknown key is ignored, a wrong-typed or
/// out-of-range value falls back to (or is clamped toward) its default. A file with a newer <c>version</c> than this
/// build knows gives the defaults rather than guessing.
/// </summary>
/// <param name="Version">File format version (1).</param>
/// <param name="Fullscreen">Borderless full screen.</param>
/// <param name="WindowWidth">Window width in pixels (320..16384).</param>
/// <param name="WindowHeight">Window height in pixels (240..16384).</param>
/// <param name="UiScale">Integer UI scale 1..8, or 0 for automatic from the window height.</param>
/// <param name="MusicVolume">0..100.</param>
/// <param name="SfxVolume">0..100.</param>
/// <param name="MasterVolume">0..100, scales sound and music together (M3-16). Files without the key get 100.</param>
public sealed record Settings(
    int Version = Settings.CurrentVersion,
    bool Fullscreen = false,
    int WindowWidth = 1280,
    int WindowHeight = 720,
    int UiScale = 0,
    int MusicVolume = 70,
    int SfxVolume = 100,
    int MasterVolume = 100)
{
    /// <summary>The file format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The default settings.</summary>
    public static Settings Default { get; } = new();

    /// <summary>The settings file path: the user's application-data folder, then RTS_2027/settings.toml.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RTS_2027", "settings.toml");

    /// <summary>The UI scale to draw with: <see cref="UiScale"/>, or when it is 0 a step chosen from the window height.</summary>
    public int EffectiveUiScale(int windowHeight) =>
        UiScale > 0 ? UiScale : windowHeight < 1000 ? 2 : windowHeight < 1500 ? 3 : 4;

    /// <summary>Loads <paramref name="path"/>; never throws. Missing or bad file gives <see cref="Default"/>.</summary>
    public static Settings Load(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
            return File.Exists(file) ? Parse(File.ReadAllText(file)) : Default;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Default;
        }
    }

    /// <summary>Parses settings text; never throws. Bad text gives <see cref="Default"/>.</summary>
    public static Settings Parse(string text)
    {
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        }
        catch (Exception)
        {
            return Default;
        }

        int version = Int(root, "version", 1, 0, int.MaxValue);
        if (version > CurrentVersion) return Default;

        Settings d = Default;
        return new Settings(
            CurrentVersion,
            root.TryGetValue("fullscreen", out object? fs) && fs is bool b ? b : d.Fullscreen,
            Int(root, "window_width", d.WindowWidth, 320, 16384),
            Int(root, "window_height", d.WindowHeight, 240, 16384),
            Int(root, "ui_scale", d.UiScale, 0, 8),
            Int(root, "music_volume", d.MusicVolume, 0, 100),
            Int(root, "sfx_volume", d.SfxVolume, 0, 100),
            Int(root, "master_volume", d.MasterVolume, 0, 100));
    }

    /// <summary>The settings as TOML text.</summary>
    public string ToToml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# RTS_2027 settings. Delete this file to restore the defaults.");
        sb.AppendLine(FormattableString.Invariant($"version = {CurrentVersion}"));
        sb.AppendLine(FormattableString.Invariant($"fullscreen = {(Fullscreen ? "true" : "false")}"));
        sb.AppendLine(FormattableString.Invariant($"window_width = {WindowWidth}"));
        sb.AppendLine(FormattableString.Invariant($"window_height = {WindowHeight}"));
        sb.AppendLine("# 0 = automatic from the window height");
        sb.AppendLine(FormattableString.Invariant($"ui_scale = {UiScale}"));
        sb.AppendLine(FormattableString.Invariant($"music_volume = {MusicVolume}"));
        sb.AppendLine(FormattableString.Invariant($"sfx_volume = {SfxVolume}"));
        sb.AppendLine(FormattableString.Invariant($"master_volume = {MasterVolume}"));
        return sb.ToString();
    }

    /// <summary>Writes the settings file (folders are created). Returns false instead of throwing.</summary>
    public bool Save(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
            string? dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(file, ToToml());
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // A TOML integer in range wins; a wrong type gives the fallback; an out-of-range integer is clamped.
    private static int Int(TomlTable table, string key, int fallback, int min, int max) =>
        table.TryGetValue(key, out object? value) && value is long l ? (int)Math.Clamp(l, min, max) : fallback;
}
