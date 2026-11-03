// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;

namespace Rts.Game.Render;

/// <summary>
/// Frame choice for effect sprites (state "play"). Pure functions, so they are tested without a graphics device.
/// </summary>
public static class FxFrames
{
    /// <summary>The state every effect sprite plays.</summary>
    public const string Play = "play";

    /// <summary>
    /// Frame of an effect anchored on a building. It is a function of the simulation tick and the anchor's phase:
    /// <c>tick / ticks_per_frame + phase</c>, wrapped for a looping effect and held on the last frame for a one-shot.
    /// </summary>
    /// <param name="effect">Effect manifest.</param>
    /// <param name="tick">Simulation tick, zero or more.</param>
    /// <param name="phase">Frame offset from the anchor.</param>
    public static int Anchored(SpriteManifest effect, int tick, int phase)
    {
        ArgumentNullException.ThrowIfNull(effect);
        SpriteState s = effect.State(Play);
        int step = Math.Max(0, tick) / s.TicksPerFrame + Math.Max(0, phase);
        return s.First + (s.Loop ? step % s.Frames : Math.Min(step, s.Frames - 1));
    }

    /// <summary>
    /// Frame of a spawned effect, counted from its spawn tick. Returns -1 once a one-shot has played out (the caller
    /// then drops it); a looping effect never finishes.
    /// </summary>
    /// <param name="effect">Effect manifest.</param>
    /// <param name="spawnTick">Tick the effect was spawned.</param>
    /// <param name="tick">Current tick.</param>
    public static int Spawned(SpriteManifest effect, int spawnTick, int tick)
    {
        ArgumentNullException.ThrowIfNull(effect);
        SpriteState s = effect.State(Play);
        int age = tick - spawnTick;
        if (age < 0) return -1;
        int step = age / s.TicksPerFrame;
        if (s.Loop) return s.First + step % s.Frames;
        return step >= s.Frames ? -1 : s.First + step;
    }

    /// <summary>The name of the state that owns a sheet frame, or null when no state does.</summary>
    /// <param name="manifest">Manifest.</param>
    /// <param name="frame">Sheet frame index.</param>
    public static string? StateOf(SpriteManifest manifest, int frame)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        foreach (SpriteState s in manifest.States)
        {
            if (frame >= s.First && frame < s.First + s.CellCount) return s.Name;
        }
        return null;
    }
}
