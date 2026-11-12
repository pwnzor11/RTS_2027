// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>
/// S19 D-205 (M13-15): how a soldier or vehicle behaves when it has no order. Set by <see cref="CommandKind.SetStance"/>, saved and
/// hashed per unit (default <see cref="Aggressive"/>, which writes no bytes). Values are persisted; append only.
/// </summary>
public enum UnitStance : byte
{
    /// <summary>Today's behaviour: acquire any visible enemy in weapon range and chase it up to the unit's sight range from its post.</summary>
    Aggressive = 0,
    /// <summary>Acquire only inside weapon range and chase no further than <see cref="Simulation.DefensiveChaseCells"/> cells from the
    /// post, then walk back to it. While attack-moving, a target that leaves weapon range is dropped and the march resumes.</summary>
    Defensive = 1,
    /// <summary>Never fires on its own; fires only at what it is ordered to attack (Attack, AttackGround).</summary>
    HoldFire = 2,
}
