// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Screens;

/// <summary>
/// QOL-07 (D-313): the opt-in match rules of the skirmish setup and the LAN lobby: how many superweapon structures each player may have
/// (<see cref="SimSetup.NoSuperweaponLimit"/> = no limit, 0 = none, else that many at once) and whether the map is revealed (no fog, no shroud).
/// They flow into <see cref="SimSetup.SuperweaponLimit"/> and <see cref="SimSetup.RevealMap"/>. <see cref="Default"/> is today's game exactly.
/// </summary>
/// <param name="SuperweaponLimit">Superweapon structures per player: -1 unlimited, 0 none, 1 or 2 each.</param>
/// <param name="RevealMap">True: every cell is visible to everyone all game.</param>
public readonly record struct MatchRules(int SuperweaponLimit = SimSetup.NoSuperweaponLimit, bool RevealMap = false)
{
    /// <summary>
    /// A record struct's implicit parameterless constructor zeroes every field and ignores the optional-parameter defaults, so a bare
    /// <c>new()</c> used to mean "no superweapons" (limit 0). This explicit one makes it the default game (unlimited, fog on).
    /// </summary>
    public MatchRules() : this(SimSetup.NoSuperweaponLimit, false) { }

    /// <summary>The limits the setup row cycles through, in order.</summary>
    public static readonly IReadOnlyList<int> LimitSteps = new[] { SimSetup.NoSuperweaponLimit, 0, 1, 2 };

    /// <summary>No limit, fog and shroud as usual: the game as it always was.</summary>
    public static MatchRules Default => new();

    /// <summary>True when both rules are at their defaults.</summary>
    public bool IsDefault => SuperweaponLimit == SimSetup.NoSuperweaponLimit && !RevealMap;

    /// <summary>True when the values are inside what the sim accepts.</summary>
    public bool IsValid => SuperweaponLimit >= SimSetup.NoSuperweaponLimit && SuperweaponLimit <= SimSetup.MaxSuperweaponLimit;

    /// <summary>The next limit after <paramref name="limit"/> in <see cref="LimitSteps"/> (wraps; a value that is not a step starts at the first).</summary>
    public static int StepLimit(int limit, int delta)
    {
        int at = 0;
        for (int i = 0; i < LimitSteps.Count; i++) if (LimitSteps[i] == limit) at = i;
        int n = LimitSteps.Count;
        return LimitSteps[((at + delta) % n + n) % n];
    }

    /// <summary>Row text: "Superweapons: unlimited", "Superweapons: none" or "Superweapons: 1 each".</summary>
    public static string LimitLabel(int limit) => limit < 0 ? "Superweapons: unlimited" : limit == 0 ? "Superweapons: none" : $"Superweapons: {limit} each";

    /// <summary>Row text: "Map: fog of war" or "Map: revealed".</summary>
    public static string RevealLabel(bool reveal) => reveal ? "Map: revealed" : "Map: fog of war";

    /// <summary>This rule set with the limit stepped.</summary>
    public MatchRules StepSuperweapons(int delta) => this with { SuperweaponLimit = StepLimit(SuperweaponLimit, delta) };

    /// <summary>This rule set with the reveal switch flipped.</summary>
    public MatchRules ToggleReveal() => this with { RevealMap = !RevealMap };
}
