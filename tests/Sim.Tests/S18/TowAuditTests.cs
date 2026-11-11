// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, FACTION-LE-08: the Tow Truck's <c>tow_bonus_permille = 1000</c> was the neutral value (D-205 raised it to 1750), so a packed rig next to a
/// Tow Truck now moves faster than one without.</summary>
public sealed class TowAuditTests
{
    /// <summary>A packed Mother Rig drives east for 20 ticks, with or without a Tow Truck parked one cell away. Returns its x in leptons
    /// and the speed multiplier the sim applied.</summary>
    private static (int X, int SpeedPermille) Drive(bool withTow)
    {
        var w = new S18World(Faction.LeftBehind, Faction.Authority);
        EntityId rig = w.Veh(0, "lb_mother_rig_packed", 20, 20);
        if (withTow) w.Veh(0, "lb_tow", 20, 21);
        w.Run(11);   // one aura pass has run
        w.Run(1, w.Cmd(0, CommandKind.Move, rig, default, new CellCoord(40, 20), (int)ActorKind.Vehicle));
        w.Run(20);
        return (w.Sim.Units.Position[rig.Index].X, w.Sim.Effects.SpeedPermille(ActorKind.Vehicle, rig.Index));
    }

    [Fact]
    public void TowedPackedRig_MovesFasterThanAnUntowedOne()
    {
        // D-205 / LE-08: tow_bonus_permille 1000 -> 1750.
        Assert.Equal(1750, S18World.RealRules.Vehicles.Single(v => v.Id == "lb_tow").TowBonusPermille);
        var (alone, speedAlone) = Drive(false);
        var (towed, speedTowed) = Drive(true);
        Assert.True(alone > 20 * 256 + 128, "the rig did move");
        Assert.Equal(1000, speedAlone);
        Assert.Equal(1750, speedTowed);
        Assert.True(towed > alone);
    }

    [Fact]
    public void Desired_TowedPackedRig_MovesFaster()
    {
        var (alone, _) = Drive(false);
        var (towed, speed) = Drive(true);
        Assert.True(speed > 1000);
        Assert.True(towed > alone);
    }
}
