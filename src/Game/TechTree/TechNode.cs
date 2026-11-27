// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.TechTree;

/// <summary>
/// One node of a faction's tech tree (U-12). <see cref="Category"/> and <see cref="TypeIndex"/> name the rules entry the way the sim's
/// production commands do (Upgrade: index into <c>SimRules.Upgrades</c>).
/// </summary>
/// <param name="Id">Rules id (unique within a tree).</param>
/// <param name="Category">Production category (Structure, Defence, Infantry, Vehicle, Aircraft or Upgrade).</param>
/// <param name="TypeIndex">Index in the rules table of that kind.</param>
/// <param name="Tier">Tech tier 1..3 as the rules say.</param>
/// <param name="Cost">Credits.</param>
/// <param name="BuildTicks">Build or research time in ticks (derived from cost when the rules give none).</param>
/// <param name="Needs">Ids of the nodes that satisfy the prerequisites: per prerequisite, every node that provides it (any one is enough).</param>
/// <param name="Unlocks">Ids of the nodes that list this one among their <see cref="Needs"/> (id order).</param>
/// <param name="Column">Column of the layout: 0 for nodes with no prerequisites, else one more than the deepest of <see cref="Needs"/>.</param>
/// <param name="Start">True for a structure the faction starts with or deploys into that is not itself buildable.</param>
public sealed record TechNode(string Id, ProductionCategory Category, int TypeIndex, int Tier, int Cost, int BuildTicks,
    IReadOnlyList<string> Needs, IReadOnlyList<string> Unlocks, int Column, bool Start);
