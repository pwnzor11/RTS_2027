// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using System.Diagnostics.CodeAnalysis;

namespace Rts.AI;

/// <summary>Maps AI ids to factories. Ids: "idle" (never acts), "skirmisher" (M1 army placeholder) and "recruit" and "tiered" (the economy AI of the Recruit framework; tiers 1-5 are its profiles, M3-11).</summary>
public static class AiRegistry
{
    /// <summary>Id of <see cref="IdleAi"/>.</summary>
    public const string IdleId = "idle";

    /// <summary>Id of <see cref="SkirmisherAi"/>.</summary>
    public const string SkirmisherId = "skirmisher";

    /// <summary>Id of <see cref="RecruitAi"/>.</summary>
    public const string RecruitId = "recruit";

    /// <summary>Id of the tier-aware Recruit framework AI; identical to "recruit" (the profile's flags decide the capabilities).</summary>
    public const string TieredId = "tiered";

    private static readonly SortedDictionary<string, Func<DifficultyProfile, IAiPlayer>> Factories =
        new(StringComparer.Ordinal)
        {
            [IdleId] = _ => new IdleAi(),
            [SkirmisherId] = p => new SkirmisherAi(p),
            [RecruitId] = p => new RecruitAi(p),
            [TieredId] = p => new RecruitAi(p, TieredId),
        };

    /// <summary>Registered ids in ordinal order.</summary>
    public static IReadOnlyList<string> Ids => Factories.Keys.ToList();

    /// <summary>True if the id is registered.</summary>
    public static bool Contains(string id) => Factories.ContainsKey(id);

    /// <summary>Creates a fresh AI for the id and profile; false if the id is unknown.</summary>
    public static bool TryCreate(string id, DifficultyProfile profile, [NotNullWhen(true)] out IAiPlayer? ai)
    {
        if (Factories.TryGetValue(id, out var factory)) { ai = factory(profile); return true; }
        ai = null;
        return false;
    }
}
