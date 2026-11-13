// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>
/// M5-01: a commander's strategy for one player (docs/design/09_m5_plan.md §3). Arrives as <see cref="CommandKind.Directive"/>,
/// is stored in the "directive" state part and read by the tiered AI through <c>PlayerView.Directive</c>. The sim itself never acts
/// on it. <see cref="Set"/> is false until the player's first directive; the AI then plays its plain tier.
/// </summary>
public readonly record struct Directive(
    bool Set,
    byte Posture,       // 0 turtle, 1 balanced, 2 aggressive, 3 all-in
    byte Focus,         // 0 economy, 1 tech, 2 army, 3 air, 4 superweapon
    byte TargetPlayer,  // a slot, or 255 = the AI chooses
    byte Mix,           // 0 auto, 1 infantry, 2 vehicles, 3 anti-air, 4 anti-structure
    bool Expand,
    CellCoord FocusCell,// Directive.NoCell = none; otherwise a cell the player had explored when the directive arrived
    int Tick)           // the tick the directive was applied (0 when not Set)
{
    /// <summary>"No focus cell".</summary>
    public static readonly CellCoord NoCell = new(-1, -1);

    /// <summary>The value before any directive: plain tier behaviour.</summary>
    public static readonly Directive None = new(false, DirectiveCodec.PostureBalanced, DirectiveCodec.FocusArmy, DirectiveCodec.AnyTarget,
        DirectiveCodec.MixAuto, false, NoCell, 0);

    /// <summary>True when <see cref="FocusCell"/> names a cell.</summary>
    public bool HasFocusCell => FocusCell.X >= 0;
}

/// <summary>M5-01 directive constants and the packing of a directive into <see cref="Command.Arg"/>.</summary>
public static class DirectiveCodec
{
    public const byte PostureTurtle = 0, PostureBalanced = 1, PostureAggressive = 2, PostureAllIn = 3;
    public const byte FocusEconomy = 0, FocusTech = 1, FocusArmy = 2, FocusAir = 3, FocusSuperweapon = 4;
    public const byte MixAuto = 0, MixInfantry = 1, MixVehicles = 2, MixAntiAir = 3, MixAntiStructure = 4;
    /// <summary>S24 (M5-12, D-241) counter mixes: anti-armour and anti-infantry. The sim's clamp (wired by package s24-dsim) keeps 0..6.</summary>
    public const byte MixAntiArmour = 5, MixAntiInfantry = 6;
    /// <summary>S24 (M5-12): Arg bit 24 = strong mix (counter-building: production follows the mix at <c>StrongMixPermille</c> instead of the
    /// normal preference). Packed by <see cref="PackStrong"/>; package s24-dsim adds it to <see cref="Directive"/> and the clamp.</summary>
    public const int StrongMixBit = 1 << 24;
    /// <summary>Adds the strong-mix bit to a packed Arg.</summary>
    public static int PackStrong(int arg, bool strong) => strong ? arg | StrongMixBit : arg & ~StrongMixBit;

    /// <summary>TargetPlayer value meaning "the AI chooses".</summary>
    public const byte AnyTarget = 255;

    /// <summary>
    /// Packs the integer fields into <see cref="Command.Arg"/>: bits 0..3 posture, 4..7 focus, 8..11 mix, 12..15 expand (0/1),
    /// 16..23 target player. Out-of-range values are stored as given; the sim clamps on apply (<see cref="Clamp"/>).
    /// </summary>
    public static int Pack(byte posture, byte focus, byte targetPlayer, byte mix, bool expand) =>
        (posture & 0xF) | (focus & 0xF) << 4 | (mix & 0xF) << 8 | (expand ? 1 : 0) << 12 | targetPlayer << 16;

    /// <summary>Unpacks <see cref="Pack"/> (no clamping).</summary>
    public static (byte Posture, byte Focus, byte TargetPlayer, byte Mix, bool Expand) Unpack(int arg) =>
        ((byte)(arg & 0xF), (byte)(arg >> 4 & 0xF), (byte)(arg >> 16 & 0xFF), (byte)(arg >> 8 & 0xF), (arg >> 12 & 0xF) != 0);

    /// <summary>
    /// Builds the command for a directive. <paramref name="focusCell"/> = <see cref="Directive.NoCell"/> for none.
    /// The game layer stamps <paramref name="execTick"/> with the first tick after the reply arrived (design §2).
    /// </summary>
    public static Command ToCommand(int execTick, byte player, ushort sequence, byte posture, byte focus, byte targetPlayer, byte mix,
        bool expand, CellCoord focusCell) =>
        new(execTick, player, sequence, CommandKind.Directive, default, default, focusCell, Pack(posture, focus, targetPlayer, mix, expand));

    /// <summary>
    /// The sim's clamp on apply (same defaults as the LLM reply parser): posture &gt; 3 → balanced, focus &gt; 4 → army, mix &gt; 4 → auto,
    /// a target that is not another non-neutral slot → any. The focus cell is checked separately (on the map and explored).
    /// </summary>
    public static (byte Posture, byte Focus, byte TargetPlayer, byte Mix) Clamp(byte posture, byte focus, byte targetPlayer, byte mix,
        byte player, int playerCount, Func<byte, bool> isNeutral)
    {
        ArgumentNullException.ThrowIfNull(isNeutral);
        if (posture > PostureAllIn) posture = PostureBalanced;
        if (focus > FocusSuperweapon) focus = FocusArmy;
        if (mix > MixAntiStructure) mix = MixAuto;
        if (targetPlayer >= playerCount || targetPlayer == player || isNeutral(targetPlayer)) targetPlayer = AnyTarget;
        return (posture, focus, targetPlayer, mix);
    }
}
