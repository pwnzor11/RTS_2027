// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Input;

namespace Rts.Game.Screens.Editor;

/// <summary>The "New map" dialog: biome and size (S 64 / M 96 / L 128, or any step from 32), then Create.</summary>
public sealed class NewMapScreen : IScreen
{
    /// <summary>Sizes offered, in cells per side.</summary>
    public static readonly int[] Sizes = [32, 48, 64, 80, 96, 112, 128];

    private readonly GameServices _s;
    private readonly Action<EditorDocument> _created;
    private readonly MenuPanel _panel;
    private int _biome;
    private int _size = 2;
    private Point _mouse;

    /// <summary>Creates the dialog; <paramref name="created"/> gets the blank document after the dialog has closed.</summary>
    public NewMapScreen(GameServices services, Action<EditorDocument> created)
    {
        _s = services;
        _created = created;
        _panel = new MenuPanel("New map", new[]
        {
            new MenuItem(() => $"Biome: {BiomeLabel(_biome)}", null, d => _biome = Step(_biome, d, MapFile.BiomeKeys.Length),
                new MenuButton("<", () => _biome = Step(_biome, -1, MapFile.BiomeKeys.Length)), new MenuButton(">", () => _biome = Step(_biome, 1, MapFile.BiomeKeys.Length))),
            new MenuItem(() => $"Size: {SizeLabel(Sizes[_size])}", null, d => _size = Step(_size, d, Sizes.Length),
                new MenuButton("<", () => _size = Step(_size, -1, Sizes.Length)), new MenuButton(">", () => _size = Step(_size, 1, Sizes.Length))),
            new MenuItem(() => "Create", Create),
            new MenuItem(() => "Back", () => _s.Screens.Pop()),
        });
    }

    /// <summary>"64 x 64 (S)" style label.</summary>
    public static string SizeLabel(int side) => $"{side} x {side}" + side switch { 64 => " (S)", 96 => " (M)", 128 => " (L)", _ => "" };

    /// <summary>"Rust belt" style label for a biome index.</summary>
    public static string BiomeLabel(int biome)
    {
        string k = MapFile.BiomeKeys[biome].Replace('_', ' ');
        return char.ToUpperInvariant(k[0]) + k[1..];
    }

    /// <summary>The blank map this dialog's choices make.</summary>
    public static EditorDocument Build(int biome, int side) =>
        EditorDocument.Create(MapFile.BiomeKeys[biome] + "_new", MapFile.BiomeKeys[biome], side, side);

    private static int Step(int v, int d, int n) => ((v + d) % n + n) % n;

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => false;

    /// <inheritdoc />
    public void Enter() { }

    /// <inheritdoc />
    public void Exit() { }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        if (input.ConsumePressed(Keys.Escape)) { _s.Screens.Pop(); return; }
        _panel.Update(input, _s.Viewport, _s.UiScale);
        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillDim(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }

    private void Create()
    {
        EditorDocument doc = Build(_biome, Sizes[_size]);
        _s.Screens.Pop();
        _created(doc);
    }
}
