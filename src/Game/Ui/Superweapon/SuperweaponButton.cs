// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Superweapon;

/// <summary>Where a superweapon stands, for its sidebar button.</summary>
public enum SuperweaponState
{
    /// <summary>Charging.</summary>
    Charging,
    /// <summary>Low power has stopped the charge.</summary>
    Paused,
    /// <summary>Fully charged: a click arms targeting.</summary>
    Ready,
}

/// <summary>One superweapon button: what the sidebar shows (M3-13).</summary>
/// <param name="Building">The superweapon building the Special command names.</param>
/// <param name="Caption">Short label ("SUNRISE").</param>
/// <param name="Status">"READY", "PAUSED" or a percentage like "42%".</param>
/// <param name="ProgressPermille">Charge 0..1000.</param>
/// <param name="State">Charging, paused or ready.</param>
/// <param name="Range">Entrance range in cells, 0 = none.</param>
public readonly record struct SuperweaponButton(EntityId Building, string Caption, string Status, int ProgressPermille,
    SuperweaponState State, int Range)
{
    /// <summary>M3-21: blast radius in cells, for the targeting telegraph (0 when the view did not say).</summary>
    public int BlastRadius { get; init; }
}

/// <summary>
/// Builds the superweapon buttons from the HUD (pure, so it is unit-tested). Reads only <see cref="PlayerHud.Superweapons"/>; the sim
/// decides whether a shot is allowed.
/// </summary>
public static class SuperweaponButtons
{
    /// <summary>One button per own superweapon building, in HUD order.</summary>
    public static IReadOnlyList<SuperweaponButton> From(PlayerHud hud)
    {
        var list = new List<SuperweaponButton>(hud.Superweapons.Count);
        foreach (SuperweaponView v in hud.Superweapons)
        {
            int permille = v.Total <= 0 ? 0 : (int)Math.Min(1000L, (long)v.Charge * 1000 / v.Total);
            SuperweaponState state = v.Ready ? SuperweaponState.Ready : v.Paused ? SuperweaponState.Paused : SuperweaponState.Charging;
            string status = state switch
            {
                SuperweaponState.Ready => "READY",
                SuperweaponState.Paused => "PAUSED",
                _ => $"{permille / 10}%",
            };
            list.Add(new SuperweaponButton(v.Building, CaptionOf(v.Superweapon), status, permille, state, v.Range) { BlastRadius = v.BlastRadius });
        }
        return list;
    }

    /// <summary>Length in ticks of one pulse of a READY button's highlight.</summary>
    public const int PulseTicks = 20;

    /// <summary>M3-21: opacity 0..255 of the gold highlight on a READY button at <paramref name="tick"/>: a triangle wave between 40 and 160.</summary>
    public static byte ReadyPulse(int tick)
    {
        int phase = Math.Max(0, tick) % PulseTicks;
        int tri = phase <= PulseTicks / 2 ? phase : PulseTicks - phase;   // 0..10..0
        return (byte)(40 + 120 * tri / (PulseTicks / 2));
    }

    /// <summary>Upper-case caption from a superweapon id: the part after the faction prefix ("dir_sunrise" gives "SUNRISE").</summary>
    public static string CaptionOf(string id)
    {
        int u = id.IndexOf('_');
        return (u >= 0 && u + 1 < id.Length ? id[(u + 1)..] : id).ToUpperInvariant();
    }
}
