// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>One skirmish slot (design 08 §2, M4; M4-11 builds the setup screen on it): kind, faction, AI tier 1..5 (ignored for a
/// human), colour index into <see cref="Rts.Game.Sprites.PlayerColours"/>, and team (slots with the same team are allies).
/// M5: <paramref name="Commander"/> = an AI slot led by the LLM commander on top of its tier (ignored for Human and Closed).
/// S19 (M13-15, see <see cref="SlotOptions"/>): <paramref name="Handicap"/> = percent of production speed and income (100 = none),
/// <paramref name="CreditsPreset"/> = index into <see cref="SlotOptions.CreditsPresets"/> (0 = the rules' starting credits),
/// <paramref name="SmallArmy"/> = add a few soldiers and light vehicles to the faction's start kit.</summary>
public sealed record SkirmishSlot(SlotKind Kind, Faction Faction, int AiTier, int Colour, int Team, bool Commander = false,
    int Handicap = SlotOptions.NoHandicap, int CreditsPreset = 0, bool SmallArmy = false);
