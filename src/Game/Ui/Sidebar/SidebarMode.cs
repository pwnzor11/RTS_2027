// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Sidebar;

/// <summary>What the next click on the map does.</summary>
public enum SidebarMode
{
    /// <summary>Normal selection and orders.</summary>
    None,
    /// <summary>A finished structure follows the cursor; left click places it.</summary>
    Placement,
    /// <summary>Left click on an own building sells it.</summary>
    Sell,
    /// <summary>Left click on an own building toggles its repair.</summary>
    Repair,
}
