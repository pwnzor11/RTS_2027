using System.Diagnostics.CodeAnalysis;

namespace Rts.AI;

/// <summary>Maps AI ids to factories. Ids: "idle" (never acts) and "skirmisher" (M1 placeholder).</summary>
public static class AiRegistry
{
    /// <summary>Id of <see cref="IdleAi"/>.</summary>
    public const string IdleId = "idle";

    /// <summary>Id of <see cref="SkirmisherAi"/>.</summary>
    public const string SkirmisherId = "skirmisher";

    private static readonly SortedDictionary<string, Func<DifficultyProfile, IAiPlayer>> Factories =
        new(StringComparer.Ordinal)
        {
            [IdleId] = _ => new IdleAi(),
            [SkirmisherId] = p => new SkirmisherAi(p),
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
