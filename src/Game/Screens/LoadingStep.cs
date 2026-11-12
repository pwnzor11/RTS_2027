// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>One part of a load (M10-20): a label for the bar and the work. The work runs on the game thread, one step per frame.</summary>
/// <param name="Label">What is being done, for the line under the bar.</param>
/// <param name="Run">The work.</param>
public sealed record LoadingStep(string Label, Action Run);
