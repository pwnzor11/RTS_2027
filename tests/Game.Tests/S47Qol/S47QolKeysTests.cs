// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13: the new rebindable actions have free, bindable default keys and show on the Controls page.</summary>
public sealed class S47QolKeysTests
{
    [Theory]
    [InlineData(GameAction.ZoomIn, Keys.OemPipe)]
    [InlineData(GameAction.ZoomOut, Keys.OemOpenBrackets)]
    [InlineData(GameAction.CycleSubtype, Keys.OemTilde)]
    public void A_new_action_has_its_default_key_and_is_listed(GameAction action, Keys key)
    {
        Assert.Equal(key, GameKeys.InfoOf(action).Default);
        Assert.True(KeyBindings.IsBindable(key));
        Assert.Contains(GameKeys.Rows, r => r.Action == action);
        Assert.Empty(KeyBindings.Default.Conflicts());
    }
}
