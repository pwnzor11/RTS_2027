// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests.S18;

/// <summary>
/// S18 TESTS-REPO-01: tests that change process-wide statics (<c>UiScale.TextFactor</c>, <c>PlayerColours</c> assignment,
/// <c>UnitRenderer.InfantryExtraScale</c>, <c>GameKeys.Active</c>) belong to this collection. With
/// <c>DisableParallelization</c> its tests run one at a time and never beside any other test class, so no test that merely READS one of
/// those statics can see it mid-change.
/// </summary>
[CollectionDefinition("UiStatics", DisableParallelization = true)]
public sealed class UiStaticsCollection
{
}
