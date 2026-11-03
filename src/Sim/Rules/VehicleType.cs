// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Sim-side vehicle type. Speed leptons/tick; TurnRate facing steps/tick; Sight in cells;
/// weapon fields are indexes into SimRules.Weapons or -1.</summary>
public sealed record VehicleType(string Id, Locomotion Locomotion, int Speed, int TurnRate, int MaxHealth,
    ArmorClass Armor, int Sight, int PrimaryWeapon, bool HasTurret, int TurretTurnRate, bool Crusher, int Cost,
    IReadOnlyList<string> Prerequisites);
