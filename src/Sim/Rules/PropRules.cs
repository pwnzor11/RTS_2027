// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// Destructible blocking props (M12-16, D-264 (3), design 16 D1 option C). game.toml <c>[props]</c>, every number (tune). A splash hit
/// (a <c>Detonate</c> with splash 1 or more, a superweapon blast, a salvo impact, a death weapon) takes <c>damage x versus[SplashArmor] / 100</c>
/// (half, minimum 1, away from the impact cell) off the health of a Tree, Snag or Wreck in range; at 0 the prop is gone and the cell is Rough.
/// A heavy crusher destroys a Tree or Snag by entering the cell. Block and Pylon have no health and never break.
/// <para><see cref="SplashArmor"/> is the one armour class every prop is hit as (ASSUMED S31: Structure, an inert hard object). Wreck is tough
/// (about 4 times a tree). <see cref="CrushPathPenalty"/> multiplies the path cost of entering a crushable prop cell for a crusher (the
/// "small extra cost", a Rough cell costs 1.33 normal cells).</para>
/// </summary>
public sealed record PropRules(int TreeHealth, int SnagHealth, int WreckHealth, ArmorClass SplashArmor, int CrushPathPenalty)
{
    /// <summary>Starting values: a tree falls to one or two shells, a wreck takes four or five.</summary>
    public static PropRules Default { get; } = new(40, 40, 160, ArmorClass.Structure, 3);

    /// <summary>Starting health of a prop overlay, 0 for anything that never breaks (None, resources, Wall, Block, Pylon).</summary>
    public int HealthOf(OverlayKind kind) => kind switch
    {
        OverlayKind.Tree => TreeHealth,
        OverlayKind.Snag => SnagHealth,
        OverlayKind.Wreck => WreckHealth,
        _ => 0,
    };
}
