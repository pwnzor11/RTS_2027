// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting;

namespace Rts.Game.Profile;

/// <summary>S47 U-12: what the player has done on one drill.</summary>
/// <param name="Medal">Best medal (0 none, 1 bronze, 2 silver, 3 gold) over all won runs.</param>
/// <param name="BestSeconds">Fastest won run in seconds; 0 = never won.</param>
/// <param name="BestLost">Fewest units and structures lost in a won run; -1 = never won.</param>
public readonly record struct DrillRecord(int Medal, int BestSeconds, int BestLost = -1)
{
    /// <summary>True when the drill was won at least once.</summary>
    public bool Won => BestSeconds > 0;

    /// <summary>The best medal as an enum.</summary>
    public DrillMedal BestMedal => (DrillMedal)Math.Clamp(Medal, 0, 3);

    /// <summary>The best of both records: the higher medal, the faster time, the smaller loss.</summary>
    public DrillRecord Merge(DrillRecord other) => new(
        Math.Max(Medal, other.Medal),
        BestSeconds == 0 ? other.BestSeconds : other.BestSeconds == 0 ? BestSeconds : Math.Min(BestSeconds, other.BestSeconds),
        BestLost < 0 ? other.BestLost : other.BestLost < 0 ? BestLost : Math.Min(BestLost, other.BestLost));
}
