namespace Rts.Sim.Missions;

/// <summary>Actor mission. Values are persisted in saves and replays; append only.</summary>
public enum MissionKind : byte { None = 0, Sleep = 1, Guard = 2, Move = 3, Attack = 4, AttackMove = 5, Stop = 6 }
