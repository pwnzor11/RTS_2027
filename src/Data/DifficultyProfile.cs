namespace Rts.Data;

/// <summary>
/// One difficulty tier's capability profile, loaded from data/ai/difficulty.toml (design 02 §7). Integers only.
/// Tier 1..5 = Recruit, Regular, Veteran, Elite, Commissar. Fields an AI does not use yet are reserved.
/// </summary>
public sealed record DifficultyProfile(
    string Id, int Tier, int DecisionIntervalTicks, int CommandDelayTicks, int ScoutInfoAgeTicks, int AttackThreshold,
    bool FocusFire, int RetreatHealthPct, int ExpansionTick, bool Superweapons, bool MultiFront,
    IReadOnlyList<string> BuildRepertoire)
{
    /// <summary>A neutral profile for tests and for AIs run without a data file.</summary>
    public static DifficultyProfile Default { get; } =
        new("regular", 2, 30, 1, 600, 12, false, 0, 0, false, false, Array.Empty<string>());
}
