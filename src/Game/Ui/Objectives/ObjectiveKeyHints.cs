// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;

namespace Rts.Game.Ui.Objectives;

/// <summary>S18 GAMEUI-03: the key hints of the objective bar and arrow, built from the keys that are currently bound (not hard-coded J / Tab / O).</summary>
public static class ObjectiveKeyHints
{
    /// <summary>The line under the objective rows (M11-30): "O objectives | J go to | Tab next". The bitmap font is ASCII only, so the separator is a bar.</summary>
    public static string Bar() =>
        $"{KeyOf(GameAction.ObjList)} objectives | {KeyOf(GameAction.ObjJump)} go to | {KeyOf(GameAction.ObjCycle)} next";

    /// <summary>The hint in the off-screen objective arrow's box.</summary>
    public static string Jump() => $"{KeyOf(GameAction.ObjJump)}: jump there";

    private static string KeyOf(GameAction action) => KeyNames.Of(GameKeys.Active.Get(action));
}
