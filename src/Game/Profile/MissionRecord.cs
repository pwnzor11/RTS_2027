// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Profile;

/// <summary>M10-19: what the player has earned on one campaign mission.</summary>
/// <param name="Completed">The mission was won at least once (the Completed medal).</param>
/// <param name="NoLosses">It was won once without losing a single own unit (the No losses medal).</param>
/// <param name="BestSeconds">Fastest win in seconds; 0 = none. Shown as a record only; there is no par medal (see <see cref="MedalRules"/>).</param>
public readonly record struct MissionRecord(bool Completed, bool NoLosses, int BestSeconds)
{
    /// <summary>The best of both records: medals are never lost, the faster time wins.</summary>
    public MissionRecord Merge(MissionRecord other) => new(
        Completed || other.Completed,
        NoLosses || other.NoLosses,
        BestSeconds == 0 ? other.BestSeconds : other.BestSeconds == 0 ? BestSeconds : Math.Min(BestSeconds, other.BestSeconds));
}
