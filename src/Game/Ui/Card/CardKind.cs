// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Card;

/// <summary>What the selection card shows (M11-04, audit section 3 option B).</summary>
public enum CardKind
{
    /// <summary>Nothing is selected: no card.</summary>
    None,
    /// <summary>Exactly one own unit.</summary>
    Unit,
    /// <summary>Two or more own units.</summary>
    Multi,
    /// <summary>One own building.</summary>
    Building,
}
