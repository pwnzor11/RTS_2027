// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Alerts;

/// <summary>What an on-screen alert says (M11-10d, audit F1).</summary>
public enum AlertKind
{
    /// <summary>One of the local player's buildings was hit.</summary>
    BaseUnderAttack,
    /// <summary>One of the local player's combat units was hit.</summary>
    UnitUnderAttack,
    /// <summary>One of the local player's harvesters was hit.</summary>
    HarvesterUnderAttack,
    /// <summary>A structure finished and waits to be placed.</summary>
    ConstructionComplete,
    /// <summary>A new unit left its factory.</summary>
    UnitReady,
    /// <summary>Power use exceeds output.</summary>
    LowPower,
    /// <summary>A building of the local player was destroyed.</summary>
    BuildingLost,
    /// <summary>A unit of the local player was destroyed.</summary>
    UnitLost,
    /// <summary>The local player's superweapon is charged.</summary>
    SuperweaponReady,
    /// <summary>The local player's superweapon went off.</summary>
    SuperweaponLaunched,
}
