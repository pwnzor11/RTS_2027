// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>Per-decision snapshot shared by the Recruit managers: the view, the HUD, the own actors split into
/// buildings / army / harvesters, owned structure counts and the enemies currently in vision. Rebuilt every Think.</summary>
internal sealed class RecruitKnowledge
{
    public PlayerView View;
    public PlayerHud Hud = null!;
    public SimRules Rules = null!;
    public readonly List<ViewActor> Own = new();
    public readonly List<ViewActor> Buildings = new();
    public readonly List<ViewActor> Army = new();
    public readonly List<ViewActor> Harvesters = new();
    public readonly List<ViewActor> Visible = new();
    public int[] BuildingCount = Array.Empty<int>();
    public CellCoord Yard;
    public bool HaveYard;

    public void Refresh(PlayerView view, string yardId)
    {
        View = view;
        Hud = view.Hud;
        Rules = view.Rules;
        view.CopyOwn(Own);
        view.CopyVisible(Visible);
        Buildings.Clear(); Army.Clear(); Harvesters.Clear();
        if (BuildingCount.Length != Rules.Buildings.Count) BuildingCount = new int[Rules.Buildings.Count];
        Array.Clear(BuildingCount);
        HaveYard = false;
        foreach (ViewActor a in Own)
        {
            switch (a.Kind)
            {
                case ActorKind.Building:
                    Buildings.Add(a);
                    BuildingCount[a.TypeIndex]++;
                    break;
                case ActorKind.Vehicle when Rules.Vehicles[a.TypeIndex].HarvestCapacity > 0:
                    Harvesters.Add(a);
                    break;
                default:
                    Army.Add(a);
                    break;
            }
        }
        // anchor of the base: the construction yard if there is one, else the first non-wall building
        foreach (ViewActor b in Buildings)
            if (Rules.Buildings[b.TypeIndex].Id == yardId) { Yard = b.Cell; HaveYard = true; break; }
        if (!HaveYard)
            foreach (ViewActor b in Buildings)
                if (!Rules.Buildings[b.TypeIndex].Wall) { Yard = b.Cell; HaveYard = true; break; }
    }

    public bool Buildable(ProductionCategory c, int type)
    {
        foreach (var (cat, t) in Hud.Buildable)
            if (cat == c && t == type) return true;
        return false;
    }

    public QueueView Queue(ProductionCategory c)
    {
        foreach (QueueView q in Hud.Queues)
            if (q.Category == c) return q;
        throw new InvalidOperationException("HUD has no queue for " + c);
    }
}
