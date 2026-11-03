namespace Rts.Sim.World;

/// <summary>Unit mission. Values are persisted in saves; append only.</summary>
public enum UnitMission : byte { Idle = 0, Move = 1, Guard = 2 }
