// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>
/// Skirmish setup v2 (design 08 §2, M4-11): the map (curated, the player's own, or generated with biome, size and a seed that
/// can be typed or re-rolled), 2 to 4 slots (Closed, Human or AI, with faction, AI tier, team and colour each), and Start. With no
/// Human slot the button reads Spectate. Start opens the game through <see cref="GameScreen.CreateSkirmish"/>.
/// </summary>
public sealed class SkirmishSetupScreen : IScreen
{
    private const ulong MaxSeed = 999_999_999;
    private const int DefaultGeneratedSeed = 1;

    private readonly GameServices _s;
    private MenuPanel _panel = null!;
    private Point _mouse;
    private readonly string[] _tierNames;
    private readonly IReadOnlyList<MapEntry> _maps;
    private int _mapIndex;
    private Biome _biome = Biome.RustBelt;
    private MapSize _size = MapSize.Small;
    private ulong _seed = DefaultGeneratedSeed;
    private OceanEdges _ocean = OceanEdges.None;   // M7-14
    private readonly List<SkirmishSlot> _slots;
    private int _layout = MapGenerator.CurrentLayout;   // generator layout of the generated map (a layout-2 map defaults 4 players to teams 1+2 vs 3+4)
    private int _seedRow = -1;
    private int _firstSlotRow;
    private string _error = "";
    private bool _hasKey;                 // GS-13: whether an LLM key is stored, read once (the key store is a file read plus a decrypt)
    private Settings? _keyFor;            // the settings value _hasKey was read for; a new value (Settings changed) reads it again

    /// <summary>Creates the screen with <see cref="SkirmishSettings.Default"/> values (curated map, a human against a Regular AI).</summary>
    public SkirmishSetupScreen(GameServices services) : this(services, MapPicker.List(services.RulesDir))
    {
    }

    /// <summary>Creates the screen with <paramref name="preselect"/> chosen as the map (M4-INT2: the editor's "Play this map"). A map file
    /// that is not in the list is added to it; generator parameters set the Generated entry's biome, size and seed.</summary>
    public SkirmishSetupScreen(GameServices services, MapChoice preselect) : this(services, MapPicker.List(services.RulesDir), preselect)
    {
    }

    /// <summary>Creates the screen over a given map list (tests).</summary>
    public SkirmishSetupScreen(GameServices services, IReadOnlyList<MapEntry> maps, MapChoice? preselect = null)
    {
        _s = services;
        _maps = preselect is null ? maps : PreselectMap(services.RulesDir, maps, preselect, out _mapIndex);
        if (preselect?.Generated is { } g)
        {
            _biome = g.Biome;
            _size = g.Size;
            _seed = g.Seed;
            _ocean = g.OceanEdges;
            _layout = g.Layout;
        }
        _tierNames = LoadTierNames(services.RulesDir);
        _slots = new List<SkirmishSlot>(SkirmishSettings.Default.Slots);
        RefreshKey();
        _panel = BuildPanel();
    }

    /// <summary>The map list with <paramref name="choice"/> selected: its index in <paramref name="index"/>, and the list with the map added
    /// when it is a file that was not in it (an editor map saved outside the user folder).</summary>
    public static IReadOnlyList<MapEntry> PreselectMap(string rulesDir, IReadOnlyList<MapEntry> maps, MapChoice choice, out int index)
    {
        if (choice.Generated is not null)
        {
            index = Math.Max(0, maps.ToList().FindIndex(m => m.IsGenerated));
            return maps;
        }

        string? file = choice.File;
        for (int i = 0; i < maps.Count; i++)
        {
            if (maps[i].File is { } f && file is not null && SameFile(rulesDir, f, file))
            {
                index = i;
                return maps;
            }
        }

        index = 0;
        if (file is null) return maps;
        try
        {
            (Rts.Sim.Map.MapData map, MapMeta meta) = MapFile.LoadWithMeta(MapPicker.ResolveFile(rulesDir, file));
            int max = meta.MaxPlayers > 0 ? Math.Min(meta.MaxPlayers, map.StartPositions.Count) : map.StartPositions.Count;
            var list = new List<MapEntry>(maps);
            int at = Math.Max(0, list.FindIndex(m => m.IsGenerated));
            list.Insert(at, new MapEntry($"{map.Name} {map.Width}x{map.Height}, {max} players (yours)", file, User: true, max));
            index = at;
            return list;
        }
        catch (MapFileException)
        {
            return maps;
        }
    }

    private static bool SameFile(string rulesDir, string a, string b) =>
        string.Equals(Path.GetFullPath(MapPicker.ResolveFile(rulesDir, a)), Path.GetFullPath(MapPicker.ResolveFile(rulesDir, b)), StringComparison.Ordinal);

    /// <summary>The choices as they stand now.</summary>
    public SkirmishSettings Current
    {
        get
        {
            MapEntry entry = _maps[_mapIndex];
            int open = _slots.Count(x => x.Kind != SlotKind.Closed);
            MapChoice map = entry.IsGenerated
                ? new MapChoice(null, new MapGenParams(_biome, _size, Math.Clamp(open, SkirmishSettings.MinPlayers, SkirmishSettings.MaxSlots), _seed, null, ResolvedOcean(out _), _layout))
                : new MapChoice(entry.File, null);
            ulong simSeed = entry.IsGenerated ? _seed : SkirmishGame.CuratedSimSeed;
            return new SkirmishSettings(map, _slots.ToArray(), simSeed);
        }
    }

    /// <summary>True when no slot is Human, so Start reads Spectate.</summary>
    public bool IsSpectate => _slots.All(x => x.Kind != SlotKind.Human);

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
    }

    /// <inheritdoc />
    public void Exit()
    {
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        if (!ReferenceEquals(_keyFor, _s.Settings)) RefreshKey();   // the settings were changed (and saved) since: read the key state again
        if (input.ConsumePressed(Keys.Escape))
        {
            _s.Screens.Pop();
            return;
        }

        if (_seedRow >= 0 && _panel.Selected == _seedRow) TypeSeed(input);
        _panel.Update(input, _s.Viewport, _s.UiScale);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }

    // The key store is read here and nowhere else while the screen is open (it is a file read and a decrypt).
    private void RefreshKey()
    {
        _keyFor = _s.Settings;
        _hasKey = HasCommanderKey(_s.Settings);
    }

    // The rows depend on the map kind and the slot count, so the panel is rebuilt when either changes; the highlighted row is kept.
    private MenuPanel BuildPanel()
    {
        var rows = new List<MenuItem>
        {
            new(() => $"Map: {_maps[_mapIndex].Label}", null, StepMap, new MenuButton("<", () => StepMap(-1)), new MenuButton(">", () => StepMap(1)))
            {
                WidthTexts = _maps.Select(m => $"Map: {m.Label}").ToArray(),   // the panel keeps one width whichever map is shown
            },
        };
        _seedRow = -1;
        if (_maps[_mapIndex].IsGenerated)
        {
            rows.Add(new(() => $"Biome: {MapPicker.BiomeName(_biome)}", null, StepBiome,
                new MenuButton("<", () => StepBiome(-1)), new MenuButton(">", () => StepBiome(1)))
            { WidthTexts = Enum.GetValues<Biome>().Select(b => $"Biome: {MapPicker.BiomeName(b)}").ToArray() });
            rows.Add(new(SizeText, null, StepSize, new MenuButton("<", () => StepSize(-1)), new MenuButton(">", () => StepSize(1)))
            { WidthTexts = Enum.GetValues<MapSize>().Select(z => SizeTextFor(z, 4)).ToArray() });
            rows.Add(new(() => OceanChoices.Label(_ocean), null, d => _ocean = OceanChoices.Step(_ocean, d),
                new MenuButton("<", () => _ocean = OceanChoices.Step(_ocean, -1)), new MenuButton(">", () => _ocean = OceanChoices.Step(_ocean, 1)))
            { WidthTexts = OceanChoices.All.Select(OceanChoices.Label).ToArray() });
            _seedRow = rows.Count;
            rows.Add(new(() => $"Seed: {_seed}", null, d => StepSeed(d),
                new MenuButton("-", () => StepSeed(-1)), new MenuButton("+", () => StepSeed(1)), new MenuButton("Reroll", RerollSeed))
            { WidthTexts = new[] { $"Seed: {MaxSeed}" } });
        }

        rows.Add(new(() => $"Slots: {_slots.Count}", null, StepSlotCount,
            new MenuButton("-", () => StepSlotCount(-1)), new MenuButton("+", () => StepSlotCount(1))));
        _firstSlotRow = rows.Count;
        for (int i = 0; i < _slots.Count; i++)
        {
            int slot = i;
            // A fixed label column (cut with "..." when a label is longer), the colour swatch in a column of its own, then the buttons.
            // The panel is sized for the widest label there can be, so it keeps one width while the choices change (S18 UX-LM-03).
            rows.Add(new(() => SlotText(slot), null, d => StepKind(slot, d),
                new MenuButton("Kind", () => StepKind(slot, 1)), new MenuButton("Fac", () => StepSlotFaction(slot)),
                new MenuButton("Tier", () => StepSlotTier(slot)), new MenuButton("Cmdr", () => ToggleSlotCommander(slot)),
                new MenuButton("Team", () => StepSlotTeam(slot)),
                new MenuButton("Col", () => StepSlotColour(slot)))
            {
                WidthTexts = WidestSlotTexts(_tierNames),
                Swatch = () => _slots[slot].Kind == SlotKind.Closed ? null : PlayerColours.Choice(_slots[slot].Colour),
            });
        }

        rows.Add(new(() => _error.Length > 0 ? _error : NoteText() ?? HintText()) { Info = true });
        rows.Add(new(() => IsSpectate ? "Spectate" : "Start", Start));
        rows.Add(new(() => "Back", () => _s.Screens.Pop()));

        var panel = new MenuPanel("Skirmish", rows);
        if (_panel is not null) panel.Selected = Math.Min(_panel.Selected, rows.Count - 1);
        return panel;
    }

    private void Rebuild() => _panel = BuildPanel();

    private string SizeText() => SizeTextFor(_size, _slots.Count(x => x.Kind != SlotKind.Closed));

    private static string SizeTextFor(MapSize size, int open)
    {
        int cells = MapGenerator.CellsFor(size);
        string tight = size == MapSize.Small && open == 4 ? " (tight for 4)" : "";
        return $"Size: {MapPicker.SizeName(size)} {cells}x{cells}{tight}";
    }

    /// <summary>M7-14: the one-line note about the Expedition's ocean edge, or null.</summary>
    private string? NoteText()
    {
        ResolvedOcean(out string? note);
        return note;
    }

    /// <summary>M7-14: the ocean choice the generated map gets (an Expedition slot forces one edge), with its note.</summary>
    private OceanEdges ResolvedOcean(out string? note) =>
        ExpeditionSetup.ResolveOcean(_ocean, _maps[_mapIndex].IsGenerated && ExpeditionSetup.HasExpedition(_slots), out note);

    // The highlighted slot says when its LLM commander has no key (it used to be appended to the row, which made the row grow).
    private string HintText() =>
        _panel is not null && _panel.Selected >= _firstSlotRow && _panel.Selected < _firstSlotRow + _slots.Count && NoKeyFor(_slots[_panel.Selected - _firstSlotRow])
            ? SetupText.NoKeyHint
            : IsSpectate ? "(no Human slot: you will watch)" : "(Kind Fac Tier Cmdr Team Col cycle each choice)";

    private bool NoKeyFor(SkirmishSlot x) => x.Kind == SlotKind.Ai && x.Commander && !_hasKey;

    private string SlotText(int i) => SlotLabel(_slots[i], i, _tierNames);

    /// <summary>Every slot label there can be, longest first in effect: the panel is sized for the widest, so it never changes width when a choice changes.</summary>
    public static IReadOnlyList<string> WidestSlotTexts(IReadOnlyList<string> tierNames)
    {
        var texts = new List<string>();
        foreach (Faction f in RuleCounts.PlayableOrder)
            foreach (bool commander in new[] { false, true })
                foreach (string tier in tierNames)
                    texts.Add($"4 AI {(commander ? SetupText.CommanderLabel + " " : "")}{tier} {FactionName(f)} T4");
        return texts;
    }

    /// <summary>True when an LLM provider is chosen in the settings and a key is stored for it (<see cref="Rts.Game.Ui.Llm.LlmHooks.Keys"/>).</summary>
    public static bool HasCommanderKey(Settings settings) =>
        settings.ProviderKind != Rts.Llm.LlmProviderKind.None && !string.IsNullOrEmpty(Rts.Game.Ui.Llm.LlmHooks.Keys.Load(settings.ProviderKind));

    /// <summary>The slot with its Commander flag flipped; Human and Closed slots come back unchanged.</summary>
    public static SkirmishSlot ToggleCommander(SkirmishSlot slot) =>
        slot.Kind == SlotKind.Ai ? slot with { Commander = !slot.Commander } : slot;

    /// <summary>True when flipping <paramref name="before"/> to <paramref name="after"/> switches a commander on although no key is set (S19 D-205: say so first).</summary>
    public static bool NeedsNoKeyNotice(SkirmishSlot before, SkirmishSlot after, bool hasKey) =>
        before.Kind == SlotKind.Ai && !before.Commander && after.Commander && !hasKey;

    private void ToggleSlotCommander(int i)
    {
        SkirmishSlot before = _slots[i];
        _slots[i] = ToggleCommander(before);
        if (NeedsNoKeyNotice(before, _slots[i], _hasKey))
            _s.Screens.Push(new NoticeScreen(_s, SetupText.NoKeyNoticeTitle, SetupText.NoKeyNotice));
    }

    /// <summary>The row text of a slot: "1 Human Authority T1", "2 AI Directorate tier 3 Veteran T2", "3 Closed".</summary>
    public static string SlotLabel(SkirmishSlot slot, int index, IReadOnlyList<string> tierNames)
    {
        string head = $"{index + 1} ";
        string tier = tierNames[Math.Clamp(slot.AiTier, 1, tierNames.Count) - 1];
        if (slot.Commander) tier = SetupText.CommanderLabel + " " + tier;
        return slot.Kind switch
        {
            SlotKind.Closed => head + "Closed",
            SlotKind.Human => $"{head}Human {FactionName(slot.Faction)} T{slot.Team + 1}",
            _ => $"{head}AI {tier} {FactionName(slot.Faction)} T{slot.Team + 1}",
        };
    }

    private static string[] LoadTierNames(string rulesDir)
    {
        var names = new string[DifficultyLoader.TierIds.Count];
        for (int i = 0; i < names.Length; i++) names[i] = $"Tier {i + 1}";
        try
        {
            foreach (DifficultyProfile profile in DifficultyLoader.Load(DifficultyLoader.PathBesideRules(rulesDir)))
            {
                if (profile.Tier >= 1 && profile.Tier <= names.Length && profile.Id.Length > 0)
                    names[profile.Tier - 1] = char.ToUpperInvariant(profile.Id[0]) + profile.Id[1..];
            }
        }
        catch (RulesException)
        {
            // Keep "Tier N" names.
        }
        return names;
    }

    // Digits typed while the seed row is highlighted replace the number; Backspace removes the last digit.
    private void TypeSeed(InputFrame input)
    {
        foreach (char ch in input.ConsumeText())
        {
            if (ch < '0' || ch > '9') continue;
            ulong next = _seed * 10 + (ulong)(ch - '0');
            if (next <= MaxSeed) _seed = next;
        }

        if (input.ConsumePressed(Keys.Back)) _seed /= 10;
    }

    private void StepSeed(int delta) => _seed = delta < 0 ? (_seed > 0 ? _seed - 1 : 0) : Math.Min(MaxSeed, _seed + 1);

    // The seed is a front-end choice (it is shown and saved), so the UI random source is fine here.
    private void RerollSeed() => _seed = (ulong)Random.Shared.NextInt64(1, (long)MaxSeed + 1);

    private void StepMap(int delta)
    {
        _mapIndex = ((_mapIndex + delta) % _maps.Count + _maps.Count) % _maps.Count;
        Rebuild();
    }

    private void StepBiome(int delta) => _biome = (Biome)(((int)_biome + delta + 4) % 4);

    private void StepSize(int delta) => _size = (MapSize)(((int)_size + delta + 3) % 3);

    private void StepSlotCount(int delta)
    {
        int n = Math.Clamp(_slots.Count + delta, SkirmishSettings.MinPlayers, SkirmishSettings.MaxSlots);
        while (_slots.Count > n) _slots.RemoveAt(_slots.Count - 1);
        while (_slots.Count < n) _slots.Add(NewSlot(_slots.Count));
        if (_layout == MapGenerator.ZonedLayout)
            for (int i = 0; i < _slots.Count; i++) _slots[i] = _slots[i] with { Team = SkirmishSettings.DefaultTeam(i, _slots.Count, _layout) };
        Rebuild();
    }

    private SkirmishSlot NewSlot(int index) =>
        new(SlotKind.Ai, Faction.Authority, SkirmishSettings.DefaultAiTier, FreeColour(index, -1), Math.Min(index, SkirmishSettings.MaxSlots - 1));

    private int FreeColour(int from, int except)
    {
        for (int k = 0; k < PlayerColours.Count; k++)
        {
            int c = (from + k) % PlayerColours.Count;
            if (!_slots.Where((x, i) => i != except && x.Kind != SlotKind.Closed).Any(x => x.Colour == c)) return c;
        }
        return from % PlayerColours.Count;
    }

    // Closed -> Human -> AI -> Closed. Only one slot may be Human: choosing Human turns the previous one into an AI.
    private void StepKind(int i, int delta)
    {
        var next = (SlotKind)(((int)_slots[i].Kind + delta + 3) % 3);
        if (next == SlotKind.Human)
            for (int k = 0; k < _slots.Count; k++)
                if (k != i && _slots[k].Kind == SlotKind.Human) _slots[k] = _slots[k] with { Kind = SlotKind.Ai };
        _slots[i] = _slots[i] with { Kind = next };
        if (next != SlotKind.Closed && _slots.Where((x, k) => k != i && x.Kind != SlotKind.Closed).Any(x => x.Colour == _slots[i].Colour))
            _slots[i] = _slots[i] with { Colour = FreeColour(_slots[i].Colour, i) };
        _error = "";
    }

    private void StepSlotFaction(int i) => _slots[i] = _slots[i] with { Faction = NextFaction(_slots[i].Faction, 1) };

    private void StepSlotTier(int i)
    {
        if (_slots[i].Kind != SlotKind.Ai) return;
        _slots[i] = _slots[i] with { AiTier = _slots[i].AiTier % _tierNames.Length + 1 };
    }

    private void StepSlotTeam(int i) => _slots[i] = _slots[i] with { Team = (_slots[i].Team + 1) % SkirmishSettings.MaxSlots };

    private void StepSlotColour(int i)
    {
        for (int k = 1; k <= PlayerColours.Count; k++)
        {
            int c = (_slots[i].Colour + k) % PlayerColours.Count;
            if (_slots.Where((x, j) => j != i && x.Kind != SlotKind.Closed).Any(x => x.Colour == c)) continue;
            _slots[i] = _slots[i] with { Colour = c };
            return;
        }
    }

    /// <summary>The faction after <paramref name="faction"/> in the pick list, wrapping round (Authority, Directorate, Left Behind; Neutral is
    /// never offered, Q-37). A faction that is not in the list steps from the start of it.</summary>
    public static Faction NextFaction(Faction faction, int delta)
    {
        IReadOnlyList<Faction> order = RuleCounts.PlayableOrder;
        int n = order.Count;
        int at = 0;
        for (int i = 0; i < n; i++) if (order[i] == faction) at = i;
        return order[((at + delta) % n + n) % n];
    }

    /// <summary>The name shown for a faction on the setup screen.</summary>
    public static string FactionName(Faction faction) => faction switch { Faction.Directorate => "Directorate", Faction.LeftBehind => "Left Behind", Faction.Expedition => "Overseas Expedition", Faction.Neutral => "Neutral", _ => "Authority" };

    /// <summary>Cuts text to at most <paramref name="max"/> characters, ending in "..." when it was cut.</summary>
    public static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 3)] + "...";

    private IReadOnlyList<string>? MapErrors(string file)
    {
        try { return MapCheck.Errors(_s.RulesDir, file); }
        catch (MapFileException e) { return new[] { e.Message }; }
    }

    // ASSUMED (M3-08): a faction whose start kit names types the rules do not have yet does not crash the menu; the setup screen stays
    // open and says why.
    private void Start()
    {
        SkirmishSettings settings = Current;
        IReadOnlyList<string> problems = settings.Validate();
        if (problems.Count == 0 && !_maps[_mapIndex].IsGenerated && settings.PlayerCount > _maps[_mapIndex].MaxPlayers)
            problems = new[] { $"This map has room for {_maps[_mapIndex].MaxPlayers} players." };
        // M7-14: an Expedition slot needs a hand-made map with a landing coast (D-127)
        if (problems.Count == 0 && settings.Map.File is { } mapFile && ExpeditionSetup.LandingProblem(settings, ExpeditionSetup.ReadMeta(_s.RulesDir, mapFile)) is { } landing)
            problems = new[] { landing };
        if (problems.Count > 0)
        {
            _error = Shorten(problems[0], 56);
            return;
        }

        // S18 DATA-TOOLS-03: a hand-made map is checked by the map validator; an error names the map in a Notice (generated maps are checked when made).
        if (!_maps[_mapIndex].IsGenerated && settings.Map.File is { } handMade && MapErrors(handMade) is { Count: > 0 } mapErrors)
        {
            _s.Screens.Push(new NoticeScreen(_s, "This map cannot be played",
                $"{_maps[_mapIndex].Label}\n" + string.Join("\n", mapErrors.Take(4)) + (mapErrors.Count > 4 ? $"\n... and {mapErrors.Count - 4} more" : "")));
            return;
        }

        try
        {
            GameScreen screen = GameScreen.CreateSkirmish(_s, settings);
            _error = "";
            _s.Screens.Push(screen);
        }
        catch (Exception e) when (ScreenGuard.IsDataError(e))   // S18 GS-10: also IOException, MapFileException and the like
        {
            _error = Shorten("Cannot start: " + e.Message, 56);
        }
    }
}
