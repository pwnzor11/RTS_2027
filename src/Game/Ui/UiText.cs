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

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        [SupplyLabel] = "Supply",
        [SupplyCapReached] = "Supply cap reached. Build more {supplyBuilding}.",
        [SupplyCapReachedPlain] = "Supply cap reached.",
        [SupplyWaiting] = "Waiting for supply",
        [DiveCaption] = "Dive (D)",
        [SurfaceCaption] = "Surface (D)",
        [ShipmentTimer] = "Shipment in {time} +{payload}",
    };

    /// <summary>The English text of a key, or the key itself when it is unknown.</summary>
    public static string Get(string key) => English.TryGetValue(key, out string? text) ? text : key;

    /// <summary>The text of a key with one <c>{name}</c> placeholder replaced by <paramref name="value"/>.</summary>
    public static string Format(string key, string name, string value) => Get(key).Replace("{" + name + "}", value, StringComparison.Ordinal);
}
