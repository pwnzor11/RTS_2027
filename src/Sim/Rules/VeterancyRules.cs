// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// Generic veterancy constants (game.toml <c>[veterancy]</c>, docs/design/14_veterancy.md §3, D-127). Three ranks for every faction.
/// Every per-rank list has exactly <see cref="Ranks"/> entries (rank 1..3); rank 0 always uses the base value. Everything is integer:
/// permille factors, kill value in credits. The defaults are the design table; <see cref="Enabled"/> defaults to false, so rules sets
/// without a <c>[veterancy]</c> table behave exactly as before.
/// </summary>
public sealed record VeterancyRules
{
    /// <summary>Number of ranks above recruit.</summary>
    public const int Ranks = 3;

    /// <summary>Master switch (TOML <c>enabled</c>, default false). Off: no kill value is earned and no bonus applies.</summary>
    public bool Enabled { get; init; }

    /// <summary>Kill value needed for rank 1..3 (TOML <c>thresholds</c>, ascending).</summary>
    public IReadOnlyList<int> Thresholds { get; init; } = new[] { 600, 1600, 3200 };

    /// <summary>Kill value per mille of the victim's cost for a unit kill (TOML <c>kill_permille_unit</c>).</summary>
    public int KillPermilleUnit { get; init; } = 1000;

    /// <summary>Kill value per mille of the victim's cost for a building kill (TOML <c>kill_permille_building</c>).</summary>
    public int KillPermilleBuilding { get; init; } = 500;

    /// <summary>Damage dealt, permille of base, per rank 1..3 (TOML <c>damage_permille</c>).</summary>
    public IReadOnlyList<int> DamagePermille { get; init; } = new[] { 1000, 1000, 1000 };

    /// <summary>Damage taken, permille of base (lower is tougher), per rank 1..3 (TOML <c>taken_permille</c>).</summary>
    public IReadOnlyList<int> TakenPermille { get; init; } = new[] { 1000, 1000, 1000 };

    /// <summary>Reload time, permille of base (lower is faster), per rank 1..3 (TOML <c>reload_permille</c>).</summary>
    public IReadOnlyList<int> ReloadPermille { get; init; } = new[] { 1000, 1000, 1000 };

    /// <summary>Speed, permille of base, per rank 1..3 (TOML <c>speed_permille</c>).</summary>
    public IReadOnlyList<int> SpeedPermille { get; init; } = new[] { 1000, 1000, 1000 };

    /// <summary>Ticks between self-heal steps (TOML <c>heal_interval_ticks</c>).</summary>
    public int HealIntervalTicks { get; init; } = 20;

    /// <summary>Self-heal per step, permille of max health, per rank 1..3 (TOML <c>heal_permille</c>; 0 = no heal).</summary>
    public IReadOnlyList<int> HealPermille { get; init; } = new[] { 0, 0, 10 };

    /// <summary>Ticks without firing or being hit before self-heal runs (TOML <c>heal_quiet_ticks</c>).</summary>
    public int HealQuietTicks { get; init; } = 100;

    /// <summary>The defaults: disabled, design values.</summary>
    public static VeterancyRules Default { get; } = new();
}
