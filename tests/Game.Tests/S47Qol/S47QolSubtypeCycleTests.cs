// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#9): cycling the unit types of a mixed selection.</summary>
public sealed class S47QolSubtypeCycleTests
{
    private static UnitKey K(ushort id) => new(ActorKind.Vehicle, new EntityId(id, 0));

    // keys 1,2 are type 10; key 3 is type 20; key 4 is infantry type 10 (a different kind with the same index)
    private static (UnitKind, int)? TypeOf(UnitKey k) => k.Id.Index switch
    {
        1 or 2 => (UnitKind.Vehicle, 10),
        3 => (UnitKind.Vehicle, 20),
        4 => (UnitKind.Infantry, 10),
        _ => null,
    };

    [Fact]
    public void Each_press_keeps_the_next_type_and_the_round_ends_with_the_whole_group()
    {
        var cycle = new SubtypeCycle();
        UnitKey[] all = { K(1), K(2), K(3), K(4) };
        IReadOnlyList<UnitKey> a = cycle.Next(all, TypeOf)!;
        Assert.Equal(new[] { K(1), K(2) }, a);
        IReadOnlyList<UnitKey> b = cycle.Next(a, TypeOf)!;      // the screen hands back the narrowed selection
        Assert.Equal(new[] { K(3) }, b);
        IReadOnlyList<UnitKey> c = cycle.Next(b, TypeOf)!;
        Assert.Equal(new[] { K(4) }, c);
        IReadOnlyList<UnitKey> d = cycle.Next(c, TypeOf)!;
        Assert.Equal(all, d);
        Assert.Equal(new[] { K(1), K(2) }, cycle.Next(d, TypeOf));   // and round again
    }

    [Fact]
    public void A_single_type_or_unknown_units_give_nothing_to_cycle()
    {
        var cycle = new SubtypeCycle();
        Assert.Null(cycle.Next(new[] { K(1), K(2) }, TypeOf));
        Assert.Null(cycle.Next(new[] { K(1), K(9) }, TypeOf));
    }

    [Fact]
    public void A_selection_the_player_changed_starts_a_new_round()
    {
        var cycle = new SubtypeCycle();
        cycle.Next(new[] { K(1), K(3) }, TypeOf);
        IReadOnlyList<UnitKey> fresh = cycle.Next(new[] { K(3), K(4) }, TypeOf)!;
        Assert.Equal(new[] { K(3) }, fresh);
    }
}
