// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Ui.Skin;

/// <summary>The look of a skinned button.</summary>
public enum SkinButtonState
{
    /// <summary>Resting.</summary>
    Normal,

    /// <summary>Mouse over.</summary>
    Hover,

    /// <summary>Mouse button held down on it.</summary>
    Pressed,

    /// <summary>Cannot be used.</summary>
    Disabled,
}

/// <summary>
/// One local player's skin for drawing (S24): the faction's <see cref="UiSkin"/> plus its textures. Each <c>Try</c> method draws the
/// piece and returns true, or returns false so the caller draws exactly what it drew before (procedural fallback). With the
/// <c>UiSkin</c> setting at "classic" (or no skin folder) every method returns false and every colour is the fallback.
/// </summary>
public sealed class SkinKit
{
    private readonly UiSkinLoader _loader;
    private readonly string? _folder;
    private readonly UiSkin? _skin;

    /// <summary>Creates the kit.</summary>
    /// <param name="loader">The skin loader.</param>
    /// <param name="faction">The local player's faction.</param>
    /// <param name="enabled">False when the setting is "classic".</param>
    public SkinKit(UiSkinLoader loader, Rts.Sim.Rules.Faction faction, bool enabled)
    {
        _loader = loader;
        if (!enabled) return;
        _folder = UiSkinLoader.FolderFor(faction);
        _skin = loader.Skin(_folder);
    }

    /// <summary>True when a skin is in use at all.</summary>
    public bool Active => _skin is not null;

    /// <summary>The skin's main text colour, or <paramref name="fallback"/>.</summary>
    public Color Text(Color fallback) => _skin?.TextColor ?? fallback;

    /// <summary>The skin's dim text colour, or <paramref name="fallback"/>.</summary>
    public Color Dim(Color fallback) => _skin?.TextDim ?? fallback;

    /// <summary>The skin's accent colour, or <paramref name="fallback"/>.</summary>
    public Color Accent(Color fallback) => _skin?.Accent ?? fallback;

    /// <summary>Draws a 9-slice piece (<c>panel</c>, <c>tab</c>, <c>minimap_frame</c>, <c>card_frame</c>...) into <paramref name="r"/>.</summary>
    public bool TrySlice(SpriteBatch batch, string key, Rectangle r)
    {
        if (_skin?.Piece(key) is not { Slice: { } slice }) return false;
        Texture2D? tex = _loader.Texture(_folder, key);
        if (tex is null) return false;
        NineSlice.Draw(batch, tex, slice, r, Color.White);
        return true;
    }

    /// <summary>Draws the stretched <c>sidebar_back</c> into <paramref name="r"/>.</summary>
    public bool TryBack(SpriteBatch batch, Rectangle r)
    {
        Texture2D? tex = _loader.Texture(_folder, UiSkin.SidebarBackKey);
        if (tex is null) return false;
        batch.Draw(tex, r, Color.White);
        return true;
    }

    /// <summary>Draws a button or tab. A missing state piece falls back to the plain <c>button</c> piece (tabs: <c>tab</c>).</summary>
    /// <param name="batch">A begun batch.</param>
    /// <param name="r">Target.</param>
    /// <param name="state">The button state.</param>
    /// <param name="tab">True for a tab: <c>tab_active</c> when <paramref name="active"/>, else <c>tab</c>.</param>
    /// <param name="active">The selected tab or armed button.</param>
    public bool TryButton(SpriteBatch batch, Rectangle r, SkinButtonState state, bool tab = false, bool active = false)
    {
        string[] keys = tab
            ? (active ? new[] { "tab_active", "tab" } : new[] { "tab" })
            : (active ? new[] { "button_pressed", "button" } : state switch
            {
                SkinButtonState.Hover => new[] { "button_hover", "button" },
                SkinButtonState.Pressed => new[] { "button_pressed", "button" },
                SkinButtonState.Disabled => new[] { "button_disabled", "button" },
                _ => new[] { "button" },
            });
        foreach (string key in keys)
            if (TrySlice(batch, key, r)) return true;
        return false;
    }
}
