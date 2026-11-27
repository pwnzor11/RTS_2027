// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Rts.Game.Controls;
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
/// <param name="UiScalePercent">The UI scale in percent: 75, 100, 125, 150, 175 or 200 (M11-10h; default 100). It multiplies the automatic scale from the window height. The old integer <c>ui_scale</c> key is ignored.</param>
/// <param name="MusicVolume">0..100.</param>
/// <param name="SfxVolume">0..100.</param>
/// <param name="MasterVolume">0..100, scales sound and music together (M3-16). Files without the key get 100.</param>
/// <param name="AmbienceVolume">0..100, the looping biome ambience (S22). Files without the key get 100 (the ambience loops are mixed quietly in sounds.toml).</param>
/// <param name="LlmProvider">AI commander backend: none, anthropic or openai (an OpenAI-compatible endpoint). Default none.</param>
/// <param name="LlmBaseUrl">Endpoint base URL; empty = the provider's default (data/llm/providers.toml).</param>
/// <param name="LlmModel">Model id; empty = the provider's default.</param>
/// <param name="LlmChatOn">Whether the commander chat panel is on (default true).</param>
/// <param name="LlmCallCap">Most LLM calls per game, 0..1000; 0 = no cap (default 120). The API key is never a setting.</param>
/// <param name="EdgeScroll">Mouse at the window edge pans the camera (M11-02). Files without the key get true.</param>
/// <param name="ScrollSpeed">Camera scroll speed in screen pixels per second, 200..3000 (M11-02). Files without the key get 900.</param>
/// <param name="TextSize">The Text size setting, scaling fonts only (M11-10h). Files without the key get Normal.</param>
/// <param name="KeyMap">Rebound keys (D-172 (6)); null = the default bindings. Stored in a <c>[keys]</c> table of action id = key name; missing or unknown entries are ignored.</param>
/// <param name="ShowDiagnostics">Show the tick / state hash / fps line in play (M11-03). Default false; files without the key get false.</param>
/// <param name="CameraCueOnNewObjective">Pan the camera to a new mission objective's target (M11-05; default true).</param>
/// <param name="AutosaveMinutes">D-205: single-player autosave every this many minutes of play (plus one at the start of a match), into 3 rotating slots; 0 = off. Default 5; files without the key get 5.</param>
/// <param name="GameSpeedPercent">S19 D-205 (M13-15): single-player skirmish game speed in percent of normal: 50, 75, 100, 150 or 200. Default 100; files without the key get 100.</param>
/// <param name="LargerInfantry">D-184 pilot: draw infantry (sprites, selection brackets, health bars) 1.25 times larger than today. Render only. Default true for the pilot screenshots; files without the key get true.</param>
/// <param name="Weather">M10 design 21 §4: view-only weather particles per biome. Default true; files without the key get true.</param>
/// <param name="ScreenShake">M10 design 21 §4: light screen shake on big blasts. Default true.</param>
/// <param name="CheckForUpdates">M10 design 21 §5: check GitHub Releases once at start and show "new version available". Default false (A43 GAMESYS-02, D-312: no network call until the player turns it on or presses "Check now"); files that store a value keep it.</param>
/// <param name="ShowOrderLines">QOL-10: thin lines from each selected unit to its attack target (red) or move destination (green). Default true.</param>
/// <param name="UiSkin">S24: "faction" (default) draws the sidebar and card with the local faction's skin art where it exists; "classic" always draws the procedural look.</param>
/// <param name="CloudShadows">M16-V-04: slow soft cloud shadows drifting over the map (view only). Default true; files without the key get true.</param>
/// <param name="WaterShimmer">M16-V-04: a slow band of sparkles sweeping over visible water (view only). Default true; files without the key get true.</param>
/// <param name="RoadPieces">M16-V-04: junction flares and rounded dead ends drawn over roads (view only). Default true; files without the key get true.</param>
/// <param name="ReduceFlashes">QOL-20 (D-313): no white hit flash, promotion flash or muzzle sparks (view only; separate from <c>ScreenShake</c>). Default false.</param>
/// <param name="TutorialOffered">QOL-13 (D-313): the first-run "play the tutorial?" offer has been shown (and answered); it is never shown again. Default false.</param>
/// <param name="AlertVolumes">QOL-12 (D-313): volume (0 = mute) of each alert category; null = every category at 100. Stored in an <c>[alerts]</c> table.</param>
/// <param name="TreadMarks">S45 V-04 (design 18): tracked and wheeled vehicles leave fading track marks on soft ground (view only). Default true; files without the key get true.</param>
/// <param name="DebrisChunks">S45 V-04 (design 18): vehicle and building deaths throw a few small debris chunks (view only). Default true; files without the key get true.</param>
/// <param name="EffectBudget">S45 V-04 (design 18): one budget caps explosions, debris and smoke; when it is full the far-off-screen and small ones are dropped first, never a superweapon or the death of a selected unit (view only). Default true; files without the key get true.</param>
/// <param name="AdditiveGlow">S45 V-04 (design 18): a soft additive glow under explosion cores, muzzle flashes and fires (view only; flashes also obey <c>ReduceFlashes</c>). Default true; files without the key get true.</param>
/// <param name="PlayerName">M9 LAN player name; empty = the Windows user name (design 20 §8 item 4).</param>
/// <param name="LanGamePort">M9 LAN game port (UDP), 1024..65535. Default <see cref="Rts.Net.NetProtocol.DefaultGamePort"/>.</param>
/// <param name="LanDiscoveryPort">M9 LAN discovery port (UDP), 1024..65535. Default <see cref="Rts.Net.NetProtocol.DefaultDiscoveryPort"/>.</param>
public sealed record Settings(
    int Version = Settings.CurrentVersion,
    bool Fullscreen = false,
    int WindowWidth = 1280,
    int WindowHeight = 720,
    int UiScalePercent = Ui.UiScale.DefaultPercent,
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
    bool ShowDiagnostics = false,
    bool HealthBarsAlways = false,   // false = "Damaged or selected" (default), true = "Always"
    bool CameraCueOnNewObjective = true,
    Ui.TextSize TextSize = Ui.TextSize.Normal,
    KeyBindings? KeyMap = null,
    bool LargerInfantry = true,
    int AutosaveMinutes = Settings.DefaultAutosaveMinutes,
    int GameSpeedPercent = Rts.Game.Host.GameSpeed.Normal,
    bool Weather = true,
    bool ScreenShake = true,
    bool CheckForUpdates = false,
    string PlayerName = "",
    int LanGamePort = Rts.Net.NetProtocol.DefaultGamePort,
    int LanDiscoveryPort = Rts.Net.NetProtocol.DefaultDiscoveryPort,
    int AmbienceVolume = 100,
    string UiSkin = Settings.UiSkinFaction,
    bool ReduceFlashes = false,
    bool TutorialOffered = false,
    Ui.Alerts.AlertMix? AlertVolumes = null,
    bool ShowOrderLines = true,
    bool CloudShadows = true,
    bool WaterShimmer = true,
    bool RoadPieces = true,
    bool TreadMarks = true,
    bool DebrisChunks = true,
    bool EffectBudget = true,
    bool AdditiveGlow = true,
    bool SidebarFollowsSelection = true,
    bool BoxSelectFightersFirst = true,
    bool MuteInBackground = false,
    bool VSync = true)
{
    /// <summary>The <c>UiSkin</c> value for faction skins.</summary>
    public const string UiSkinFaction = "faction";

    /// <summary>The <c>UiSkin</c> value for the procedural look.</summary>
    public const string UiSkinClassic = "classic";

    /// <summary>True when faction skins are on.</summary>
    public bool UseFactionSkin => !string.Equals(UiSkin, UiSkinClassic, StringComparison.Ordinal);

    /// <summary>The file format version this build reads and writes.</summary>
    public const int CurrentVersion = 2;

    /// <summary>Default autosave interval in minutes (D-205).</summary>
    public const int DefaultAutosaveMinutes = 5;

    /// <summary>Longest autosave interval in minutes.</summary>
    public const int MaxAutosaveMinutes = 60;

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

    /// <summary>The one UI scale rule for a window: automatic from its height times <see cref="UiScalePercent"/>, clamped to what fits (see <see cref="Ui.UiScale.Resolve"/>).</summary>
    public Ui.UiScaleResult ResolveUiScale(int windowWidth, int windowHeight) => Ui.UiScale.Resolve(windowWidth, windowHeight, UiScalePercent);

    /// <summary>Loads <paramref name="path"/>; never throws. Missing or bad file gives <see cref="Default"/>.</summary>
    public static Settings Load(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
            if (!File.Exists(file)) return Default;
            string text = File.ReadAllText(file);
            Settings parsed = Parse(text, out bool unreadable);
            if (unreadable) KeepAsBad(file);   // S18 GAMEREND-12: the next Save must not destroy a file this build could not read
            return parsed;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Default;
        }
    }

    // Moves an unreadable settings file aside as <file>.bad (replacing an older .bad); never throws.
    private static void KeepAsBad(string file)
    {
        try
        {
            File.Move(file, file + ".bad", overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left where it is: the next Save will replace it, as before.
        }
    }

    /// <summary>Parses settings text; never throws. Bad text gives <see cref="Default"/>.</summary>
    public static Settings Parse(string text) => Parse(text, out _);

    private static Settings Parse(string text, out bool unreadable)
    {
        unreadable = false;
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        }
        catch (Exception)
        {
            unreadable = true;
            return Default;
        }

        int version = Int(root, "version", 1, 0, int.MaxValue);
        if (version > CurrentVersion)
        {
            unreadable = true;   // written by a newer build
            return Default;
        }

        Settings d = Default;
        return new Settings(
            CurrentVersion,
            root.TryGetValue("fullscreen", out object? fs) && fs is bool b ? b : d.Fullscreen,
            Int(root, "window_width", d.WindowWidth, 320, 16384),
            Int(root, "window_height", d.WindowHeight, 240, 16384),
            Ui.UiScale.SnapPercent(Int(root, "ui_scale_percent", d.UiScalePercent, Ui.UiScale.Percents[0], Ui.UiScale.Percents[^1])),
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
            root.TryGetValue("show_diagnostics", out object? sd) && sd is bool sdb ? sdb : d.ShowDiagnostics,
            root.TryGetValue("health_bars_always", out object? hb) && hb is bool hbb ? hbb : d.HealthBarsAlways,
            root.TryGetValue("camera_cue_on_new_objective", out object? cq) && cq is bool cqb ? cqb : d.CameraCueOnNewObjective,
            (Ui.TextSize)Int(root, "text_size", (int)d.TextSize, (int)Ui.TextSize.Small, (int)Ui.TextSize.ExtraLarge),
            ParseKeys(root),
            root.TryGetValue("larger_infantry", out object? li) && li is bool lib ? lib : d.LargerInfantry,
            Int(root, "autosave_minutes", d.AutosaveMinutes, 0, MaxAutosaveMinutes),
            Rts.Game.Host.GameSpeed.Snap(Int(root, "game_speed_percent", d.GameSpeedPercent, 50, 200)),
            root.TryGetValue("weather", out object? we) && we is bool web ? web : d.Weather,
            root.TryGetValue("screen_shake", out object? sh) && sh is bool shb ? shb : d.ScreenShake,
            root.TryGetValue("check_for_updates", out object? cu) && cu is bool cub ? cub : d.CheckForUpdates,
            Text(root, "player_name", d.PlayerName),
            Int(root, "lan_game_port", d.LanGamePort, 1024, 65535),
            Int(root, "lan_discovery_port", d.LanDiscoveryPort, 1024, 65535),
            Int(root, "ambience_volume", d.AmbienceVolume, 0, 100),
            root.TryGetValue("ui_skin", out object? us) && us is string uss && uss.Trim().ToLowerInvariant() == UiSkinClassic ? UiSkinClassic : UiSkinFaction,
            root.TryGetValue("reduce_flashes", out object? rf) && rf is bool rfb ? rfb : d.ReduceFlashes,
            root.TryGetValue("tutorial_offered", out object? to) && to is bool tob ? tob : d.TutorialOffered,
            ParseAlerts(root),
            root.TryGetValue("show_order_lines", out object? sol) && sol is bool solb ? solb : d.ShowOrderLines,
            root.TryGetValue("cloud_shadows", out object? clq) && clq is bool clqb ? clqb : d.CloudShadows,
            root.TryGetValue("water_shimmer", out object? waq) && waq is bool waqb ? waqb : d.WaterShimmer,
            root.TryGetValue("road_pieces", out object? roq) && roq is bool roqb ? roqb : d.RoadPieces,
            root.TryGetValue("tread_marks", out object? tm) && tm is bool tmb ? tmb : d.TreadMarks,
            root.TryGetValue("debris_chunks", out object? dc) && dc is bool dcb ? dcb : d.DebrisChunks,
            root.TryGetValue("effect_budget", out object? eb) && eb is bool ebb ? ebb : d.EffectBudget,
            root.TryGetValue("additive_glow", out object? ag) && ag is bool agb ? agb : d.AdditiveGlow,
            root.TryGetValue("sidebar_follows_selection", out object? sf) && sf is bool sfb ? sfb : d.SidebarFollowsSelection,
            root.TryGetValue("box_select_fighters_first", out object? bf) && bf is bool bfb ? bfb : d.BoxSelectFightersFirst,
            root.TryGetValue("mute_in_background", out object? mb) && mb is bool mbb ? mbb : d.MuteInBackground,
            root.TryGetValue("vsync", out object? vs) && vs is bool vsb ? vsb : d.VSync);
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
        sb.AppendLine("# UI scale in percent (75, 100, 125, 150, 175, 200) on top of the automatic scale from the window height");
        sb.AppendLine(FormattableString.Invariant($"ui_scale_percent = {Ui.UiScale.SnapPercent(UiScalePercent)}"));
        sb.AppendLine("# Text size: 0 Small, 1 Normal, 2 Large, 3 Extra large (fonts only)");
        sb.AppendLine(FormattableString.Invariant($"text_size = {(int)TextSize}"));
        sb.AppendLine(FormattableString.Invariant($"music_volume = {MusicVolume}"));
        sb.AppendLine(FormattableString.Invariant($"sfx_volume = {SfxVolume}"));
        sb.AppendLine(FormattableString.Invariant($"master_volume = {MasterVolume}"));
        sb.AppendLine(FormattableString.Invariant($"ambience_volume = {AmbienceVolume}"));
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
        sb.AppendLine(FormattableString.Invariant($"health_bars_always = {(HealthBarsAlways ? "true" : "false")}"));
        sb.AppendLine("# Draw infantry 1.25 times larger (D-184 pilot; sprites, selection brackets and health bars; render only).");
        sb.AppendLine(FormattableString.Invariant($"larger_infantry = {(LargerInfantry ? "true" : "false")}"));
        sb.AppendLine("# Single-player autosave: minutes between autosaves (0 = off); 3 rotating slots (D-205).");
        sb.AppendLine(FormattableString.Invariant($"autosave_minutes = {Math.Clamp(AutosaveMinutes, 0, MaxAutosaveMinutes)}"));
        sb.AppendLine("# Single-player skirmish game speed in percent of normal: 50, 75, 100, 150 or 200 (the sim step is unchanged).");
        sb.AppendLine(FormattableString.Invariant($"game_speed_percent = {Rts.Game.Host.GameSpeed.Snap(GameSpeedPercent)}"));
        sb.AppendLine("# Weather particles and screen shake on big blasts (view only).");
        sb.AppendLine(FormattableString.Invariant($"weather = {(Weather ? "true" : "false")}"));
        sb.AppendLine(FormattableString.Invariant($"screen_shake = {(ScreenShake ? "true" : "false")}"));
        sb.AppendLine("# Reduce flashes: no white hit flash, promotion flash or muzzle sparks (separate from screen shake).");
        sb.AppendLine(FormattableString.Invariant($"reduce_flashes = {(ReduceFlashes ? "true" : "false")}"));
        sb.AppendLine("# The first-run offer to play the tutorial has been shown.");
        sb.AppendLine(FormattableString.Invariant($"tutorial_offered = {(TutorialOffered ? "true" : "false")}"));
        sb.AppendLine("# Check GitHub Releases once at start for a newer version (nothing is downloaded).");
        sb.AppendLine(FormattableString.Invariant($"check_for_updates = {(CheckForUpdates ? "true" : "false")}"));
        sb.AppendLine("# LAN: player name (empty = Windows user name) and UDP ports.");
        sb.AppendLine(FormattableString.Invariant($"player_name = {Quote(PlayerName)}"));
        sb.AppendLine(FormattableString.Invariant($"lan_game_port = {Math.Clamp(LanGamePort, 1024, 65535)}"));
        sb.AppendLine(FormattableString.Invariant($"lan_discovery_port = {Math.Clamp(LanDiscoveryPort, 1024, 65535)}"));
        sb.AppendLine("# UI skin: faction (art of your faction where it exists) | classic (always the plain look)");
        sb.AppendLine(FormattableString.Invariant($"ui_skin = {Quote(UseFactionSkin ? UiSkinFaction : UiSkinClassic)}"));
        sb.AppendLine("# Thin lines from selected units to their attack target or move destination (view only).");
        sb.AppendLine(FormattableString.Invariant($"show_order_lines = {(ShowOrderLines ? "true" : "false")}"));
        sb.AppendLine("# Road pieces: flared junctions and rounded dead ends over roads (view only).");
        sb.AppendLine(FormattableString.Invariant($"road_pieces = {(RoadPieces ? "true" : "false")}"));
        sb.AppendLine("# Water shimmer: a slow band of sparkles over visible water (view only).");
        sb.AppendLine(FormattableString.Invariant($"water_shimmer = {(WaterShimmer ? "true" : "false")}"));
        sb.AppendLine("# Cloud shadows: soft shadows drifting over the map (view only).");
        sb.AppendLine(FormattableString.Invariant($"cloud_shadows = {(CloudShadows ? "true" : "false")}"));
        sb.AppendLine("# Battle effects (view only, S45 V-04): tread marks on soft ground, debris chunks from deaths.");
        sb.AppendLine(FormattableString.Invariant($"tread_marks = {(TreadMarks ? "true" : "false")}"));
        sb.AppendLine(FormattableString.Invariant($"debris_chunks = {(DebrisChunks ? "true" : "false")}"));
        sb.AppendLine("# Effect budget: caps explosions, debris and smoke in big fights, dropping far-off and small ones first.");
        sb.AppendLine(FormattableString.Invariant($"effect_budget = {(EffectBudget ? "true" : "false")}"));
        sb.AppendLine("# Additive glow under explosions, muzzle flashes and fires (flashes also follow reduce_flashes).");
        sb.AppendLine(FormattableString.Invariant($"additive_glow = {(AdditiveGlow ? "true" : "false")}"));
        sb.AppendLine("# Play options (S47 U-13): the sidebar tab follows a selected production building.");
        sb.AppendLine(FormattableString.Invariant($"sidebar_follows_selection = {(SidebarFollowsSelection ? "true" : "false")}"));
        sb.AppendLine("# A plain box select leaves out harvesters and builders when fighters are in the box (Alt+drag does the other one).");
        sb.AppendLine(FormattableString.Invariant($"box_select_fighters_first = {(BoxSelectFightersFirst ? "true" : "false")}"));
        sb.AppendLine("# Mute all sound while the window is in the background (saved volumes are not changed).");
        sb.AppendLine(FormattableString.Invariant($"mute_in_background = {(MuteInBackground ? "true" : "false")}"));
        sb.AppendLine("# Wait for the monitor refresh when drawing (off can tear the picture).");
        sb.AppendLine(FormattableString.Invariant($"vsync = {(VSync ? "true" : "false")}"));
        sb.AppendLine("# Pan the camera to a new mission objective's target (M11-05)");
        sb.AppendLine(FormattableString.Invariant($"camera_cue_on_new_objective = {(CameraCueOnNewObjective ? "true" : "false")}"));
        IReadOnlyList<(string Id, int Volume)> alerts = Alerts.Overrides();
        if (alerts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# Alert volume per category in percent (0 = muted). Only changed categories are listed.");
            sb.AppendLine("[alerts]");
            foreach ((string id, int volume) in alerts) sb.AppendLine(FormattableString.Invariant($"{id} = {volume}"));
        }

        IReadOnlyList<(string Id, string Key)> rebound = Bindings.Overrides();
        if (rebound.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# Rebound keys: action = key name. Only changed keys are listed; delete a line (or the table) to restore its default.");
            sb.AppendLine("[keys]");
            foreach ((string id, string key) in rebound) sb.AppendLine(FormattableString.Invariant($"{id} = {Quote(key)}"));
        }

        IReadOnlyList<(string Id, string Key)> second = Bindings.AltOverrides();   // QOL-20
        if (second.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# Second keys: action = key name. The action works with either key; delete a line to remove its second key.");
            sb.AppendLine("[keys_alt]");
            foreach ((string id, string key) in second) sb.AppendLine(FormattableString.Invariant($"{id} = {Quote(key)}"));
        }

        return sb.ToString();
    }

    /// <summary>The alert volumes in force (every category at 100 when none were changed).</summary>
    public Ui.Alerts.AlertMix Alerts => AlertVolumes ?? Ui.Alerts.AlertMix.Default;

    /// <summary>These settings with <paramref name="mix"/> (stored as null when it is the default, so equal settings compare equal).</summary>
    public Settings WithAlerts(Ui.Alerts.AlertMix mix) => this with { AlertVolumes = mix.IsDefault ? null : mix };

    private static Ui.Alerts.AlertMix? ParseAlerts(TomlTable root)
    {
        if (!root.TryGetValue("alerts", out object? raw) || raw is not TomlTable table) return null;
        var saved = new List<(string, long)>();
        foreach (KeyValuePair<string, object> entry in table)
            if (entry.Value is long v) saved.Add((entry.Key, v));
        Ui.Alerts.AlertMix mix = Ui.Alerts.AlertMix.FromPairs(saved);
        return mix.IsDefault ? null : mix;
    }

    /// <summary>The key bindings in force (the defaults when none were changed).</summary>
    public KeyBindings Bindings => KeyMap ?? KeyBindings.Default;

    /// <summary>These settings with <paramref name="bindings"/> (stored as null when they are the defaults, so equal settings compare equal).</summary>
    public Settings WithBindings(KeyBindings bindings) => this with { KeyMap = bindings.IsAllDefault ? null : bindings };

    private static KeyBindings? ParseKeys(TomlTable root)
    {
        var saved = new List<(string, string)>();
        if (root.TryGetValue("keys", out object? raw) && raw is TomlTable table)
        {
            foreach (KeyValuePair<string, object> entry in table)
            {
                if (entry.Value is string name) saved.Add((entry.Key, name));
            }
        }

        var savedAlt = new List<(string, string)>();   // QOL-20: the second keys
        if (root.TryGetValue("keys_alt", out object? rawAlt) && rawAlt is TomlTable altTable)
            foreach (KeyValuePair<string, object> entry in altTable)
                if (entry.Value is string name) savedAlt.Add((entry.Key, name));
        KeyBindings parsed = KeyBindings.FromOverrides(saved, savedAlt);
        return parsed.IsAllDefault ? null : parsed;
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
            AtomicFile.WriteAllText(file, ToToml());   // S18 GS-05 (a crash mid-write no longer truncates the settings); U-10 flushes to disk first
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
