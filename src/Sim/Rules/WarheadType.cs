namespace Rts.Sim.Rules;

/// <summary>VersusPercent[(int)ArmorClass] = damage percent (integer). Splash: 0 = target only,
/// 1 = 3×3 cell block with falloff (design 02 §4). Splash hits allies when HitsAllies.</summary>
public sealed record WarheadType(string Id, IReadOnlyList<int> VersusPercent, int Splash, bool HitsAllies);
