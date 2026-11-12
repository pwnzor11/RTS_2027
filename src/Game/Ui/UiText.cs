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

    /// <summary>M7-14: caption of the Dive/Surface button while the ship is surfaced (the click dives).</summary>
    public const string DiveCaption = "ui.dive.dive";

    /// <summary>M7-14: caption of the Dive/Surface button while the ship is dived (the click surfaces).</summary>
    public const string SurfaceCaption = "ui.dive.surface";

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

    /// <summary>M13-15: skirmish setup, caption of the button that cycles a slot's handicap (50, 75, 100 percent of production speed and income).</summary>
    public const string OptHandicapButton = "ui.setup.handicap";

    /// <summary>M13-15: skirmish setup, caption of the button that cycles a slot's starting credits preset.</summary>
    public const string OptCreditsButton = "ui.setup.credits";

    /// <summary>M13-15: skirmish setup, caption of the button that toggles a slot's start units (base only / small army).</summary>
    public const string OptStartButton = "ui.setup.start_units";

    /// <summary>M13-15: skirmish setup, hint under the panel while an options row is highlighted.</summary>
    public const string OptHint = "ui.setup.options_hint";

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        [SupplyLabel] = "Supply",
        [SupplyCapReached] = "Supply cap reached. Build more {supplyBuilding}.",
        [SupplyCapReachedPlain] = "Supply cap reached.",
        [SupplyWaiting] = "Waiting for supply",
        [DiveCaption] = "Dive (D)",
        [SurfaceCaption] = "Surface (D)",
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
        [OptHandicapButton] = "HC",
        [OptCreditsButton] = "Cash",
        [OptStartButton] = "Army",
        [OptHint] = "(HC handicap, Cash start credits, Army start units)",
    };

    /// <summary>The English text of a key, or the key itself when it is unknown.</summary>
    public static string Get(string key) => English.TryGetValue(key, out string? text) ? text : key;

    /// <summary>The text of a key with one <c>{name}</c> placeholder replaced by <paramref name="value"/>.</summary>
    public static string Format(string key, string name, string value) => Get(key).Replace("{" + name + "}", value, StringComparison.Ordinal);
}
