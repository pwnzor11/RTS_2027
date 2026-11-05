// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Veterancy;

/// <summary>
/// Client-side promotion detection (VET-5). Keeps the last-seen rank per vehicle or soldier and reports a promotion when a rank
/// rises. The sim sends no event for it. An id that is no longer listed (dead, or out of sight) is forgotten, so a later sighting
/// starts fresh and is not a promotion. Equal or lower ranks, and first sightings, are never promotions.
/// </summary>
public sealed class PromotionTracker
{
    /// <summary>How long the promotion flash lasts: 0.5 s at 20 ticks per second.</summary>
    public const int FlashTicks = 10;

    private readonly Dictionary<RankKey, int> _lastRank = new();
    private readonly Dictionary<RankKey, int> _flashUntil = new();
    private readonly HashSet<RankKey> _listed = new();
    private readonly List<RankKey> _gone = new();

    /// <summary>
    /// Reads the ranks listed now. Fills <paramref name="promoted"/> (cleared first) with the keys whose rank rose since the last call,
    /// and starts their flash at <paramref name="tick"/>. Forgets every key missing from <paramref name="current"/>.
    /// </summary>
    public void Update(int tick, IReadOnlyList<(RankKey Key, int Rank)> current, List<RankKey> promoted)
    {
        promoted.Clear();
        _listed.Clear();
        foreach ((RankKey key, int rank) in current)
        {
            _listed.Add(key);
            if (_lastRank.TryGetValue(key, out int before) && rank > before)
            {
                promoted.Add(key);
                _flashUntil[key] = tick + FlashTicks;
            }

            _lastRank[key] = rank;
        }

        _gone.Clear();
        foreach (RankKey key in _lastRank.Keys)
            if (!_listed.Contains(key)) _gone.Add(key);
        foreach (RankKey key in _gone)
        {
            _lastRank.Remove(key);
            _flashUntil.Remove(key);
        }
    }

    /// <summary>True while the promotion flash of <paramref name="key"/> is running at <paramref name="tick"/>.</summary>
    public bool IsFlashing(RankKey key, int tick) => _flashUntil.TryGetValue(key, out int until) && tick < until;

    /// <summary>Number of keys currently remembered (for tests and diagnostics).</summary>
    public int Tracked => _lastRank.Count;
}
