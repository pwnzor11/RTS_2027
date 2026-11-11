// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Ui.Sidebar;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEUI-03: the key names the HUD shows are the bound keys, not the defaults.</summary>
[Collection("GameKeysActive")]
public sealed class UiKeyNamesTests
{
    [Fact]
    public void Default_tab_letters_are_unchanged()
    {
        KeyBindings saved = GameKeys.Active;
        try
        {
            GameKeys.Active = KeyBindings.Default;
            string all = string.Concat(Enum.GetValues<SidebarTab>().Select(t => SidebarTabs.KeyName(t)));
            Assert.Equal("QWERTY", all);
        }
        finally { GameKeys.Active = saved; }
    }

    [Fact]
    public void A_rebound_tab_shows_its_new_key()
    {
        KeyBindings saved = GameKeys.Active;
        try
        {
            GameKeys.Active = KeyBindings.Default.With(GameAction.TabStructures, Keys.Z).With(GameAction.TabVehicles, Keys.F5);
            Assert.Equal("Z", SidebarTabs.KeyName(SidebarTab.Structures));
            Assert.Equal(KeyNames.Of(Keys.F5), SidebarTabs.KeyName(SidebarTab.Vehicles));
            Assert.Equal("W", SidebarTabs.KeyName(SidebarTab.Defences));
        }
        finally { GameKeys.Active = saved; }
    }
}
