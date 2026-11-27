// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui;

/// <summary>
/// Keyed UI strings (SUP-4) with their English text. The project has no string table yet (<c>VeterancyText</c> and <c>LeftBehindText</c>
/// keep their own placeholders), so this is the one place the supply keys live. Keys are dotted lower-case ids; a placeholder is written
/// as <c>{name}</c> and filled by <see cref="Format"/>. The bitmap font is ASCII.
/// </summary>
public static class UiText
{
    /// <summary>Label of the supply counter ("Supply 34/60").</summary>
    public const string SupplyLabel = "ui.supply.label";

    /// <summary>Greyed cameo tooltip at the supply cap. Placeholder: <c>{supplyBuilding}</c>.</summary>
    public const string SupplyCapReached = "ui.supply.cap_reached";

    /// <summary>Greyed cameo tooltip at the supply cap when no supply building exists for the faction.</summary>
    public const string SupplyCapReachedPlain = "ui.supply.cap_reached_plain";

    /// <summary>Cameo of a unit at the head of a lane that is paused for supply.</summary>
    public const string SupplyWaiting = "ui.supply.waiting";

    /// <summary>M7-14: the shipment timer row ("Shipment in 1:05 +300"). Placeholders: <c>{time}</c> (m:ss) and <c>{payload}</c>.</summary>
    public const string ShipmentTimer = "ui.shipment.timer";

    /// <summary>M7-14: caption of the Dive/Surface button while the ship is surfaced (the click dives). Placeholder: <c>{key}</c> (the Deploy key, from the bindings).</summary>
    public const string DiveCaption = "ui.dive.dive";

    /// <summary>M7-14: caption of the Dive/Surface button while the ship is dived (the click surfaces). Placeholder: <c>{key}</c> (the Deploy key).</summary>
    public const string SurfaceCaption = "ui.dive.surface";

    /// <summary>S44 M-01: caption of the Lay mine button for a selected minelayer. Placeholders: <c>{key}</c> (the Deploy key) and <c>{stock}</c> (mines aboard).</summary>
    public const string MineCaption = "ui.mine.lay";

    /// <summary>S44 M-15: caption of the Drop beacon button for a selected landing craft, Skyhook, Pioneer or Combat Diver.</summary>
    public const string BeaconCaption = "ui.beacon.drop";

    /// <summary>S44 M-15: the same button while it waits for the target cell.</summary>
    public const string BeaconArmedCaption = "ui.beacon.pick";

    /// <summary>D-205: toast after a quicksave.</summary>
    public const string Quicksaved = "ui.save.quicksaved";

    /// <summary>D-205: toast when a quicksave could not be written. Placeholder: <c>{reason}</c>.</summary>
    public const string QuicksaveFailed = "ui.save.quicksave_failed";

    /// <summary>D-205: toast after an autosave.</summary>
    public const string Autosaved = "ui.save.autosaved";

    /// <summary>D-205: toast when an autosave could not be written. Placeholder: <c>{reason}</c>.</summary>
    public const string AutosaveFailed = "ui.save.autosave_failed";

    /// <summary>D-205: toast when Quickload is pressed and there is no quicksave. Placeholder: <c>{key}</c> (the Quicksave key).</summary>
    public const string NoQuicksave = "ui.save.no_quicksave";

    /// <summary>D-205: title of the Quickload confirmation list.</summary>
    public const string QuickloadTitle = "ui.save.quickload_title";

    /// <summary>D-205: the Quickload confirmation list's "yes" row.</summary>
    public const string QuickloadYes = "ui.save.quickload_yes";

    /// <summary>D-205: hint under the Quickload confirmation.</summary>
    public const string QuickloadHint = "ui.save.quickload_hint";

    /// <summary>D-205: Settings row label of the autosave interval. Placeholder: <c>{interval}</c>.</summary>
    public const string AutosaveSetting = "ui.save.autosave_setting";

    /// <summary>D-205: the card's Sell button label after the first click (the second click within 2 s sells).</summary>
    public const string SellConfirm = "ui.card.sell_confirm";

    /// <summary>D-205: the one-line hint at the start of an Expedition match. Placeholder: <c>{key}</c> (the Deploy key).</summary>
    public const string ExpeditionStart = "ui.expedition.start_hint";

    /// <summary>D-205 (S18 UXG-07): the placement reason of a floating dock (the Anchorage) that does not touch land.</summary>
    public const string MustTouchShore = "ui.placement.must_touch_shore";

    /// <summary>M13-15: toast after the game speed changed in play. Placeholder: <c>{speed}</c> (0.5x, 0.75x, 1x, 1.5x, 2x).</summary>
    public const string SpeedToast = "ui.speed.toast";

    /// <summary>M13-15: the skirmish setup row of the game speed. Placeholder: <c>{speed}</c>.</summary>
    public const string SpeedSetup = "ui.speed.setup";

    /// <summary>M13-15: HUD line while the game speed is not 1x. Placeholder: <c>{speed}</c>.</summary>
    public const string SpeedHud = "ui.speed.hud";

    /// <summary>M13-15: alert line when a research finished.</summary>
    public const string AlertUpgradeComplete = "ui.alert.upgrade_complete";

    /// <summary>M13-15: alert line when a unit lane stood empty for a while.</summary>
    public const string AlertProductionIdle = "ui.alert.production_idle";

    /// <summary>M13-15: alert line when production is stalled.</summary>
    public const string AlertProductionBlocked = "ui.alert.production_blocked";

    /// <summary>M13-15: alert line when a harvester has been idle for a while.</summary>
    public const string AlertHarvesterIdle = "ui.alert.harvester_idle";

    /// <summary>M13-15: the idle-harvester button. Placeholders: <c>{count}</c>, <c>{key}</c>.</summary>
    public const string IdleHarvesterButton = "ui.idle.harvester_button";

    /// <summary>M13-15: the idle-factory button. Placeholders: <c>{count}</c>, <c>{key}</c>.</summary>
    public const string IdleFactoryButton = "ui.idle.factory_button";

    /// <summary>M13-15: toast when the idle-harvester key finds none.</summary>
    public const string NoIdleHarvester = "ui.idle.no_harvester";

    /// <summary>M13-15: toast when the idle-factory key finds none.</summary>
    public const string NoIdleFactory = "ui.idle.no_factory";

    /// <summary>M13-15: cameo tooltip, repeat production is off. Placeholder: <c>{key}</c> (the modifier, "Alt").</summary>
    public const string RepeatHint = "ui.repeat.hint";

    /// <summary>M13-15: cameo tooltip, repeat production is on.</summary>
    public const string RepeatOnHint = "ui.repeat.on_hint";

    /// <summary>M13-15: the loop marker drawn on a repeating cameo.</summary>
    public const string RepeatMarker = "ui.repeat.marker";

    /// <summary>M13-15: tooltip line "Strong vs: ...". Placeholder: <c>{classes}</c>.</summary>
    public const string StrongVs = "ui.matchup.strong";

    /// <summary>M13-15: tooltip line "Weak vs: ...". Placeholder: <c>{classes}</c>.</summary>
    public const string WeakVs = "ui.matchup.weak";

    /// <summary>M13-15: toast when the weapon range rings are switched on or off. Placeholder: <c>{state}</c> (on / off).</summary>
    public const string RangeRingsToast = "ui.rings.toast";

    /// <summary>M13-15: card button and toast name of the aggressive stance.</summary>
    public const string StanceAggressive = "ui.stance.aggressive";

    /// <summary>M13-15: card button and toast name of the defensive stance.</summary>
    public const string StanceDefensive = "ui.stance.defensive";

    /// <summary>M13-15: card button and toast name of the hold-fire stance.</summary>
    public const string StanceHoldFire = "ui.stance.hold_fire";

    /// <summary>QOL-17: toast when the auto type-groups toggle changes. Placeholder: <c>{state}</c> (on / off).</summary>
    public const string AutoTypeGroupsToast = "ui.groups.auto_toast";

    /// <summary>U-02: toast when the select-army key finds no combat unit (the selection is kept).</summary>
    public const string NoArmySelection = "ui.select.no_army";

    /// <summary>U-02: toast after Ctrl+number sets a group. Placeholders: <c>{number}</c> (1..9) and <c>{count}</c> (units or structures, two or more).</summary>
    public const string GroupSetToast = "ui.groups.set_toast";

    /// <summary>U-02: toast after Ctrl+number sets a group of one. Placeholder: <c>{number}</c> (1..9).</summary>
    public const string GroupSetOneToast = "ui.groups.set_one_toast";

    /// <summary>QOL-05: toast when the production-building key finds none.</summary>
    public const string NoProductionBuilding = "ui.production.none";

    /// <summary>M13-15: skirmish setup, caption of the button that cycles a slot's handicap (50, 75, 100 percent of production speed and income).</summary>
    public const string OptHandicapButton = "ui.setup.handicap";

    /// <summary>M13-15: skirmish setup, caption of the button that cycles a slot's starting credits preset.</summary>
    public const string OptCreditsButton = "ui.setup.credits";

    /// <summary>M13-15: skirmish setup, caption of the button that toggles a slot's start units (base only / small army).</summary>
    public const string OptStartButton = "ui.setup.start_units";

    /// <summary>M13-15: skirmish setup, hint under the panel while an options row is highlighted.</summary>
    public const string OptHint = "ui.setup.options_hint";

    /// <summary>Pause-menu entry and Archive entry that open the tech-tree viewer.</summary>
    public const string TechTreeMenu = "ui.techtree.menu";

    /// <summary>Title of the tech-tree overlay. Placeholder: <c>{faction}</c>.</summary>
    public const string TechTreeTitle = "ui.techtree.title";

    /// <summary>Key line of the tech-tree overlay. Placeholder: <c>{key}</c> (the Tech tree key).</summary>
    public const string TechTreeHint = "ui.techtree.hint";

    /// <summary>U-12: status word, a structure you own or an upgrade you have researched.</summary>
    public const string TechTreeBuilt = "ui.techtree.built";

    /// <summary>U-12: status word, you can queue it now.</summary>
    public const string TechTreeAvailable = "ui.techtree.available";

    /// <summary>U-12: status word, a prerequisite is missing.</summary>
    public const string TechTreeLocked = "ui.techtree.locked";

    /// <summary>U-12: status word, the mission does not allow it.</summary>
    public const string TechTreeMissionLocked = "ui.techtree.mission_locked";

    /// <summary>U-12: tooltip line. Placeholder: <c>{cost}</c> (credits).</summary>
    public const string TechTreeCost = "ui.techtree.cost";

    /// <summary>U-12: tooltip line. Placeholder: <c>{time}</c> (seconds, such as 12s).</summary>
    public const string TechTreeTime = "ui.techtree.time";

    /// <summary>U-12: tooltip line. Placeholder: <c>{list}</c> (names, comma separated; one of each group is enough).</summary>
    public const string TechTreeNeeds = "ui.techtree.needs";

    /// <summary>U-12: tooltip line for a node without prerequisites.</summary>
    public const string TechTreeNeedsNothing = "ui.techtree.needs_nothing";

    /// <summary>U-12: tooltip line. Placeholder: <c>{list}</c> (names, comma separated).</summary>
    public const string TechTreeUnlocks = "ui.techtree.unlocks";

    /// <summary>U-12: tooltip line for a node that nothing needs.</summary>
    public const string TechTreeUnlocksNothing = "ui.techtree.unlocks_nothing";

    /// <summary>U-12: tooltip kind.</summary>
    public const string TechTreeKindStructure = "ui.techtree.kind.structure";

    /// <summary>U-12: tooltip kind.</summary>
    public const string TechTreeKindDefence = "ui.techtree.kind.defence";

    /// <summary>U-12: tooltip kind.</summary>
    public const string TechTreeKindInfantry = "ui.techtree.kind.infantry";

    /// <summary>U-12: tooltip kind.</summary>
    public const string TechTreeKindVehicle = "ui.techtree.kind.vehicle";

    /// <summary>U-12: tooltip kind.</summary>
    public const string TechTreeKindAircraft = "ui.techtree.kind.aircraft";

    /// <summary>U-12: tooltip kind.</summary>
    public const string TechTreeKindUpgrade = "ui.techtree.kind.upgrade";

    /// <summary>U-12: tooltip line naming the tech tier. Placeholder: <c>{tier}</c>.</summary>
    public const string TechTreeColumn = "ui.techtree.column";

    /// <summary>U-12: shown when a faction has no buildable types.</summary>
    public const string TechTreeEmpty = "ui.techtree.empty";

    /// <summary>U-12: faction name in the tech-tree title.</summary>
    public const string FactionAuthority = "ui.techtree.faction.authority";

    /// <summary>U-12: faction name in the tech-tree title.</summary>
    public const string FactionDirectorate = "ui.techtree.faction.directorate";

    /// <summary>U-12: faction name in the tech-tree title.</summary>
    public const string FactionLeftBehind = "ui.techtree.faction.left_behind";

    /// <summary>U-12: faction name in the tech-tree title.</summary>
    public const string FactionExpedition = "ui.techtree.faction.expedition";

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        [SupplyLabel] = "Supply",
        [SupplyCapReached] = "Supply cap reached. Build more {supplyBuilding}.",
        [SupplyCapReachedPlain] = "Supply cap reached.",
        [SupplyWaiting] = "Waiting for supply",
        [DiveCaption] = "Dive ({key})",
        [SurfaceCaption] = "Surface ({key})",
        [MineCaption] = "Lay mine ({key}) {stock} left",
        [BeaconCaption] = "Drop beacon",
        [BeaconArmedCaption] = "Click a soft land cell",
        [ShipmentTimer] = "Shipment in {time} +{payload}",
        [Quicksaved] = "Quicksaved.",
        [QuicksaveFailed] = "Quicksave failed: {reason}",
        [Autosaved] = "Autosaved.",
        [AutosaveFailed] = "Autosave failed: {reason}",
        [NoQuicksave] = "No quicksave yet. Press {key} to make one.",
        [QuickloadTitle] = "Load the quicksave?",
        [QuickloadYes] = "Yes, load the quicksave",
        [QuickloadHint] = "Progress since the quicksave is lost.",
        [AutosaveSetting] = "Autosave: {interval}",
        [ExpeditionStart] = "Your Command Ship is at the centre of the view. Select it and press {key} to land.",
        [MustTouchShore] = "Must touch the shore",
        [SellConfirm] = "Confirm",
        [SpeedToast] = "Game speed {speed}",
        [SpeedSetup] = "Game speed: {speed}",
        [SpeedHud] = "SPEED {speed}",
        [AlertUpgradeComplete] = "Upgrade complete",
        [AlertProductionIdle] = "Production idle",
        [AlertProductionBlocked] = "Production blocked",
        [AlertHarvesterIdle] = "Harvester idle",
        [IdleHarvesterButton] = "IDLE HARVESTER {count} ({key})",
        [IdleFactoryButton] = "IDLE FACTORY {count} ({key})",
        [NoIdleHarvester] = "No idle harvester",
        [NoIdleFactory] = "No idle factory",
        [RepeatHint] = "{key}+click: repeat production",
        [RepeatOnHint] = "Repeating: {key}+click to stop, right click to cancel",
        [RepeatMarker] = "LOOP",
        [StrongVs] = "Strong vs: {classes}",
        [WeakVs] = "Weak vs: {classes}",
        [RangeRingsToast] = "Weapon range rings {state}",
        [StanceAggressive] = "Aggro",
        [StanceDefensive] = "Defend",
        [StanceHoldFire] = "Hold",
        [AutoTypeGroupsToast] = "Control groups by type {state}",
        [NoArmySelection] = "No army units to select",
        [GroupSetToast] = "Group {number} set ({count} units)",
        [GroupSetOneToast] = "Group {number} set (1 unit)",
        [NoProductionBuilding] = "No production building",
        [OptHandicapButton] = "HC",
        [OptCreditsButton] = "Cash",
        [OptStartButton] = "Army",
        [OptHint] = "(HC handicap, Cash start credits, Army start units)",
        [TechTreeMenu] = "Tech tree",
        [TechTreeTitle] = "TECH TREE - {faction}",
        [TechTreeHint] = "Wheel / arrows: scroll   Hover: details   {key} or Esc: close",
        [TechTreeBuilt] = "Built",
        [TechTreeAvailable] = "Available",
        [TechTreeLocked] = "Locked",
        [TechTreeMissionLocked] = "Locked in this mission",
        [TechTreeCost] = "Cost: {cost}",
        [TechTreeTime] = "Build time: {time}",
        [TechTreeNeeds] = "Needs: {list}",
        [TechTreeNeedsNothing] = "Needs: nothing",
        [TechTreeUnlocks] = "Unlocks: {list}",
        [TechTreeUnlocksNothing] = "Unlocks: nothing",
        [TechTreeKindStructure] = "Structure",
        [TechTreeKindDefence] = "Defence",
        [TechTreeKindInfantry] = "Infantry",
        [TechTreeKindVehicle] = "Vehicle",
        [TechTreeKindAircraft] = "Aircraft",
        [TechTreeKindUpgrade] = "Upgrade",
        [TechTreeColumn] = "Tier {tier}",
        [TechTreeEmpty] = "No tech tree for this faction.",
        [FactionAuthority] = "Authority",
        [FactionDirectorate] = "Directorate",
        [FactionLeftBehind] = "Left Behind",
        [FactionExpedition] = "Expedition",
    };

    /// <summary>The English text of a key, or the key itself when it is unknown.</summary>
    public static string Get(string key) => English.TryGetValue(key, out string? text) ? text : key;

    /// <summary>The text of a key with one <c>{name}</c> placeholder replaced by <paramref name="value"/>.</summary>
    public static string Format(string key, string name, string value) => Get(key).Replace("{" + name + "}", value, StringComparison.Ordinal);
}
