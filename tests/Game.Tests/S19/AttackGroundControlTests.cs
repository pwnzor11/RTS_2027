// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Sim.Core;
using Xunit;

namespace Rts.Game.Tests.S19;

/// <summary>S19 D-205 (FAD-05): Ctrl + right-click is the attack-ground (force-fire) order and has a line on the Controls page.</summary>
public sealed class AttackGroundControlTests
{
    [Fact]
    public void ForceFire_SendsAttackGround() => Assert.Equal(CommandKind.AttackGround, OrderController.ForceFireOrder);

    [Fact]
    public void ControlsPage_ListsCtrlRightClick() =>
        Assert.Contains(GameKeys.Rows, r => r.Action is null && r.LabelFor(KeyBindings.Default) == "Ctrl + right click");
}
