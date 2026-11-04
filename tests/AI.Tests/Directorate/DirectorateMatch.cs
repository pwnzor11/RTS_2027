// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tunnels;

namespace AI.Tests.Directorate;

/// <summary>What a Directorate-vs-Authority game showed (read off the sim and the AI counters).</summary>
internal sealed record DirectorateOutcome(int Winner, int EndTick, ulong Hash, int HatchesBuilt, int PeakEntrances, int TransitUnits,
    int DigOrders, int TunnelOrders, int Structures, int Scrapers, int Harvested, int RadMoves, int Waves, int MaxTunnelUnits, int Income, int Unplaceable);

/// <summary>Plays one headless game with a Directorate Recruit in <c>slot 0</c> (or the slot given) against another side, keeping hold of the AI objects.</summary>
internal static class DirectorateMatch
{
    public static readonly string Hatch = "dir_hatch";

    public static DirectorateOutcome Play(MapData map, ulong seed, int ticks, Faction otherFaction = Faction.Authority, bool dirInSlot0 = true,
        int dirTier = 0, int otherTier = 0, string otherAi = "recruit")
    {
        SimRules rules = TestWorld.Rules.Sim;
        byte dir = dirInSlot0 ? (byte)0 : (byte)1, other = (byte)(1 - dir);
        var factions = new Faction[2];
        factions[dir] = Faction.Directorate; factions[other] = otherFaction;
        var sim = new Simulation(new SimSetup(seed, map, 2, rules, TestWorld.Rules.RulesHash) { Factions = factions });
        SkirmishStart.Apply(sim);
        var driver = new AiDriver(seed);
        var dirAi = new RecruitAi(TestWorld.Tiers[dirTier]);
        driver.Add(dir, dirAi, TestWorld.Tiers[dirTier]);
        AiRegistry.TryCreate(otherAi, TestWorld.Tiers[otherTier], out IAiPlayer? foe);
        driver.Add(other, foe!, TestWorld.Tiers[otherTier]);

        int hatchType = SimRules.IndexOf(rules.Buildings, Hatch, b => b.Id);
        var hatches = new HashSet<ulong>();
        int peakEntrances = 0, maxTunnel = 0, winner = -1, income = 0, prevFunds = sim.Players.Cash[dir] + sim.Players.Stored[dir];
        var inTunnel = new HashSet<ulong>();
        var pending = new List<Command>();
        for (int t = 0; t < ticks; t++)
        {
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            if (sim.Tick % 4 != 0) continue;
            int funds = sim.Players.Cash[dir] + sim.Players.Stored[dir];
            if (funds > prevFunds) income += funds - prevFunds;   // credits only rise from harvesting here
            prevFunds = funds;
            int entrances = 0, tunnelNow = 0;
            for (int i = 1; i < sim.Buildings.Capacity; i++)
            {
                if (!sim.Buildings.Slots.IsIndexAlive(i) || sim.Buildings.Player[i] != dir) continue;
                if (sim.Buildings.TypeIndex[i] == hatchType) hatches.Add(((ulong)i << 16) | (uint)sim.Buildings.Slots.GenerationOf(i));
                if (rules.Buildings[sim.Buildings.TypeIndex[i]].Entrance != TunnelSize.None) entrances++;
            }
            peakEntrances = Math.Max(peakEntrances, entrances);
            for (int i = 1; i < sim.Infantry.Capacity; i++)
                if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == dir && sim.Tunnels.InfantryLayer[i] == ActorLayer.Tunnel)
                { inTunnel.Add(((ulong)i << 16) | (uint)sim.Infantry.Slots.GenerationOf(i)); tunnelNow++; }
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == dir && sim.Tunnels.VehicleLayer[i] == ActorLayer.Tunnel)
                { inTunnel.Add(((ulong)i << 16) | (uint)sim.Units.Slots.GenerationOf(i) | (1UL << 40)); tunnelNow++; }
            maxTunnel = Math.Max(maxTunnel, tunnelNow);
            if (sim.Players.Defeated[dir] || sim.Players.Defeated[other])
            {
                winner = sim.Players.Defeated[other] && !sim.Players.Defeated[dir] ? 0 : sim.Players.Defeated[dir] && !sim.Players.Defeated[other] ? 1 : -1;
                break;
            }
        }
        int scrapers = 0;
        int scraperType = SimRules.IndexOf(rules.Vehicles, "dir_scraper", v => v.Id);
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == dir && sim.Units.TypeIndex[i] == scraperType) scrapers++;
        int structures = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == dir) structures++;
        return new DirectorateOutcome(winner, sim.Tick, sim.ComputeStateHash(), hatches.Count, peakEntrances, inTunnel.Count,
            dirAi.HatchesDug, dirAi.TunnelOrders, structures, scrapers, sim.Players.Cash[dir] + sim.Players.Stored[dir], dirAi.RadiationMoves,
            dirAi.Waves, maxTunnel, income, dirAi.Unplaceable);
    }
}
