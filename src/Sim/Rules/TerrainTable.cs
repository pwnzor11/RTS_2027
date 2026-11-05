// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Land × locomotion → speed percent (0 = impassable), plus buildable and soft (M3: burrowers may surface,
/// Bores may break through) per land type.</summary>
public sealed class TerrainTable
{
    private readonly int[] _speedPercent; // [land * Locomotions + loco]
    private readonly bool[] _buildable;
    private readonly bool[] _soft;
    private readonly bool[] _landing;

    /// <summary>Soft land by default (design 05 §1): clear, rough and ash; road, water, rock, cliff and shallow (M4) are hard.</summary>
    public static bool DefaultSoft(LandType land) => land is LandType.Clear or LandType.Rough or LandType.Ash;

    /// <summary>Table with the default soft column (<see cref="DefaultSoft"/>).</summary>
    public TerrainTable(int[] speedPercent, bool[] buildable) : this(speedPercent, buildable, null) { }

    /// <summary>Table with an explicit soft column (one entry per land type), or the default when null.</summary>
    public TerrainTable(int[] speedPercent, bool[] buildable, bool[]? soft) : this(speedPercent, buildable, soft, null) { }

    /// <summary>Table with an explicit soft column and an explicit landing column (M7-02; one entry per land type each), or the defaults
    /// (<see cref="DefaultSoft"/>; no landing land) when null.</summary>
    public TerrainTable(int[] speedPercent, bool[] buildable, bool[]? soft, bool[]? landing)
    {
        if (speedPercent.Length != RuleCounts.LandTypes * RuleCounts.Locomotions)
            throw new ArgumentException("speedPercent must be LandTypes × Locomotions.", nameof(speedPercent));
        if (buildable.Length != RuleCounts.LandTypes)
            throw new ArgumentException("buildable must have one entry per land type.", nameof(buildable));
        _speedPercent = (int[])speedPercent.Clone();
        _buildable = (bool[])buildable.Clone();
        if (soft != null && soft.Length != RuleCounts.LandTypes)
            throw new ArgumentException("soft must have one entry per land type.", nameof(soft));
        if (landing != null && landing.Length != RuleCounts.LandTypes)
            throw new ArgumentException("landing must have one entry per land type.", nameof(landing));
        _landing = new bool[RuleCounts.LandTypes];
        if (landing != null) landing.CopyTo(_landing, 0);
        _soft = new bool[RuleCounts.LandTypes];
        for (int l = 0; l < RuleCounts.LandTypes; l++) _soft[l] = soft != null ? soft[l] : DefaultSoft((LandType)l);
    }

    public int SpeedPercent(LandType land, Locomotion loco) => _speedPercent[(int)land * RuleCounts.Locomotions + (int)loco];
    public bool Buildable(LandType land) => _buildable[(int)land];

    /// <summary>True if the land is soft (terrain.toml <c>[soft]</c>): burrowers can surface and Bores break through there
    /// unless the cell is hardened (design 05 §1–§2).</summary>
    public bool Soft(LandType land) => _soft[(int)land];

    /// <summary>True if landing craft may unload vehicles onto the land (terrain.toml <c>[landing]</c>, M7-02; the beach, <see cref="LandType.Coast"/>).
    /// False for every land when the table is absent.</summary>
    public bool Landing(LandType land) => _landing[(int)land];
}
