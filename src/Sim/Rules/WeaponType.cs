namespace Rts.Sim.Rules;

/// <summary>Range in leptons; RateOfFire = ticks between shots; ProjectileSpeed leptons/tick (0 = instant hit);
/// Warhead = index into SimRules.Warheads.</summary>
public sealed record WeaponType(string Id, int Damage, int Range, int MinRange, int RateOfFire,
    int ProjectileSpeed, int Warhead);
