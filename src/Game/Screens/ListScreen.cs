// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>One row of a <see cref="ListScreen"/>.</summary>
/// <param name="Label">The row text.</param>
/// <param name="Detail">A second line shown while the row is selected.</param>
/// <param name="Activate">Runs on Enter or a click, or null.</param>
/// <param name="Preview">M10-18: makes the map picture shown beside the list while the row is selected (the editor's Open list), or null for no picture.</param>
public sealed record ListRow(string Label, string Detail, Action? Activate, Func<Rts.Game.Ui.MapPreviewImage?>? Preview = null);

/// <summary>
/// A paged list of rows (saves, replays) with a detail line for the selected row and Back. With no rows it shows
/// <c>emptyText</c> and the hint instead. Left and Right change page.
/// </summary>
public sealed class ListScreen : IScreen
{
    /// <summary>Rows per page.</summary>
    public const int PageSize = 8;

    private readonly GameServices _s;
    private readonly IReadOnlyList<ListRow> _rows;
    private readonly MenuPanel _panel;
    private Point _mouse;
    private int _page;
    private readonly MapPreviewView _previewView = new();
    private ListRow? _previewRow;
    private MapPreviewImage? _previewImage;

    /// <summary>Creates the screen.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="title">Panel title.</param>
    /// <param name="rows">The rows, in display order.</param>
    /// <param name="emptyText">Shown when <paramref name="rows"/> is empty.</param>
    /// <param name="hint">Always shown under the list (what the rows are, or what is missing).</param>
    public ListScreen(GameServices services, string title, IReadOnlyList<ListRow> rows, string emptyText, string hint)
    {
        _s = services;
        _rows = rows;
        var items = new List<MenuItem>();
        int slots = Math.Min(PageSize, rows.Count);
        for (int slot = 0; slot < slots; slot++)
        {
            int captured = slot;
            items.Add(new MenuItem(() => RowAt(captured)?.Label ?? "", () => ActivateRow(RowAt(captured))));
        }
        // The empty message, the detail line and the hint only inform: dim, never highlighted, not choices (S18 UX-LM-12).
        if (rows.Count == 0) items.Add(new MenuItem(() => emptyText) { Info = true });
        else items.Add(new MenuItem(() => SelectedDetail()) { Info = true });
        if (rows.Count > PageSize)
        {
            items.Add(new MenuItem(() => $"Page {_page + 1} of {Pages}", null, StepPage,
                new MenuButton("<", () => StepPage(-1)), new MenuButton(">", () => StepPage(1))));
        }
        if (hint.Length > 0) items.Add(new MenuItem(() => hint) { Info = true });
        items.Add(new MenuItem(() => "Back", Back));
        _panel = new MenuPanel(title, items);
    }

    /// <summary>The panel (tests).</summary>
    public MenuPanel Panel => _panel;

    private int Pages => Math.Max(1, (_rows.Count + PageSize - 1) / PageSize);

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
    }

    /// <inheritdoc />
    public void Exit() => _previewView.Dispose();

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        if (input.ConsumePressed(Keys.Escape))
        {
            Back();
            return;
        }

        int before = _panel.Selected;
        _panel.Update(input, _s.Viewport, _s.UiScale);
        if (_panel.Selected != before) MenuSounds.Play(_s.Audio, MenuSounds.Move);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
        DrawPreview(sb);
    }

    // M10-18: the selected row's map picture, to the right of the panel (or the left) when there is room.
    private void DrawPreview(SpriteBatch sb)
    {
        ListRow? row = _panel.Selected < Math.Min(PageSize, _rows.Count) ? RowAt(_panel.Selected) : null;
        if (row?.Preview is null) return;
        if (!ReferenceEquals(row, _previewRow))
        {
            _previewRow = row;
            _previewImage = row.Preview();
        }

        Rectangle panel = _panel.PanelRect;
        UiUnit scale = _s.UiScale;
        int side = 48 * scale, gap = 3 * scale;
        int x;
        if (_s.Viewport.Width - panel.Right - gap >= side) x = panel.Right + gap;
        else if (panel.X - gap >= side) x = panel.X - gap - side;
        else return;
        _previewView.Draw(sb, _s.Pixel, new Rectangle(x, panel.Y, side, side), _previewImage, scale);
    }

    private ListRow? RowAt(int slot)
    {
        int index = _page * PageSize + slot;
        return index < _rows.Count ? _rows[index] : null;
    }

    // The select sound for a row that does something, the deny sound for one that does not.
    private void ActivateRow(ListRow? row)
    {
        MenuSounds.Play(_s.Audio, row?.Activate is null ? MenuSounds.Deny : MenuSounds.Select);
        row?.Activate?.Invoke();
    }

    private string SelectedDetail() =>
        _panel is not null && _panel.Selected < Math.Min(PageSize, _rows.Count) ? RowAt(_panel.Selected)?.Detail ?? "" : "";

    private void StepPage(int d) => _page = (_page + d + Pages) % Pages;

    private void Back()
    {
        MenuSounds.Play(_s.Audio, MenuSounds.Back);
        _s.Screens.Pop();
    }
}
