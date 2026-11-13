// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Picks the infantry death animation by cause (M10-14b). Sheets may carry <c>die_blast</c>, <c>die_burn</c> and <c>die_rad</c> besides
/// <c>die</c>; one facing, drawn facing east. A missing cause state falls back to <c>die</c>. The sim does not report which warhead
/// killed a unit, so <see cref="CauseOf"/> is a lookup the view can apply once it knows the killing warhead.
/// </summary>
public static class DeathStates
{
    /// <summary>The plain death state.</summary>
    public const string Die = "die";
    /// <summary>Death by explosion.</summary>
    public const string DieBlast = "die_blast";
    /// <summary>Death by fire.</summary>
    public const string DieBurn = "die_burn";
    /// <summary>Death by radiation or ash.</summary>
    public const string DieRad = "die_rad";

    /// <summary>Classifies a warhead: radiation (contaminates, or an ash / radiation id) is Rad, fire (flame, incendiary, napalm, burn) is Burn, anything with splash is Blast, other hits Unknown.</summary>
    /// <param name="warhead">The killing warhead, or null.</param>
    public static DeathCause CauseOf(WarheadType? warhead)
    {
        if (warhead is null) return DeathCause.Unknown;
        string id = warhead.Id;
        if (warhead.Contaminate > 0 || Has(id, "_rad") || Has(id, "_ash") || Has(id, "radiation")) return DeathCause.Rad;
        if (Has(id, "flame") || Has(id, "incend") || Has(id, "napalm") || Has(id, "burn") || Has(id, "fire")) return DeathCause.Burn;
        if (warhead.Splash > 0 || Has(id, "blast") || Has(id, "bomb") || Has(id, "shell") || Has(id, "rocket") || id.EndsWith("_he", StringComparison.Ordinal)) return DeathCause.Blast;
        return DeathCause.Unknown;
    }

    /// <summary>The state name for a cause, before the fallback.</summary>
    /// <param name="cause">What killed it.</param>
    public static string WantedState(DeathCause cause) => cause switch
    {
        DeathCause.Blast => DieBlast,
        DeathCause.Burn => DieBurn,
        DeathCause.Rad => DieRad,
        _ => Die,
    };

    /// <summary>The state to play: the cause's own when the sheet has it, else <c>die</c> (null when the sheet has no <c>die</c> either).</summary>
    /// <param name="manifest">The soldier's sprite manifest.</param>
    /// <param name="cause">What killed it.</param>
    public static string? Pick(SpriteManifest manifest, DeathCause cause)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        string wanted = WantedState(cause);
        if (SpriteStates.HasState(manifest, wanted)) return wanted;
        return SpriteStates.HasState(manifest, Die) ? Die : null;
    }

    /// <summary>True when the one-facing east-drawn animation is to be mirrored: the soldier faced the west half (facing 129..255, 0 = north, clockwise).</summary>
    /// <param name="facing">Facing byte.</param>
    public static bool MirrorWest(byte facing) => facing > 128;

    private static bool Has(string id, string part) => id.Contains(part, StringComparison.OrdinalIgnoreCase);
}
