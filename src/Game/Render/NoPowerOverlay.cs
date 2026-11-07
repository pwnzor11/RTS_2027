// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// The blinking "no power" marker (D-170, M11-11): drawn over a building the sim has stopped for lack of power. The test is the
/// sim's own hold-fire rule (Simulation.Combat.cs): the building needs power and its owner's power band is 1 or worse.
/// OWNER: M11-11; called by SceneRenderer.
/// </summary>
public static class NoPowerOverlay
{
    /// <summary>Seconds the icon stays on, then the same again off.</summary>
    public const float HalfPeriodSeconds = 0.5f;

    /// <summary>
    /// Whether a building shows the marker: it needs power, its owner's power band is 1 or worse (power short), it is one of the
    /// buildings the viewer sees as its own, and it is standing (not a ghost, not destroyed).
    /// </summary>
    /// <param name="needsPower">The building type's NeedsPower.</param>
    /// <param name="powerBand">The owner's <c>PowerBand</c> (0 = full power, 1..3 = short).</param>
    /// <param name="isLocalPlayer">The building is the viewer's own (or the view is omniscient).</param>
    /// <param name="isAlive">Standing: not a last-seen ghost and with health left.</param>
    public static bool ShouldShow(bool needsPower, int powerBand, bool isLocalPlayer, bool isAlive) =>
        needsPower && powerBand >= 1 && isLocalPlayer && isAlive;

    /// <summary>
    /// Blink phase at a render-clock time in seconds: on for the first half period, off for the second, repeating
    /// (on 0.5 s, off 0.5 s).
    /// </summary>
    public static bool BlinkOn(float seconds)
    {
        long phase = (long)MathF.Floor(seconds / HalfPeriodSeconds);
        return (phase & 1) == 0;
    }
}
