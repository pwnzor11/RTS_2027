// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Ui.Objectives;

/// <summary>A short eased camera move (M11-05): about 0.6 s from the current centre to a target centre. Pure; the screen applies the result.</summary>
public sealed class CameraPan
{
    /// <summary>Default length of the move in seconds.</summary>
    public const float DefaultSeconds = 0.6f;

    private Vector2 _from, _to;
    private float _elapsed, _length;

    /// <summary>True while a move is running.</summary>
    public bool Active { get; private set; }

    /// <summary>Starts a move from <paramref name="from"/> to <paramref name="to"/> (world pixels, view centres).</summary>
    public void Start(Vector2 from, Vector2 to, float seconds = DefaultSeconds)
    {
        _from = from; _to = to; _elapsed = 0f; _length = Math.Max(0.01f, seconds); Active = true;
    }

    /// <summary>Stops the move (the player took the camera).</summary>
    public void Cancel() => Active = false;

    /// <summary>Advances by <paramref name="dt"/> seconds and returns the view centre for this frame, or null when no move runs.</summary>
    public Vector2? Step(float dt)
    {
        if (!Active) return null;
        _elapsed += Math.Max(0f, dt);
        float k = GuidanceMath.Ease(_elapsed / _length);
        if (_elapsed >= _length) Active = false;
        return Vector2.Lerp(_from, _to, k);
    }
}
