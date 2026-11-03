// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Land × locomotion → speed percent (0 = impassable), plus buildable per land type.</summary>
public sealed class TerrainTable
{
    private readonly int[] _speedPercent; // [land * Locomotions + loco]
    private readonly bool[] _buildable;

    public TerrainTable(int[] speedPercent, bool[] buildable)
    {
        if (speedPercent.Length != RuleCounts.LandTypes * RuleCounts.Locomotions)
            throw new ArgumentException("speedPercent must be LandTypes × Locomotions.", nameof(speedPercent));
        if (buildable.Length != RuleCounts.LandTypes)
            throw new ArgumentException("buildable must have one entry per land type.", nameof(buildable));
        _speedPercent = (int[])speedPercent.Clone();
        _buildable = (bool[])buildable.Clone();
    }

    public int SpeedPercent(LandType land, Locomotion loco) => _speedPercent[(int)land * RuleCounts.Locomotions + (int)loco];
    public bool Buildable(LandType land) => _buildable[(int)land];
}
