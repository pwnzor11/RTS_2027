// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sprites;

namespace Rts.Game.Screens;

/// <summary>
/// Skirmish setup (D-062 (2)): map seed (type digits, Left/Right or the -/+/Rnd buttons), AI tier 1..5 by name,
/// player colour, Start and Back. For M2 Start opens the sandbox scenario with the chosen seed; tier and colour are kept
/// in <see cref="SkirmishSettings"/> for the production and AI packages.
/// </summary>
public sealed class SkirmishSetupScreen : IScreen
{
    private const ulong MaxSeed = 999_999_999;
    private const int SeedRow = 0;
    private const int ColourRow = 2;
    private static readonly string[] ColourNames = { "Green", "Red", "Blue", "Ochre" };

    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private Point _mouse;
    private readonly string[] _tierNames;
    private ulong _seed;
    private int _tier;
    private int _colour;

    /// <summary>Creates the screen with <see cref="SkirmishSettings.Default"/> values.</summary>
    public SkirmishSetupScreen(GameServices services)
    {
        _s = services;
        _seed = SkirmishSettings.Default.MapSeed;
        _tier = SkirmishSettings.Default.AiTier;
        _colour = SkirmishSettings.Default.PlayerColour;
        _tierNames = LoadTierNames(services.RulesDir);

        _panel = new MenuPanel("Skirmish", new[]
        {
            new MenuItem(() => $"Map seed: {_seed}", null, d => StepSeed(d),
                new MenuButton("-", () => StepSeed(-1)), new MenuButton("+", () => StepSeed(1)),
                new MenuButton("Rnd", RandomSeed)),
            new MenuItem(() => $"AI tier: {_tier} {_tierNames[_tier - 1]}", null, StepTier,
                new MenuButton("<", () => StepTier(-1)), new MenuButton(">", () => StepTier(1))),
            new MenuItem(() => $"Colour: {ColourNames[_colour]}", null, StepColour,
                new MenuButton("<", () => StepColour(-1)), new MenuButton(">", () => StepColour(1))),
            new MenuItem(() => "Start", Start),
            new MenuItem(() => "Back", () => _s.Screens.Pop()),
        });
    }

    /// <summary>The choices as they stand now.</summary>
    public SkirmishSettings Current => new(_seed, _tier, _colour);

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

        if (_panel.Selected == SeedRow) TypeSeed(input);
        _panel.Update(input, _s.Viewport, _s.UiScale);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        int scale = _s.UiScale;
        _panel.Draw(sb, _s.Pixel, _s.Viewport, scale, _mouse);

        // Colour swatch left of the colour row's buttons.
        Rectangle row = _panel.RowRect(ColourRow);
        Rectangle firstButton = _panel.ButtonRect(ColourRow, 0);
        int size = row.Height - 4 * scale;
        var swatch = new Rectangle(firstButton.X - size - 3 * scale, row.Y + (row.Height - size) / 2, size, size);
        Shapes.Fill(sb, _s.Pixel, swatch, PlayerColours.Base(_colour));
        Shapes.Outline(sb, _s.Pixel, swatch, Color.White);
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

    private void RandomSeed() => _seed = (ulong)Random.Shared.NextInt64(1, (long)MaxSeed + 1);

    private void StepTier(int delta) => _tier = Math.Clamp(_tier + delta, 1, _tierNames.Length);

    private void StepColour(int delta) => _colour = (_colour + delta + PlayerColours.Count) % PlayerColours.Count;

    private void Start() => _s.Screens.Push(GameScreen.CreateSandbox(_s, false, Current));
}
