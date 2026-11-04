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
    private readonly List<SkirmishSlot> _slots;
    private int _seedRow = -1;
    private int _firstSlotRow;
    private string _error = "";

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
        }
        _tierNames = LoadTierNames(services.RulesDir);
        _slots = new List<SkirmishSlot>(SkirmishSettings.Default.Slots);
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
                ? new MapChoice(null, new MapGenParams(_biome, _size, Math.Clamp(open, SkirmishSettings.MinPlayers, SkirmishSettings.MaxSlots), _seed))
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
        int scale = _s.UiScale;
        _panel.Draw(sb, _s.Pixel, _s.Viewport, scale, _mouse);

        // A colour swatch left of each slot row's buttons.
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Kind == SlotKind.Closed) continue;
            int row = _firstSlotRow + i;
            Rectangle r = _panel.RowRect(row);
            Rectangle firstButton = _panel.ButtonRect(row, 0);
            int size = r.Height - 4 * scale;
            var swatch = new Rectangle(firstButton.X - size - 3 * scale, r.Y + (r.Height - size) / 2, size, size);
            Shapes.Fill(sb, _s.Pixel, swatch, PlayerColours.Choice(_slots[i].Colour));
            Shapes.Outline(sb, _s.Pixel, swatch, Color.White);
        }
    }

    // The rows depend on the map kind and the slot count, so the panel is rebuilt when either changes; the highlighted row is kept.
    private MenuPanel BuildPanel()
    {
        var rows = new List<MenuItem>
        {
            new(() => $"Map: {_maps[_mapIndex].Label}", null, StepMap, new MenuButton("<", () => StepMap(-1)), new MenuButton(">", () => StepMap(1))),
        };
        _seedRow = -1;
        if (_maps[_mapIndex].IsGenerated)
        {
            rows.Add(new(() => $"Biome: {MapPicker.BiomeName(_biome)}", null, StepBiome,
                new MenuButton("<", () => StepBiome(-1)), new MenuButton(">", () => StepBiome(1))));
            rows.Add(new(SizeText, null, StepSize, new MenuButton("<", () => StepSize(-1)), new MenuButton(">", () => StepSize(1))));
            _seedRow = rows.Count;
            rows.Add(new(() => $"Seed: {_seed}", null, d => StepSeed(d),
                new MenuButton("-", () => StepSeed(-1)), new MenuButton("+", () => StepSeed(1)), new MenuButton("Reroll", RerollSeed)));
        }

        rows.Add(new(() => $"Slots: {_slots.Count}", null, StepSlotCount,
            new MenuButton("-", () => StepSlotCount(-1)), new MenuButton("+", () => StepSlotCount(1))));
        _firstSlotRow = rows.Count;
        for (int i = 0; i < _slots.Count; i++)
        {
            int slot = i;
            rows.Add(new(() => SlotText(slot), null, d => StepKind(slot, d),
                new MenuButton("Kind", () => StepKind(slot, 1)), new MenuButton("Fac", () => StepSlotFaction(slot)),
                new MenuButton("Tier", () => StepSlotTier(slot)), new MenuButton("Cmdr", () => ToggleSlotCommander(slot)),
                new MenuButton("Team", () => StepSlotTeam(slot)),
                new MenuButton("Col", () => StepSlotColour(slot))));
        }

        rows.Add(new(() => _error.Length > 0 ? _error : HintText()));
        rows.Add(new(() => IsSpectate ? "Spectate" : "Start", Start));
        rows.Add(new(() => "Back", () => _s.Screens.Pop()));

        var panel = new MenuPanel("Skirmish", rows);
        if (_panel is not null) panel.Selected = Math.Min(_panel.Selected, rows.Count - 1);
        return panel;
    }

    private void Rebuild() => _panel = BuildPanel();

    private string SizeText()
    {
        int cells = MapGenerator.CellsFor(_size);
        int open = _slots.Count(x => x.Kind != SlotKind.Closed);
        string tight = _size == MapSize.Small && open == 4 ? " (tight for 4)" : "";
        return $"Size: {MapPicker.SizeName(_size)} {cells}x{cells}{tight}";
    }

    private string HintText() => IsSpectate ? "(no Human slot: you will watch)" : "(Kind Fac Tier Cmdr Team Col cycle each choice)";

    private string SlotText(int i)
    {
        SkirmishSlot x = _slots[i];
        string label = SlotLabel(x, i, _tierNames);
        return x.Kind == SlotKind.Ai && x.Commander && !HasCommanderKey(_s.Settings) ? label + " " + SetupText.NoKeyHint : label;
    }

    /// <summary>True when an LLM provider is chosen in the settings and a key is stored (<see cref="Rts.Game.Ui.Llm.LlmHooks.SecretStore"/>).</summary>
    public static bool HasCommanderKey(Settings settings) =>
        settings.ProviderKind != Rts.Llm.LlmProviderKind.None && !string.IsNullOrEmpty(Rts.Game.Ui.Llm.LlmHooks.SecretStore.Load());

    /// <summary>The slot with its Commander flag flipped; Human and Closed slots come back unchanged.</summary>
    public static SkirmishSlot ToggleCommander(SkirmishSlot slot) =>
        slot.Kind == SlotKind.Ai ? slot with { Commander = !slot.Commander } : slot;

    private void ToggleSlotCommander(int i) => _slots[i] = ToggleCommander(_slots[i]);

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

    /// <summary>The faction after <paramref name="faction"/> in the pick list, wrapping round (Authority, Directorate).</summary>
    public static Faction NextFaction(Faction faction, int delta)
    {
        int n = RuleCounts.PlayableFactions;   // Neutral is never offered (Q-37)
        return (Faction)((((int)faction + delta) % n + n) % n);
    }

    /// <summary>The name shown for a faction on the setup screen.</summary>
    public static string FactionName(Faction faction) => faction switch { Faction.Directorate => "Directorate", Faction.Neutral => "Neutral", _ => "Authority" };

    /// <summary>Cuts text to at most <paramref name="max"/> characters, ending in "..." when it was cut.</summary>
    public static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 3)] + "...";

    // ASSUMED (M3-08): a faction whose start kit names types the rules do not have yet does not crash the menu; the setup screen stays
    // open and says why.
    private void Start()
    {
        SkirmishSettings settings = Current;
        IReadOnlyList<string> problems = settings.Validate();
        if (problems.Count == 0 && !_maps[_mapIndex].IsGenerated && settings.PlayerCount > _maps[_mapIndex].MaxPlayers)
            problems = new[] { $"This map has room for {_maps[_mapIndex].MaxPlayers} players." };
        if (problems.Count > 0)
        {
            _error = Shorten(problems[0], 56);
            return;
        }

        try
        {
            GameScreen screen = GameScreen.CreateSkirmish(_s, settings);
            _error = "";
            _s.Screens.Push(screen);
        }
        catch (Exception e) when (e is InvalidOperationException or RulesException)
        {
            _error = Shorten("Cannot start: " + e.Message, 56);
        }
    }
}
