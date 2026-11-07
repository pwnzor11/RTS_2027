// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Sidebar;
using Rts.Game.Veterancy;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Card;

/// <summary>
/// Reads the rules and the sim and fills a <see cref="CardModel"/> for the current selection (M11-04): a single own unit, a group, or
/// one own building. Called every frame; it allocates a little but touches no sim state. Button key texts come from
/// <see cref="GameKeys.Active"/>, so a rebound key shows at once.
/// </summary>
public static class CardModelBuilder
{
    /// <summary>The order buttons of a unit or group, in order; Deploy only when <paramref name="deployApplies"/>.</summary>
    public static IReadOnlyList<CardButton> UnitButtons(bool deployApplies)
    {
        var list = new List<CardButton>(5)
        {
            OrderButton(GameAction.Stop, "Stop"),
            OrderButton(GameAction.Guard, "Guard"),
            OrderButton(GameAction.Scatter, "Scatter"),
            OrderButton(GameAction.AttackMove, "Attack"),
        };
        if (deployApplies) list.Add(OrderButton(GameAction.Deploy, "Deploy"));
        return list;
    }

    /// <summary>The buttons of a building: Sell and Repair.</summary>
    public static IReadOnlyList<CardButton> BuildingButtons() => new[]
    {
        new CardButton(CardButtonKind.Sell, default, "Sell", ""),
        new CardButton(CardButtonKind.Repair, default, "Repair", ""),
    };

    private static CardButton OrderButton(GameAction action, string label) =>
        new(CardButtonKind.Order, action, label, KeyNames.Of(GameKeys.Active.Get(action)));

    /// <summary>Builds the model; <see cref="CardModel.Empty"/> when nothing of the local player is selected.</summary>
    /// <param name="source">The sim source (rules, sim, current snapshot).</param>
    /// <param name="selection">The selected units.</param>
    /// <param name="player">The local player.</param>
    /// <param name="building">The selected own building, or <see cref="EntityId.None"/>.</param>
    public static CardModel Build(SimSnapshotSource source, Selection selection, byte player, EntityId building)
    {
        SimSnapshot snap = source.Current;
        SimRules rules = source.Rules;
        if (selection.Count == 0)
        {
            if (building.IsNone) return CardModel.Empty;
            foreach (BuildingView b in snap.Buildings)
                if (!b.Ghost && b.Id == building && b.Player == player) return ForBuilding(source, b);
            return CardModel.Empty;
        }

        var cameos = new List<CardCameo>(selection.Count);
        UnitView? single = null;
        bool deploy = false;
        foreach (UnitKey key in selection.Units)
        {
            UnitView? found = null;
            foreach (UnitView u in snap.Units)
                if (u.Player == player && UnitKey.Of(u) == key) { found = u; break; }
            if (found is not UnitView unit) continue;
            single = unit;
            (string id, int max) = IdAndMax(rules, unit);
            cameos.Add(new CardCameo(key, id, unit.Health, max));
            if (M3Orders.DeployModeOf(rules, key.Kind, unit.TypeIndex) != DeployMode.None) deploy = true;
        }

        if (cameos.Count == 0) return CardModel.Empty;
        if (cameos.Count == 1 && single is UnitView only) return ForUnit(source, only, cameos[0], deploy);

        IReadOnlyList<CardCameo> ordered = CardGrouping.Order(cameos);
        IReadOnlyList<CardGroup> groups = CardGrouping.Groups(ordered, CameoNames.Display);
        return new CardModel
        {
            Kind = CardKind.Multi,
            Title = CardGrouping.Header(ordered.Count, groups),
            Cameos = ordered,
            Groups = groups,
            Buttons = UnitButtons(deploy),
        };
    }

    private static (string Id, int Max) IdAndMax(SimRules rules, UnitView u) =>
        u.Kind == UnitKind.Vehicle
            ? (rules.Vehicles[u.TypeIndex].Id, rules.Vehicles[u.TypeIndex].MaxHealth)
            : (rules.Infantry[u.TypeIndex].Id, rules.Infantry[u.TypeIndex].MaxHealth);

    private static CardModel ForUnit(SimSnapshotSource source, UnitView unit, CardCameo cameo, bool deploy)
    {
        SimRules r = source.Rules;
        UnitKey key = cameo.Key;
        int weapon, speed, sight, scale;
        bool canRank;
        ArmorClass armor;
        Faction faction;
        if (unit.Kind == UnitKind.Vehicle)
        {
            VehicleType t = r.Vehicles[unit.TypeIndex];
            weapon = t.PrimaryWeapon; speed = t.Speed; sight = t.Sight; armor = t.Armor; faction = t.Faction;
            scale = t.VetThresholdPermille; canRank = t.CanRank;
        }
        else
        {
            InfantryType t = r.Infantry[unit.TypeIndex];
            weapon = t.PrimaryWeapon; speed = t.Speed; sight = t.Sight; armor = t.Armor; faction = t.Faction;
            scale = t.VetThresholdPermille; canRank = t.CanRank;
        }

        var stats = new List<CardStat>(5);
        if ((uint)weapon < (uint)r.Weapons.Count)
        {
            WeaponType w = r.Weapons[weapon];
            stats.Add(new CardStat("Damage", w.Damage.ToString(CultureInfo.InvariantCulture)));
            stats.Add(new CardStat("Range", CardGrouping.Tenths(w.Range * 10 / 256)));
        }
        else
        {
            stats.Add(new CardStat("Damage", "-"));
            stats.Add(new CardStat("Range", "-"));
        }

        stats.Add(new CardStat("Speed", CardGrouping.Tenths(speed * SimConstants.TicksPerSecond * 10 / 256)));
        stats.Add(new CardStat("Armour", CardGrouping.ArmourText(armor)));
        stats.Add(new CardStat("Sight", sight.ToString(CultureInfo.InvariantCulture)));

        string? rank = null;
        if (r.Veterancy.Enabled)
        {
            int rk = source.Sim.RankOf(key.Kind, key.Id.Index);
            int kill = source.Sim.KillValueOf(key.Kind, key.Id.Index);
            int next = VeterancyText.NextThreshold(r.Veterancy.Thresholds, rk, scale, canRank);
            IReadOnlyList<string> vet = VeterancyText.InfoLines(own: true, rk, kill, next);
            if (vet.Count > 0) rank = string.Join("  -  ", vet);
        }

        var lines = new List<string>();
        if (unit.Kind == UnitKind.Vehicle && r.Vehicles[unit.TypeIndex].AircraftSlots > 0
            && source.Sim.ViewFor(SimSnapshotSource.LocalPlayer).OwnCarrierHangar(key.Id, out int docked, out int slots))
            lines.Add(CarrierReadout.Text(docked, slots));

        return new CardModel
        {
            Kind = CardKind.Unit,
            Title = CameoNames.Display(cameo.RulesId),
            SubLine = (unit.Kind == UnitKind.Vehicle ? "Vehicle" : "Infantry") + " - " + FactionText(faction),
            PortraitId = cameo.RulesId,
            Health = unit.Health,
            MaxHealth = cameo.MaxHealth,
            RankLine = rank,
            Stats = stats,
            Lines = lines,
            Cameos = new[] { cameo },
            Buttons = UnitButtons(deploy),
        };
    }

    private static string FactionText(Faction f) => f switch
    {
        Faction.LeftBehind => "Left Behind",
        Faction.Expedition => "Expedition",
        _ => f.ToString(),
    };

    private static CardModel ForBuilding(SimSnapshotSource source, BuildingView b)
    {
        SimRules r = source.Rules;
        BuildingType t = r.Buildings[b.TypeIndex];
        PlayerHud hud = source.Sim.HudFor(b.Player);
        var lines = new List<string>(4);

        string power = t.Power > 0 ? "Power +" + t.Power.ToString(CultureInfo.InvariantCulture)
            : t.Power < 0 ? "Power use " + (-t.Power).ToString(CultureInfo.InvariantCulture) : "Power none";
        lines.Add(power + "   Base " + hud.PowerOut.ToString(CultureInfo.InvariantCulture) + " / " + hud.PowerUse.ToString(CultureInfo.InvariantCulture));

        if (t.Produces is ProductionCategory lane)
        {
            lines.Add(ProductionText(r, hud, lane));
            CellCoord? rally = source.Sim.Production.RallyCell(b.Player, lane);
            lines.Add(rally is null ? "Rally point: not set (right-click the ground)" : "Rally point: set");
        }

        if (b.Occupants > 0) lines.Add("Garrison " + b.Occupants.ToString(CultureInfo.InvariantCulture));

        return new CardModel
        {
            Kind = CardKind.Building,
            Title = CameoNames.Display(t.Id),
            SubLine = "Structure - " + FactionText(t.Faction),
            PortraitId = t.Id,
            Health = b.Health,
            MaxHealth = t.MaxHealth,
            Lines = lines,
            Buttons = BuildingButtons(),
        };
    }

    /// <summary>The one-line production summary of a lane: "Building Rifleman 45%  +2 queued", "Ready: X (place it)" or "Idle".</summary>
    public static string ProductionText(SimRules r, PlayerHud hud, ProductionCategory lane)
    {
        foreach (QueueView q in hud.Queues)
        {
            if (q.Category != lane) continue;
            if (q.Items.Count > 0)
            {
                QueueItemView head = q.Items[0];
                string name = CameoNames.Display(IdOfType(r, lane, head.TypeIndex));
                string text = "Building " + name + " " + (head.ProgressPermille / 10).ToString(CultureInfo.InvariantCulture) + "%";
                if (q.Items.Count > 1) text += "  +" + (q.Items.Count - 1).ToString(CultureInfo.InvariantCulture) + " queued";
                return text;
            }

            if (q.ReadyTypeIndex >= 0) return "Ready: " + CameoNames.Display(IdOfType(r, lane, q.ReadyTypeIndex));
        }

        return "Idle";
    }

    private static string IdOfType(SimRules r, ProductionCategory c, int index) => c switch
    {
        ProductionCategory.Infantry => (uint)index < (uint)r.Infantry.Count ? r.Infantry[index].Id : "?",
        ProductionCategory.Vehicle or ProductionCategory.Aircraft => (uint)index < (uint)r.Vehicles.Count ? r.Vehicles[index].Id : "?",
        _ => (uint)index < (uint)r.Buildings.Count ? r.Buildings[index].Id : "?",
    };
}
