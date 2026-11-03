namespace Rts.Sim.Missions;

/// <summary>Per-mission sub-state, reset to Init when a mission commences. Persisted; append only.</summary>
public enum MissionStatus : byte { Init = 0, Turning = 1, Driving = 2, Waiting = 3 }
