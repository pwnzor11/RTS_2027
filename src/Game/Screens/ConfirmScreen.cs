// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>
/// A small modal question: a title, wrapped text and two rows, the confirming one first and "Cancel" last. "Cancel" starts selected,
/// so a stray Enter or Esc never confirms. Choosing the confirm row closes the screen, then runs the action (S18 UX-LM-04).
/// </summary>
public sealed class ConfirmScreen : IScreen
{
    /// <summary>Text of the cancelling row.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>Title of the Quit to menu question.</summary>
    public const string QuitTitle = "Quit to menu?";

    /// <summary>Text of the Quit to menu question.</summary>
    public const string QuitText = "Progress since your last save is lost.";

    /// <summary>Confirm row of the Quit to menu question.</summary>
    public const string QuitYes = "Quit to menu";

    /// <summary>Title of the Restart question.</summary>
    public const string RestartTitle = "Restart?";

    /// <summary>Text of the Restart question.</summary>
    public const string RestartText = "The game starts again from the beginning.";

    /// <summary>Confirm row of the Restart question.</summary>
    public const string RestartYes = "Restart";

    /// <summary>Title of the Overwrite question.</summary>
    public const string OverwriteTitle = "Overwrite save?";

    /// <summary>Text of the Overwrite question; {0} is the slot name.</summary>
    public const string OverwriteText = "The old save \"{0}\" is replaced.";

    /// <summary>Confirm row of the Overwrite question.</summary>
    public const string OverwriteYes = "Overwrite";

    private const int WrapChars = 44;

    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private readonly Action _onCancel;
    private Point _mouse;

    /// <summary>Creates the question. <paramref name="onYes"/> runs after this screen has been popped.</summary>
    public ConfirmScreen(GameServices services, string title, string text, string yesLabel, Action onYes, Action? onCancel = null)
    {
        _s = services;
        _onCancel = onCancel ?? (() => { });
        var items = new List<MenuItem>();
        foreach (string line in TextWrap.Wrap(text, WrapChars * BitmapFont.Advance, l => l.Length * BitmapFont.Advance))
        {
            string shown = line;
            items.Add(new MenuItem(() => shown) { Info = true });
        }
        items.Add(new MenuItem(() => yesLabel, () => { MenuSounds.Play(_s.Audio, MenuSounds.Select); _s.Screens.Pop(); onYes(); }));
        items.Add(new MenuItem(() => CancelLabel, Cancel));
        _panel = new MenuPanel(title, items) { Selected = items.Count - 1 };
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => false;

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
            Cancel();
            return;
        }

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

    private void Cancel()
    {
        MenuSounds.Play(_s.Audio, MenuSounds.Back);
        _s.Screens.Pop();
        _onCancel();
    }
}
