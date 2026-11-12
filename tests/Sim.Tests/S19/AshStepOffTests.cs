// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S19;

/// <summary>S19 M13-12 (owner option a, ASSUMED - confirm): an idle soldier on an ash field walks to the nearest clean cell within
/// <see cref="Simulation.AshStepOffRadius"/>; a soldier ordered onto the field stays; with no clean cell in reach it stays.</summary>
public sealed class AshStepOffTests
{
    private static S18World WithField(int cx, int cy, int half, OverlayKind kind = OverlayKind.Ashfall)
    {
        var w = new S18World(Faction.Authority, Faction.Directorate);
        for (int y = cy - half; y <= cy + half; y++)
            for (int x = cx - half; x <= cx + half; x++)
                w.Sim.Map.SetOverlay(new CellCoord(x, y), kind, 100);
        return w;
    }

    private static CellCoord CellOf(S18World w, EntityId id) => w.Sim.Infantry.Position[id.Index].ToCell();

    private static bool OnField(S18World w, EntityId id) => OverlayKinds.IsResource(w.Sim.Map.OverlayAt(CellOf(w, id)));

    [Theory]
    [InlineData(OverlayKind.Ashfall)]
    [InlineData(OverlayKind.Pitchblende)]
    public void An_idle_soldier_walks_off_a_small_field(OverlayKind kind)
    {
        S18World w = WithField(30, 30, 1, kind);   // 3x3 field
        EntityId s = w.Inf(0, "auth_rifleman", 30, 30);
        int max = w.InfHp(s);
        w.Run(90);
        Assert.False(OnField(w, s), $"still on the field at {CellOf(w, s)}");
        Assert.Equal(2, Math.Max(Math.Abs(CellOf(w, s).X - 30), Math.Abs(CellOf(w, s).Y - 30)));   // the nearest clean ring (just outside the 3x3)
        Assert.True(w.InfHp(s) >= max - 2 * 2, "left before taking more than two hits");
    }

    [Fact]
    public void A_soldier_ordered_onto_the_field_stays()
    {
        S18World w = WithField(30, 30, 1);
        EntityId s = w.Inf(0, "auth_rifleman", 26, 30);
        w.Run(300, w.Cmd(0, CommandKind.Move, s, default, new CellCoord(30, 30), (int)ActorKind.Infantry));
        Assert.Equal(new CellCoord(30, 30), CellOf(w, s));
        Assert.True(OnField(w, s));
    }

    [Fact]
    public void With_no_clean_cell_in_reach_the_soldier_stays()
    {
        S18World w = WithField(30, 30, Simulation.AshStepOffRadius + 1);
        EntityId s = w.Inf(0, "auth_rifleman", 30, 30);
        w.Run(120);
        Assert.Equal(new CellCoord(30, 30), CellOf(w, s));
    }

    [Fact]
    public void A_soldier_off_any_field_is_left_alone()
    {
        S18World w = WithField(30, 30, 1);
        EntityId s = w.Inf(0, "auth_rifleman", 40, 40);
        w.Run(120);
        Assert.Equal(new CellCoord(40, 40), CellOf(w, s));
    }

    [Fact]
    public void The_step_off_is_deterministic()
    {
        CellCoord Run()
        {
            S18World w = WithField(30, 30, 1);
            EntityId s = w.Inf(0, "auth_rifleman", 30, 30);
            w.Run(90);
            return CellOf(w, s);
        }
        Assert.Equal(Run(), Run());
    }
}
