// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;

namespace Rts.Game.Render;

/// <summary>
/// Chooses which animation state and frame a sprite shows. Pure functions of the snapshot data and the manifest, so
/// they can be tested without a graphics device. Nothing here is simulation state: the age of a building and the
/// moving flag are renderer memory (see <see cref="RenderMemory"/>).
/// </summary>
public static class SpriteStates
{
    /// <summary>Resting state of every sprite.</summary>
    public const string Idle = "idle";

    /// <summary>Building state played once while it is placed.</summary>
    public const string Build = "build";

    /// <summary>Building state shown below half health, when the manifest has one.</summary>
    public const string Damaged = "damaged";

    /// <summary>
    /// Building state of an open gate (M3-21), when the manifest has one. The art for it is still to come: a gate without this state
    /// draws its closed sprite with a placeholder overlay (see <see cref="BuildingRenderer"/>), and the day the state appears in the
    /// manifest the frames are used with no code change.
    /// </summary>
    public const string Open = "open";

    /// <summary>Infantry state while moving, when the manifest has one.</summary>
    public const string Walk = "walk";

    /// <summary>Health fraction under which a building shows its damaged state (half health, exclusive).</summary>
    public const int DamagedDivisor = 2;

    /// <summary>
    /// The frame for a building. During the build window (the first <c>frames * ticks_per_frame</c> ticks after it
    /// appears) the build frames play once and then hold on the last one. After that the building shows
    /// <c>damaged</c> when health is below half of <paramref name="maxHealth"/> (and the manifest has that state),
    /// otherwise <c>idle</c>, looping by tick. Ghosts always pass <paramref name="ghost"/> = true and get idle.
    /// </summary>
    /// <param name="manifest">The building's manifest.</param>
    /// <param name="ageTicks">Ticks since the building first appeared in a snapshot. A large value means it is settled.</param>
    /// <param name="health">Health as the renderer sees it (last-seen for ghosts).</param>
    /// <param name="maxHealth">Maximum health from the rules. Zero or less disables the damaged state.</param>
    /// <param name="ghost">Whether this is a last-seen ghost.</param>
    /// <param name="tick">Simulation tick, used to loop the resting state.</param>
    /// <param name="open">M3-21: the building is an open gate. Shows the <c>open</c> state when the manifest has one (after the build window).</param>
    /// <returns>The sheet frame index.</returns>
    public static int BuildingFrame(SpriteManifest manifest, int ageTicks, int health, int maxHealth, bool ghost, int tick, bool open = false)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!ghost && HasState(manifest, Build))
        {
            SpriteState build = manifest.State(Build);
            int window = build.Frames * build.TicksPerFrame;
            if (ageTicks >= 0 && ageTicks < window)
            {
                int step = Math.Min(ageTicks / build.TicksPerFrame, build.Frames - 1);
                return build.First + step;
            }
        }

        if (open && !ghost && HasState(manifest, Open)) return manifest.FrameIndex(Open, 0, Math.Max(0, tick));

        bool damaged = !ghost && maxHealth > 0 && health * DamagedDivisor < maxHealth;
        string state = damaged && HasState(manifest, Damaged) ? Damaged : Idle;
        return manifest.FrameIndex(state, 0, Math.Max(0, tick));
    }

    /// <summary>
    /// The frame for a unit. Vehicles always use idle (the snapshot does not carry a mission, so no harvest state).
    /// Infantry use walk while moving, when the manifest has it, and idle otherwise. Facing rounds to the state's
    /// facing count inside the manifest.
    /// </summary>
    /// <param name="manifest">The unit's manifest.</param>
    /// <param name="infantry">True for infantry, false for vehicles.</param>
    /// <param name="moving">Whether the unit's position changed since the previous snapshot.</param>
    /// <param name="facing">Facing byte, 0 = north, clockwise.</param>
    /// <param name="tick">Simulation tick.</param>
    public static int UnitFrame(SpriteManifest manifest, bool infantry, bool moving, byte facing, int tick)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        string state = infantry && moving && HasState(manifest, Walk) ? Walk : Idle;
        return manifest.FrameIndex(state, facing, Math.Max(0, tick));
    }

    /// <summary>Whether a manifest has a state with this name.</summary>
    public static bool HasState(SpriteManifest manifest, string name)
    {
        foreach (SpriteState state in manifest.States)
        {
            if (string.Equals(state.Name, name, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
