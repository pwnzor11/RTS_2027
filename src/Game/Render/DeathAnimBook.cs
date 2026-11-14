// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;

namespace Rts.Game.Render;

/// <summary>One soldier playing its death state: the death, the state picked for the cause, the first tick and how long it runs.</summary>
/// <param name="Death">The death record (position, facing, owner, type, cause).</param>
/// <param name="State">The sprite state to play (<c>die</c>, <c>die_blast</c>, <c>die_burn</c> or <c>die_rad</c>).</param>
/// <param name="Start">First tick of the animation.</param>
/// <param name="Duration">Ticks it runs: frames times ticks per frame.</param>
public readonly record struct DeathAnim(DeathRecord Death, string State, int Start, int Duration);

/// <summary>
/// The death animations playing now (M10-14b). View only, on the game clock (snapshot ticks): a soldier whose sheet has a death state plays it
/// once at its last position; when it ends, the record is handed back so the caller leaves the infantry mark exactly as without art.
/// </summary>
public sealed class DeathAnimBook
{
    private readonly List<DeathAnim> _items = new();

    /// <summary>The animations playing.</summary>
    public IReadOnlyList<DeathAnim> Items => _items;

    /// <summary>Starts an animation for a soldier's death when the sheet has a death state; false (nothing added) otherwise.</summary>
    /// <param name="death">The death.</param>
    /// <param name="manifest">The soldier's sprite manifest.</param>
    public bool Add(DeathRecord death, SpriteManifest manifest)
    {
        string? state = DeathStates.Pick(manifest, death.Cause);
        if (state is null) return false;
        SpriteState s = manifest.State(state);
        _items.Add(new DeathAnim(death, state, death.Tick, Math.Max(1, s.Frames * s.TicksPerFrame)));
        return true;
    }

    /// <summary>The sheet frame to draw at <paramref name="tick"/>, or -1 when the animation has not started or is over.</summary>
    /// <param name="manifest">The soldier's sprite manifest.</param>
    /// <param name="anim">The animation.</param>
    /// <param name="tick">Current snapshot tick.</param>
    public static int Frame(SpriteManifest manifest, DeathAnim anim, int tick)
    {
        if (tick < anim.Start || tick - anim.Start >= anim.Duration) return -1;
        return manifest.FrameIndex(anim.State, 0, tick - anim.Start);
    }

    /// <summary>Removes animations that are over (their deaths go to <paramref name="finished"/>, which is cleared first) or that start in the future (the clock went back: dropped).</summary>
    /// <param name="tick">Current snapshot tick.</param>
    /// <param name="finished">Receives the deaths whose animation just ended.</param>
    public void Release(int tick, List<DeathRecord> finished)
    {
        finished.Clear();
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            DeathAnim a = _items[i];
            if (tick < a.Start) _items.RemoveAt(i);
        }

        for (int i = 0; i < _items.Count; i++)
        {
            DeathAnim a = _items[i];
            if (tick - a.Start < a.Duration) continue;
            finished.Add(a.Death);
            _items.RemoveAt(i--);
        }
    }

    /// <summary>Forgets everything.</summary>
    public void Clear() => _items.Clear();
}
