// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
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
    /// <summary>Burrowing deployers (Bores): not army; the faction tactics deploy them into entrances. Empty for Authority.</summary>
    public readonly List<ViewActor> Bores = new();
    /// <summary>Aura carriers (Agitprop): not army; the faction tactics keep them with the wave. Empty for Authority.</summary>
    public readonly List<ViewActor> Support = new();
    /// <summary>M6-10: own packed rigs (vehicles that unpack into a building): not army, never sent on waves. Empty for Authority / Directorate.</summary>
    public readonly List<ViewActor> PackedRigs = new();
    /// <summary>M6-10: own wreck-salvaging vehicles (Haulers): not harvesters, not army; the Left Behind tactics send them to wrecks.</summary>
    public readonly List<ViewActor> Salvagers = new();
    public readonly List<ViewActor> Visible = new();
    /// <summary>M3-11: army units that retreated and guard the base (not part of waves). Filled by <see cref="Squads"/>.</summary>
    public readonly List<ViewActor> Home = new();
    /// <summary>M3-11: army units on special duty (scout, raiders); not part of waves. Filled by <see cref="Squads"/>.</summary>
    public readonly List<ViewActor> Detached = new();
    /// <summary>M4-12: own infantry sitting in a garrison (MissionKind.Garrisoned): never army, never ordered by the wave managers.</summary>
    public readonly List<ViewActor> Garrisoned = new();
    /// <summary>M7-10: own armed ships. Never army (a wave cannot sail over land); the Expedition tactics steer them. Empty for other factions.</summary>
    public readonly List<ViewActor> Fleet = new();
    /// <summary>M7-10: own Expedition vehicles that carry no gun or carry passengers (Command Ship, Landing Craft, Supply Tender, Skyhook ...): not army.</summary>
    public readonly List<ViewActor> Utility = new();
    /// <summary>M7-10: own aircraft docked in a carrier's hangar (hidden, cannot be ordered like army). Empty for other factions.</summary>
    public readonly List<ViewActor> Hangar = new();
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
        Buildings.Clear(); Army.Clear(); Harvesters.Clear(); Bores.Clear(); Support.Clear(); Garrisoned.Clear(); PackedRigs.Clear(); Salvagers.Clear(); Fleet.Clear(); Utility.Clear(); Hangar.Clear();
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
                case ActorKind.Vehicle when Rules.Vehicles[a.TypeIndex] is { UnpacksInto: >= 0, Locomotion: not Locomotion.Burrow }:
                    PackedRigs.Add(a);   // M6-10
                    break;
                case ActorKind.Vehicle when Rules.Vehicles[a.TypeIndex].SalvageRate > 0:
                    Salvagers.Add(a);   // M6-10
                    break;
                case ActorKind.Vehicle when Rules.Vehicles[a.TypeIndex].HarvestCapacity > 0:
                    Harvesters.Add(a);
                    break;
                case ActorKind.Vehicle when Rules.Vehicles[a.TypeIndex] is { DeploysInto: >= 0, Locomotion: Locomotion.Burrow }:
                    Bores.Add(a);
                    break;
                case ActorKind.Vehicle when Rules.Vehicles[a.TypeIndex].AuraRadius > 0:
                    Support.Add(a);
                    break;
                case ActorKind.Vehicle when Rules.Vehicles[a.TypeIndex] is { Faction: Faction.Expedition } && (Rules.Vehicles[a.TypeIndex].PrimaryWeapon < 0 || Rules.Vehicles[a.TypeIndex].Passengers > 0):
                    Utility.Add(a);   // M7-10
                    break;
                case ActorKind.Vehicle when Rules.Vehicles[a.TypeIndex] is { Locomotion: Locomotion.Naval }:
                    Fleet.Add(a);   // M7-10
                    break;
                case ActorKind.Vehicle when a.Underground && Rules.Vehicles[a.TypeIndex] is { Locomotion: Locomotion.Air, Faction: Faction.Expedition }:
                    Hangar.Add(a);   // M7-10
                    break;
                case ActorKind.Infantry when a.Mission == MissionKind.Garrisoned:
                    Garrisoned.Add(a);
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
                if (!(Rules.Buildings[b.TypeIndex].Wall || Rules.Buildings[b.TypeIndex].Gate)) { Yard = b.Cell; HaveYard = true; break; }
    }

    /// <summary>Stable key of an actor (slot, generation, kind).</summary>
    public static ulong KeyOf(ViewActor a) => ((ulong)a.Id.Index << 16) | a.Id.Generation | ((ulong)(int)a.Kind << 48);

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
