// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Inputs of <see cref="MapGenerator.Generate(MapGenParams)"/> (M4). The map depends only on these; Name null = the default
/// name <c>&lt;biome&gt;_&lt;s|m|l&gt;_&lt;players&gt;p_&lt;seed&gt;</c> (<see cref="MapGenerator.DefaultName"/>). <paramref name="OceanEdges"/>
/// (M7-13, default none) makes those edges sea with a Coast band and fills <c>convoy_edge</c> and <c>landing</c> in the metadata; with
/// none set the output is byte-identical to the pre-M7 generator. <paramref name="Layout"/> (M12-05, D-180) is the generator version: it is
/// stored in skirmish saves and map files so a map is only ever rebuilt by the generator that made it. Saves and files from before the
/// field existed read as 0, which no generator in this build can rebuild (<see cref="MapGenerator.CurrentLayout"/>).
/// <paramref name="Stamps"/> (M12-13, default false) places 2 to 4 symmetric stamps from <c>data/stamps</c> in the neutral ground; off, the output is
/// byte-identical to a generator without stamps.</summary>
public sealed record MapGenParams(Biome Biome, MapSize Size, int Players, ulong Seed, string? Name = null, OceanEdges OceanEdges = OceanEdges.None,
    int Layout = MapGenerator.CurrentLayout, bool Stamps = false);
