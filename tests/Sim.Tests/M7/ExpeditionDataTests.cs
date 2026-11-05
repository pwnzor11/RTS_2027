// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M7;

/// <summary>
/// Checks the shipped Expedition data (M7-08, docs/design/07_expedition.md, 11_m7_plan §2.3): every exp_ id loads with the faction,
/// prerequisites resolve, tiers 1 to 3 are reachable from the start kit, the provision store gives supply, the depot is a beachhead
/// source, water buildings carry masks, and the faction stays unplayable. Reads the real data.
/// </summary>
public sealed class ExpeditionDataTests
{
    private static RuleSet LoadReal()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!);
    }

    [Fact]
    public void EveryExpId_LoadsWithTheFaction()
    {
        SimRules s = LoadReal().Sim;

        Assert.All(s.Vehicles.Where(v => v.Id.StartsWith("exp_", StringComparison.Ordinal)), v => Assert.Equal(Faction.Expedition, v.Faction));
        Assert.All(s.Infantry.Where(i => i.Id.StartsWith("exp_", StringComparison.Ordinal)), i => Assert.Equal(Faction.Expedition, i.Faction));
        Assert.All(s.Buildings.Where(b => b.Id.StartsWith("exp_", StringComparison.Ordinal)), b => Assert.Equal(Faction.Expedition, b.Faction));
        Assert.All(s.Upgrades.Where(u => u.Id.StartsWith("exp_", StringComparison.Ordinal)), u => Assert.Equal(Faction.Expedition, u.Faction));
        Assert.Equal(5, s.Infantry.Count(i => i.Faction == Faction.Expedition));
        Assert.Equal(21, s.Vehicles.Count(v => v.Faction == Faction.Expedition));   // 7 hover/land + 9 ships + 5 aircraft
        Assert.Equal(20, s.Buildings.Count(b => b.Faction == Faction.Expedition));
        Assert.Equal(8, s.Upgrades.Count(u => u.Faction == Faction.Expedition));
        Assert.DoesNotContain(s.Vehicles, v => v.Id is "exp_skyfreighter");
        Assert.DoesNotContain(s.Buildings, b => b.Id is "exp_airhead");
    }

    [Fact]
    public void StaysUnplayable()
    {
        Assert.Equal(3, RuleCounts.PlayableFactions);
        Assert.False(RuleCounts.IsPlayable(Faction.Expedition));
        Assert.DoesNotContain(Faction.Expedition, RuleCounts.PlayableOrder);
    }

    [Fact]
    public void TiersOneToThree_AreReachableFromTheStartKit()
    {
        SimRules s = LoadReal().Sim;
        FactionStartKit kit = s.StartKits[(int)Faction.Expedition];

        // the kit holds no conyard; the Command Ship deploys into the depot
        Assert.Equal("", kit.Conyard);
        VehicleType ship = s.Vehicles.Single(v => v.Id == "exp_command_ship");
        Assert.Contains("exp_command_ship", kit.Units);
        BuildingType depot = s.Buildings[ship.DeploysInto];
        Assert.Equal("exp_landing_depot", depot.Id);
        foreach (string id in kit.Units)
            Assert.True(s.Vehicles.Any(v => v.Id == id) || s.Infantry.Any(i => i.Id == id), id);

        var owned = new HashSet<string>(StringComparer.Ordinal) { depot.Id };
        foreach (string p in depot.Provides) owned.Add(p);
        BuildingType[] mine = s.Buildings.Where(b => b.Faction == Faction.Expedition && b.Buildable).ToArray();
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (BuildingType b in mine)
            {
                if (owned.Contains(b.Id) || !b.Prerequisites.All(owned.Contains)) continue;
                owned.Add(b.Id);
                foreach (string p in b.Provides) owned.Add(p);
                grew = true;
            }
        }

        foreach (BuildingType b in mine) Assert.True(owned.Contains(b.Id), b.Id + " is unreachable");
        Assert.Equal(new[] { 1, 2, 3 }, mine.Select(b => b.Tier).Distinct().OrderBy(t => t).ToArray());
        Assert.Equal("exp_tier2", kit.Tier2Provides);
        Assert.Equal("exp_tier3", kit.Tier3Provides);
        Assert.Contains("exp_tier2", owned);
        Assert.Contains("exp_tier3", owned);
        foreach (VehicleType v in s.Vehicles.Where(v => v.Faction == Faction.Expedition && v.Id != "exp_command_ship"))
            Assert.All(v.Prerequisites, p => Assert.True(owned.Contains(p), v.Id + " needs " + p));
        foreach (InfantryType i in s.Infantry.Where(i => i.Faction == Faction.Expedition))
            Assert.All(i.Prerequisites, p => Assert.True(owned.Contains(p), i.Id + " needs " + p));
        foreach (UpgradeType u in s.Upgrades.Where(u => u.Faction == Faction.Expedition))
            Assert.All(u.Prerequisites, p => Assert.True(owned.Contains(p), u.Id + " needs " + p));
        // tier sugar: a tier-3 unit needs the tier-3 gate, a tier-2 unit the tier-2 gate
        Assert.Contains("exp_tier3", s.Vehicles.Single(v => v.Id == "exp_cruiser").Prerequisites.Concat(s.Buildings.Single(b => b.Id == "exp_drydock").Prerequisites));
        Assert.Contains("exp_tier2", s.Vehicles.Single(v => v.Id == "exp_glide_tank").Prerequisites);
    }

    [Fact]
    public void ProvisionStore_GivesSupply_AndDepotIsABeachheadSource()
    {
        SimRules s = LoadReal().Sim;
        BuildingType store = s.Buildings.Single(b => b.Id == "exp_provision_store");
        Assert.Equal(10, store.SupplyProvided);
        Assert.Equal(5, store.SupplyUnstocked);
        Assert.Equal(150, store.Cost);
        Assert.Equal(20, s.Buildings.Single(b => b.Id == "exp_landing_depot").SupplyProvided);

        BuildingType depot = s.Buildings.Single(b => b.Id == "exp_landing_depot");
        Assert.True(depot.BeachheadSource);
        Assert.Equal(10, depot.BeachheadRadius);
        Assert.False(depot.Buildable);
        Assert.Equal(8, s.Buildings.Single(b => b.Id == "exp_quay").BeachheadRadius);
        Assert.Equal(8, s.Buildings.Single(b => b.Id == "exp_shipyard").BeachheadRadius);
        Assert.Equal(6, s.Buildings.Single(b => b.Id == "exp_beacon").BeachheadRadius);
    }

    [Fact]
    public void WaterBuildings_HaveMasks_AndTheAnchorageIsTheShipmentDock()
    {
        SimRules s = LoadReal().Sim;
        string[] water = ["exp_quay", "exp_anchorage", "exp_shipyard", "exp_drydock"];
        foreach (BuildingType b in s.Buildings.Where(b => b.Faction == Faction.Expedition))
        {
            if (!water.Contains(b.Id)) { Assert.False(b.WaterBuilding, b.Id); continue; }
            Assert.True(b.WaterBuilding, b.Id);
            Assert.Equal(b.FootprintW * b.FootprintH, b.WaterMask.Count);
            Assert.Contains(true, b.WaterMask);
            Assert.Contains(false, b.WaterMask);   // straddles the shoreline: some land too
        }
        Assert.Equal(new[] { "exp_anchorage" }, s.Buildings.Where(b => b.ShipmentDock).Select(b => b.Id).ToArray());
        Assert.Equal("exp_supply_tender", LoadReal().Sim.Shipments.TenderType);
        Assert.False(s.Shipments.Enabled);
    }

    [Fact]
    public void Ships_HaveNavalFields_AndNavalWeaponsUseTheNewTargets()
    {
        SimRules s = LoadReal().Sim;
        VehicleType cruiser = s.Vehicles.Single(v => v.Id == "exp_cruiser");
        Assert.Equal(Locomotion.Naval, cruiser.Locomotion);
        Assert.True(cruiser.DeepOnly);
        Assert.Equal(2000, cruiser.DrawScalePermille);
        VehicleType sub = s.Vehicles.Single(v => v.Id == "exp_missile_sub");
        Assert.True(sub.Submerges);
        Assert.Equal(ArmorClass.Submerged, sub.Armor);
        VehicleType carrier = s.Vehicles.Single(v => v.Id == "exp_carrier");
        Assert.Equal(4, carrier.AircraftSlots);
        Assert.True(carrier.SortieTicks > 0);
        Assert.True(s.Vehicles.Single(v => v.Id == "exp_landing_craft").UnloadTicks > 0);
        Assert.Equal(WeaponTargets.Submerged, s.Weapons.Single(w => w.Id == "exp_launch_charge").Targets);
        Assert.Equal(Locomotion.Hover, s.Vehicles.Single(v => v.Id == "exp_skimmer").Locomotion);
        Assert.Equal("exp_landing_depot", s.Buildings[s.Vehicles.Single(v => v.Id == "exp_command_ship").DeploysInto].Id);
        Assert.Equal("exp_salvo_array", s.Buildings.Single(b => b.Id == "exp_salvo_array").Superweapon);
        WarheadType salvo = s.Warheads.Single(w => w.Id == "exp_salvo_array_blast");
        Assert.Equal(12, salvo.SalvoMissiles);   // D-142: the Array fires a salvo, not one flat blast
        Assert.True(salvo.SalvoDamage > 0 && salvo.SalvoRadius > 0 && salvo.SalvoWarnTicks > 0);
    }
}
