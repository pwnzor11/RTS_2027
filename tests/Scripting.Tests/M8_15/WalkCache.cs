// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Collections.Concurrent;

namespace Rts.Scripting.Tests.M8_15;

/// <summary>
/// Uninterrupted walks are deterministic, so the test classes of M8-15 that need "the straight run" of a mission at a tier
/// (the save/load comparison, the balance table) share one run per (mission, tier) instead of repeating it.
/// </summary>
internal static class WalkCache
{
    private static readonly ConcurrentDictionary<(string, int), Lazy<WalkResult>> Runs = new();

    /// <summary>A fresh, uncached straight walk.</summary>
    public static WalkResult Fresh(string missionId, int tier) =>
        CampaignWalks.Play(missionId, tier, MissionSession.Create(TestData.DataDir, missionId));

    /// <summary>The straight walk of the mission at the tier; played once per process.</summary>
    public static WalkResult Get(string missionId, int tier) =>
        Runs.GetOrAdd((missionId, tier), k => new Lazy<WalkResult>(() => Fresh(k.Item1, k.Item2))).Value;

    /// <summary>Stores a fresh result so later <see cref="Get"/> calls reuse it.</summary>
    public static void Remember(WalkResult r) =>
        Runs.TryAdd((r.Mission, r.Tier), new Lazy<WalkResult>(() => r));
}
