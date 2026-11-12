// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Rts.Game.Input;

/// <summary>
/// One frame of input: keyboard and mouse state plus last frame's, and the characters typed. Screens are updated top
/// down and share one frame; a screen that handles a key, button, wheel or the typed text calls the matching
/// <c>Consume</c> method, after which the query methods report nothing for it, so screens below do not react too.
/// The raw <see cref="Keyboard"/> and <see cref="Mouse"/> states are never affected by consumption.
/// </summary>
public sealed class InputFrame
{
    private static readonly char[] NoText = Array.Empty<char>();

    private readonly HashSet<Keys> _consumedKeys = new();
    private char[] _text;
    private bool _keyboardConsumed;
    private bool _leftConsumed;
    private bool _rightConsumed;
    private bool _middleConsumed;
    private bool _wheelConsumed;

    /// <summary>Creates a frame from this frame's and the previous frame's raw states.</summary>
    /// <param name="keyboard">Keyboard state now.</param>
    /// <param name="previousKeyboard">Keyboard state last frame.</param>
    /// <param name="mouse">Mouse state now.</param>
    /// <param name="previousMouse">Mouse state last frame.</param>
    /// <param name="text">Characters typed since the last frame, in order.</param>
    public InputFrame(KeyboardState keyboard, KeyboardState previousKeyboard, MouseState mouse, MouseState previousMouse,
        IEnumerable<char>? text = null)
    {
        Keyboard = keyboard;
        PreviousKeyboard = previousKeyboard;
        Mouse = mouse;
        PreviousMouse = previousMouse;
        _text = text is null ? NoText : text.ToArray();
    }

    /// <summary>Raw keyboard state now (unaffected by consumption).</summary>
    public KeyboardState Keyboard { get; }

    /// <summary>Raw keyboard state last frame.</summary>
    public KeyboardState PreviousKeyboard { get; }

    /// <summary>Raw mouse state now (unaffected by consumption).</summary>
    public MouseState Mouse { get; }

    /// <summary>Raw mouse state last frame.</summary>
    public MouseState PreviousMouse { get; }

    /// <summary>Cursor position in window pixels. Always available: hover is not consumable.</summary>
    public Point MousePosition => Mouse.Position;

    /// <summary>How far the cursor moved since last frame.</summary>
    public Point MouseDelta => Mouse.Position - PreviousMouse.Position;

    /// <summary>Characters typed this frame and not yet consumed.</summary>
    public IReadOnlyList<char> Text => _keyboardConsumed ? NoText : _text;

    /// <summary>True while either Alt key is held (fixed modifier, M13-15 repeat production).</summary>
    public bool IsAltDown() => IsDown(Keys.LeftAlt) || IsDown(Keys.RightAlt);

    /// <summary>True when the key is held and nobody consumed it.</summary>
    public bool IsDown(Keys key) => !IsKeyConsumed(key) && Keyboard.IsKeyDown(key);

    /// <summary>True only on the frame the key went down, if nobody consumed it.</summary>
    public bool Pressed(Keys key) => !IsKeyConsumed(key) && Keyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);

    /// <summary>True only on the frame the key went up, if nobody consumed it.</summary>
    public bool Released(Keys key) => !IsKeyConsumed(key) && Keyboard.IsKeyUp(key) && PreviousKeyboard.IsKeyDown(key);

    /// <summary>Marks one key as handled.</summary>
    public void Consume(Keys key) => _consumedKeys.Add(key);

    /// <summary>If the key went down this frame (and is unconsumed), consumes it and returns true.</summary>
    public bool ConsumePressed(Keys key)
    {
        if (!Pressed(key)) return false;
        Consume(key);
        return true;
    }

    /// <summary>Marks every key and the typed text as handled (a text field or console has focus).</summary>
    public void ConsumeKeyboard() => _keyboardConsumed = true;

    /// <summary>Takes the typed characters: returns them and leaves none for screens below.</summary>
    public IReadOnlyList<char> ConsumeText()
    {
        char[] taken = Text.ToArray();
        _text = NoText;
        return taken;
    }

    /// <summary>True while the left button is held and unconsumed.</summary>
    public bool LeftDown => !_leftConsumed && Mouse.LeftButton == ButtonState.Pressed;

    /// <summary>True only on the frame the left button went down, if unconsumed.</summary>
    public bool LeftPressed => !_leftConsumed && Mouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;

    /// <summary>True only on the frame the left button went up, if unconsumed.</summary>
    public bool LeftReleased => !_leftConsumed && Mouse.LeftButton == ButtonState.Released && PreviousMouse.LeftButton == ButtonState.Pressed;

    /// <summary>True while the right button is held and unconsumed.</summary>
    public bool RightDown => !_rightConsumed && Mouse.RightButton == ButtonState.Pressed;

    /// <summary>True only on the frame the right button went down, if unconsumed.</summary>
    public bool RightPressed => !_rightConsumed && Mouse.RightButton == ButtonState.Pressed && PreviousMouse.RightButton == ButtonState.Released;

    /// <summary>True while the middle button is held (this and last frame, so drags have a delta) and unconsumed.</summary>
    public bool MiddleDragging => !_middleConsumed && Mouse.MiddleButton == ButtonState.Pressed && PreviousMouse.MiddleButton == ButtonState.Pressed;

    /// <summary>Wheel movement this frame (raw units), 0 if consumed.</summary>
    public int WheelDelta => _wheelConsumed ? 0 : Mouse.ScrollWheelValue - PreviousMouse.ScrollWheelValue;

    /// <summary>Marks the left button (down, press and release) as handled.</summary>
    public void ConsumeLeft() => _leftConsumed = true;

    /// <summary>Marks the right button as handled.</summary>
    public void ConsumeRight() => _rightConsumed = true;

    /// <summary>Marks the middle button as handled.</summary>
    public void ConsumeMiddle() => _middleConsumed = true;

    /// <summary>Marks the wheel as handled.</summary>
    public void ConsumeWheel() => _wheelConsumed = true;

    /// <summary>Marks all buttons and the wheel as handled.</summary>
    public void ConsumeMouse()
    {
        _leftConsumed = true;
        _rightConsumed = true;
        _middleConsumed = true;
        _wheelConsumed = true;
    }

    /// <summary>
    /// UI first refusal: when the cursor is inside <paramref name="region"/>, consumes all mouse buttons and the wheel
    /// so the map below does not react, and returns true.
    /// </summary>
    public bool ConsumeMouseOver(IUiRegion region)
    {
        if (!region.HitTest(MousePosition)) return false;
        ConsumeMouse();
        return true;
    }

    private bool IsKeyConsumed(Keys key) => _keyboardConsumed || _consumedKeys.Contains(key);
}
