// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

/// <summary>
/// Generator parameters of one biome (<c>data/mapgen/biomes.toml</c>, <c>[biome.&lt;key&gt;]</c>; design 08 §1). Not sim rules: maps are
/// identified by their map hash, so these stay out of RulesHash. Permille values are shares of the map area; counts are per symmetry
/// orbit (each placement is copied to every image), so a 4-player map gets four times the features of one fundamental domain.
/// <see cref="RockStyle"/> is one of rubble, dunes, spurs, blobs. <see cref="Decor"/> (M12-05) holds the blocking-prop rules.
/// </summary>
public sealed record BiomeParams(
    string Key,
    string Theater,
    LandType BaseLand,
    MapSymmetry Symmetry2p,
    MapSymmetry Symmetry4p,
    int WaterPermille,
    int ShallowPermille,
    int RockPermille,
    int RoughPermille,
    string RockStyle,
    int CliffPlateaus,
    int Rivers,
    int RiverWidth,
    int RiverWanderPermille,
    int FordSpacing,
    int RoadGrid,
    int OceanWidth,
    int AshFieldsPerStart,
    int PitchFieldsPerStart,
    int ContestedFields,
    int ExtraFields,
    int FieldDensityMin,
    int FieldDensityMax,
    int Ruins,
    DecorParams Decor);
