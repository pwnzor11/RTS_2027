// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Llm;

/// <summary>M5-06: the leader personas in <c>data/llm/personas.toml</c>. Contract written by the lead; M5-06 fills the bodies.</summary>
public static class PersonaCatalog
{
    /// <summary>Loads every persona from <paramref name="path"/>; never throws (a missing or bad file gives an empty list).</summary>
    public static IReadOnlyList<CommanderPersona> Load(string path) => Array.Empty<CommanderPersona>();   // M5-06 fills

    /// <summary>The persona for <paramref name="player"/> playing <paramref name="faction"/> (the Faction enum's name, e.g. "Authority"):
    /// the faction's personas in file order, rotated by player slot so two commanders of one faction differ. Deterministic.
    /// No match gives <see cref="CommanderPersona.Plain"/>.</summary>
    public static CommanderPersona Pick(IReadOnlyList<CommanderPersona> all, string faction, byte player) =>
        CommanderPersona.Plain(faction);   // M5-06 fills
}
