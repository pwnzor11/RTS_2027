// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Ui.Objectives;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEUI-03: the objective hints name the keys that are bound.</summary>
[Collection("GameKeysActive")]
public sealed class UiObjectiveKeyHintTests
{
    [Fact]
    public void Default_hint_text_is_unchanged()
    {
        KeyBindings saved = GameKeys.Active;
        try
        {
            GameKeys.Active = KeyBindings.Default;
            Assert.Equal("O objectives | J go to | Tab next", ObjectiveKeyHints.Bar());
            Assert.Equal("J: jump there", ObjectiveKeyHints.Jump());
        }
        finally { GameKeys.Active = saved; }
    }

    [Fact]
    public void Rebinding_changes_the_hint()
    {
        KeyBindings saved = GameKeys.Active;
        try
        {
            GameKeys.Active = KeyBindings.Default.With(GameAction.ObjJump, Keys.F7).With(GameAction.ObjList, Keys.L);
            Assert.Equal("L objectives | F7 go to | Tab next", ObjectiveKeyHints.Bar());
            Assert.Equal("F7: jump there", ObjectiveKeyHints.Jump());
        }
        finally { GameKeys.Active = saved; }
    }
}
