// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Naval;
using Rts.Sim.Rules;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S19;

/// <summary>S19 D-205 (M13-11, FACTION-LE-05 / LE-06): a dived missile sub that fires is seen and can be shot for
/// <see cref="Simulation.SubRevealTicks"/> (5 s); a surfaced sub uses ship_light armour, only a dived hidden one uses submerged.</summary>
public sealed class SubRevealTests
{
    private static (S18World W, EntityId Sub, EntityId Turret) Coast()
    {
        var w = new S18World(Faction.Expedition, Faction.Authority, waterFromX: 40, keepAlive: false);
        w.Place(1, "auth_power", 30, 26);
        w.Place(1, "auth_power", 30, 34);
        EntityId turret = w.Place(1, "auth_turret", 39, 30);
        EntityId sub = w.Veh(0, "exp_missile_sub", 43, 30);
        return (w, sub, turret);
    }

    [Fact]
    public void RevealConstant_IsFiveSeconds() => Assert.Equal(100, Simulation.SubRevealTicks);

    [Fact]
    public void Armour_IsShipLightSurfaced_AndSubmergedOnlyWhileDivedAndUnseen()
    {
        var (w, sub, _) = Coast();
        Assert.Equal(ArmorClass.ShipLight, w.Rules.Vehicles[w.V("exp_missile_sub")].Armor);
        Assert.Equal(ArmorClass.ShipLight, w.Sim.ArmorAt(ActorKind.Vehicle, sub.Index));
        w.Run(1, w.Cmd(0, CommandKind.Deploy, sub, default, default, (int)ActorKind.Vehicle));
        Assert.Equal(ArmorClass.Submerged, w.Sim.ArmorAt(ActorKind.Vehicle, sub.Index));
    }

    [Fact]
    public void DivedSub_IsHidden_UntilItFires_ThenSeenForFiveSeconds_ThenHiddenAgain()
    {
        var (w, sub, turret) = Coast();
        w.Run(1, w.Cmd(0, CommandKind.Deploy, sub, default, default, (int)ActorKind.Vehicle));
        var subRef = new ActorRef(ActorKind.Vehicle, sub);
        Assert.False(w.Sim.IsValidTarget(1, subRef));
        w.Run(1, w.Attack(0, ActorKind.Vehicle, sub, ActorKind.Building, turret));
        int fired = -1;
        for (int t = 0; t < 400 && fired < 0; t++) { w.Run(1); if (w.Sim.Naval.RevealLeft[sub.Index] > 0) fired = t; }
        Assert.True(fired >= 0, "the sub should have fired");
        Assert.True(w.Sim.Naval.Submerged[sub.Index]);            // still dived
        Assert.True(w.Sim.IsValidTarget(1, subRef));              // but the defenders can answer
        Assert.False(w.Sim.IsHiddenFrom(1, ActorKind.Vehicle, sub.Index));
        Assert.Equal(ArmorClass.ShipLight, w.Sim.ArmorAt(ActorKind.Vehicle, sub.Index));
    }

    [Fact]
    public void Reveal_RunsOutAfterFiveSeconds_WhenNothingIsInRangeToShoot()
    {
        var (w, sub, _) = Coast();
        EntityId far = w.Veh(0, "exp_missile_sub", 60, 10);
        w.Run(1, w.Cmd(0, CommandKind.Deploy, far, default, default, (int)ActorKind.Vehicle));
        w.Sim.Naval.RevealLeft[far.Index] = Simulation.SubRevealTicks;
        w.Run(Simulation.SubRevealTicks - 1);
        Assert.Equal(1, w.Sim.Naval.RevealLeft[far.Index]);
        w.Run(1);
        Assert.Equal(0, w.Sim.Naval.RevealLeft[far.Index]);
        Assert.False(w.Sim.IsValidTarget(1, new ActorRef(ActorKind.Vehicle, far)));
    }

    [Fact]
    public void RevealTimer_SurvivesSaveAndLoad()
    {
        var (w, sub, _) = Coast();
        w.Sim.Naval.RevealLeft[sub.Index] = 37;
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) w.Sim.Naval.Write(bw);
        ms.Position = 0;
        var copy = new NavalState(w.Sim.Setup);
        using (var br = new BinaryReader(ms)) copy.Read(br);
        Assert.Equal(37, copy.RevealLeft[sub.Index]);
    }
}
