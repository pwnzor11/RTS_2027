using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>Guard, Attack and AttackMove mission handlers. OWNER: M1-09 (combat). Lead-created stub: idle.</summary>
internal static class CombatMissions
{
    public static int HandleGuard(Simulation sim, MobileStore store, ActorKind kind, int i) => MissionHandlers.GuardSleepTicks;
    public static int HandleAttack(Simulation sim, MobileStore store, ActorKind kind, int i) => MissionHandlers.GuardSleepTicks;
    public static int HandleAttackMove(Simulation sim, MobileStore store, ActorKind kind, int i) => MissionHandlers.GuardSleepTicks;
}
