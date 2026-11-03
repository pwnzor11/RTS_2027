// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>How one player regards another (D-054). Self and Ally never auto-target each other; Neutral
/// players are never auto-targeted and never auto-target. Explicit Attack orders may target Enemy or Neutral.</summary>
public enum Relation : byte { Self = 0, Ally = 1, Enemy = 2, Neutral = 3 }

/// <summary>Team ids for <see cref="SimSetup.Teams"/>.</summary>
public static class Teams
{
    /// <summary>A slot with this team id is neutral (civilians, tech buildings).</summary>
    public const byte Neutral = 255;
}
