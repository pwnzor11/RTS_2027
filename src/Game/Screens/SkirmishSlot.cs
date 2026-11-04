// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>One skirmish slot (design 08 §2, M4; M4-11 builds the setup screen on it): kind, faction, AI tier 1..5 (ignored for a
/// human), colour index into <see cref="Rts.Game.Sprites.PlayerColours"/>, and team (slots with the same team are allies).</summary>
public sealed record SkirmishSlot(SlotKind Kind, Faction Faction, int AiTier, int Colour, int Team);
