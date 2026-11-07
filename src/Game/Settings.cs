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
/// <param name="Version">File format version (2; M5-08 added the llm_* keys, older files get their defaults).</param>
/// <param name="Fullscreen">Borderless full screen.</param>
/// <param name="WindowWidth">Window width in pixels (320..16384).</param>
/// <param name="WindowHeight">Window height in pixels (240..16384).</param>
/// <param name="UiScale">Integer UI scale 1..8, or 0 for automatic from the window height.</param>
/// <param name="MusicVolume">0..100.</param>
/// <param name="SfxVolume">0..100.</param>
/// <param name="MasterVolume">0..100, scales sound and music together (M3-16). Files without the key get 100.</param>
/// <param name="LlmProvider">AI commander backend: none, anthropic or openai (an OpenAI-compatible endpoint). Default none.</param>
/// <param name="LlmBaseUrl">Endpoint base URL; empty = the provider's default (data/llm/providers.toml).</param>
/// <param name="LlmModel">Model id; empty = the provider's default.</param>
/// <param name="LlmChatOn">Whether the commander chat panel is on (default true).</param>
/// <param name="LlmCallCap">Most LLM calls per game, 0..1000; 0 = no cap (default 120). The API key is never a setting.</param>
/// <param name="EdgeScroll">Mouse at the window edge pans the camera (M11-02). Files without the key get true.</param>
/// <param name="ScrollSpeed">Camera scroll speed in screen pixels per second, 200..3000 (M11-02). Files without the key get 900.</param>
/// <param name="ShowDiagnostics">Show the tick / state hash / fps line in play (M11-03). Default false; files without the key get false.</param>
public sealed record Settings(
    int Version = Settings.CurrentVersion,
    bool Fullscreen = false,
    int WindowWidth = 1280,
    int WindowHeight = 720,
    int UiScale = 0,
    int MusicVolume = 70,
    int SfxVolume = 100,
    int MasterVolume = 100,
    string LlmProvider = "none",
    string LlmBaseUrl = "",
    string LlmModel = "",
    bool LlmChatOn = true,
    int LlmCallCap = 120,
    bool EdgeScroll = true,
    int ScrollSpeed = Settings.DefaultScrollSpeed,
    bool ShowDiagnostics = false)
{
    /// <summary>The file format version this build reads and writes.</summary>
    public const int CurrentVersion = 2;

    /// <summary>Default camera scroll speed in screen pixels per second.</summary>
    public const int DefaultScrollSpeed = 900;

    /// <summary>Lowest camera scroll speed.</summary>
    public const int MinScrollSpeed = 200;

    /// <summary>Highest camera scroll speed.</summary>
    public const int MaxScrollSpeed = 3000;

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
            Int(root, "master_volume", d.MasterVolume, 0, 100),
            Provider(root.TryGetValue("llm_provider", out object? lp) && lp is string ps ? ps : d.LlmProvider),
            Text(root, "llm_base_url", d.LlmBaseUrl),
            Text(root, "llm_model", d.LlmModel),
            root.TryGetValue("llm_chat_on", out object? lc) && lc is bool cb ? cb : d.LlmChatOn,
            Int(root, "llm_call_cap", d.LlmCallCap, 0, 1000),
            root.TryGetValue("edge_scroll", out object? es) && es is bool esb ? esb : d.EdgeScroll,
            Int(root, "scroll_speed", d.ScrollSpeed, MinScrollSpeed, MaxScrollSpeed),
            root.TryGetValue("show_diagnostics", out object? sd) && sd is bool sdb ? sdb : d.ShowDiagnostics);
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
        sb.AppendLine("# AI commander (LLM). The API key is NOT stored here; it is kept encrypted in llm.key.");
        sb.AppendLine("# llm_provider: none | anthropic | openai. Empty url / model = the provider default. llm_call_cap 0 = no cap.");
        sb.AppendLine(FormattableString.Invariant($"llm_provider = {Quote(Provider(LlmProvider))}"));
        sb.AppendLine(FormattableString.Invariant($"llm_base_url = {Quote(LlmBaseUrl)}"));
        sb.AppendLine(FormattableString.Invariant($"llm_model = {Quote(LlmModel)}"));
        sb.AppendLine(FormattableString.Invariant($"llm_chat_on = {(LlmChatOn ? "true" : "false")}"));
        sb.AppendLine(FormattableString.Invariant($"llm_call_cap = {Math.Clamp(LlmCallCap, 0, 1000)}"));
        sb.AppendLine("# Camera: mouse at the window edge scrolls; speed in screen pixels per second (Shift = 2x).");
        sb.AppendLine(FormattableString.Invariant($"edge_scroll = {(EdgeScroll ? "true" : "false")}"));
        sb.AppendLine(FormattableString.Invariant($"scroll_speed = {Math.Clamp(ScrollSpeed, MinScrollSpeed, MaxScrollSpeed)}"));
        sb.AppendLine("# Show the tick / hash / fps line while playing.");
        sb.AppendLine(FormattableString.Invariant($"show_diagnostics = {(ShowDiagnostics ? "true" : "false")}"));
        return sb.ToString();
    }

    /// <summary>The provider as a <see cref="Rts.Llm.LlmProviderKind"/> (unknown text = none).</summary>
    public Rts.Llm.LlmProviderKind ProviderKind => Provider(LlmProvider) switch
    {
        "anthropic" => Rts.Llm.LlmProviderKind.Anthropic,
        "openai" => Rts.Llm.LlmProviderKind.OpenAiCompatible,
        _ => Rts.Llm.LlmProviderKind.None,
    };

    /// <summary>The settings text for a provider kind.</summary>
    public static string ProviderName(Rts.Llm.LlmProviderKind kind) => kind switch
    {
        Rts.Llm.LlmProviderKind.Anthropic => "anthropic",
        Rts.Llm.LlmProviderKind.OpenAiCompatible => "openai",
        _ => "none",
    };

    private static string Provider(string text)
    {
        string t = text.Trim().ToLowerInvariant();
        return t is "anthropic" or "openai" ? t : "none";
    }

    // Free text kept short and single-line so a hand-edited file cannot bloat or break the settings.
    private static string Text(TomlTable table, string key, string fallback) =>
        table.TryGetValue(key, out object? v) && v is string s ? Clean(s) : fallback;

    private static string Clean(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            if (!char.IsControl(c)) sb.Append(c);
        string t = sb.ToString().Trim();
        return t.Length > 200 ? t[..200] : t;
    }

    private static string Quote(string s) => "\"" + Clean(s).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

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
