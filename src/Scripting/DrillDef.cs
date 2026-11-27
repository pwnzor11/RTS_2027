// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>
/// S47 U-12: the medal thresholds of a drill mission (<c>[drill]</c> in mission.toml). Lower is better: a run earns the best medal whose limit
/// it meets (and, when a <c>keep_*</c> minimum is set, whose minimum of surviving own units it also meets).
/// </summary>
/// <param name="Order">List position in the Drills menu (1 first).</param>
/// <param name="Metric">"seconds" (time taken) or "lost" (own units plus structures lost).</param>
/// <param name="Gold">Gold limit of the metric.</param>
/// <param name="Silver">Silver limit.</param>
/// <param name="Bronze">Bronze limit.</param>
/// <param name="KeepGold">Own units that must survive for gold; 0 = no requirement.</param>
/// <param name="KeepSilver">Own units that must survive for silver; 0 = none.</param>
/// <param name="KeepBronze">Own units that must survive for bronze; 0 = none.</param>
public sealed record DrillDef(int Order, string Metric, int Gold, int Silver, int Bronze, int KeepGold = 0, int KeepSilver = 0, int KeepBronze = 0)
{
    /// <summary>True when the metric is time.</summary>
    public bool IsTime => Metric == "seconds";
}
