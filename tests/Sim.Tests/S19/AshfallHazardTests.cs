// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S19;

/// <summary>S19 D-205 (D-028, M13-12): soldiers standing on an ashfall (or pitchblende) field cell take periodic damage reduced by
/// their rad resistance; vehicles and bare ground are unaffected. The rate is data (<c>[radiation] ashfall_*</c>).</summary>
public sealed class AshfallHazardTests
{
    private static readonly CellCoord Field = new(30, 30);

    private static S18World Field30(OverlayKind kind = OverlayKind.Ashfall)
    {
        var w = new S18World(Faction.Authority, Faction.Directorate);
        // S19 M13-12: idle soldiers walk off a field when a clean cell is within Simulation.AshStepOffRadius; a 9x9 field keeps
        // the soldiers in its middle on ash, so these tests still measure the damage.
        for (int y = Field.Y - 4; y <= Field.Y + 4; y++)
            for (int x = Field.X - 4; x <= Field.X + 4; x++)
                w.Sim.Map.SetOverlay(new CellCoord(x, y), kind, 100);
        return w;
    }

    [Fact]
    public void ShippedRate_IsTwoHp_EveryThirtyTicks()
    {
        RadiationRules r = S18World.RealRules.Radiation;
        Assert.Equal((2, 30), (r.AshfallDamage, r.AshfallIntervalTicks));
    }

    [Fact]
    public void UnprotectedSoldier_OnAshfall_LosesTwoHpPerInterval_AndBareGroundIsSafe()
    {
        var w = Field30();
        EntityId onField = w.Inf(0, "auth_rifleman", Field.X, Field.Y);
        EntityId onGround = w.Inf(0, "auth_rifleman", 40, 40);
        int max = w.InfHp(onField);
        w.Run(30 * 5);
        Assert.Equal(max - 2 * 5, w.InfHp(onField));
        Assert.Equal(max, w.InfHp(onGround));
    }

    [Fact]
    public void Pitchblende_IsTheSameResource_SoItHurtsToo()
    {
        var w = Field30(OverlayKind.Pitchblende);
        EntityId s = w.Inf(0, "auth_rifleman", Field.X, Field.Y);
        int max = w.InfHp(s);
        w.Run(60);
        Assert.True(w.InfHp(s) < max);
    }

    [Fact]
    public void RadResistance_ReducesTheDamage_AndVehiclesAreUnaffected()
    {
        var w = Field30();
        EntityId plain = w.Inf(0, "auth_rifleman", Field.X, Field.Y);
        EntityId resistant = w.Inf(1, "dir_conscript", Field.X, Field.Y, 1);
        EntityId tank = w.Veh(0, "auth_decon_truck", Field.X + 1, Field.Y);
        int rp = w.Rules.Infantry![w.I("dir_conscript")].RadResistPermille;
        Assert.True(rp > 0 && rp < 1000);
        int maxPlain = w.InfHp(plain), maxRes = w.InfHp(resistant), maxTank = w.VehHp(tank);
        w.Run(30 * 10);
        int lostPlain = maxPlain - w.InfHp(plain), lostRes = maxRes - w.InfHp(resistant);
        Assert.Equal(20, lostPlain);
        Assert.True(lostRes > 0 && lostRes < lostPlain);
        Assert.Equal(maxTank, w.VehHp(tank));
    }
}
