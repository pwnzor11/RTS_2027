namespace Rts.AI;

/// <summary>Outcome of one AI-vs-AI game. Winner: 0 = side A, 1 = side B, -1 = draw. ValueA / ValueB = sum of the
/// current health of everything each side still has (the tiebreak report for drawn games).</summary>
public readonly record struct AiGameResult(int Winner, int EndTick, int ValueA, int ValueB, ulong FinalHash);
