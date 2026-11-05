// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Llm.Tests;

/// <summary>M7-02: now that <see cref="Faction.Expedition"/> exists, the Expedition personas in data/llm/personas.toml are reachable by the
/// faction's enum name, exactly as the game layer asks for them (<c>slot.Faction.ToString()</c>).</summary>
public class ExpeditionPersonaTests
{
    private static IReadOnlyList<CommanderPersona> Shipped()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "data", "llm", "personas.toml");
            if (File.Exists(candidate)) return PersonaCatalog.Load(candidate);
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException("data/llm/personas.toml");
    }

    [Fact]
    public void Every_persona_faction_string_is_a_Faction_enum_name()
    {
        foreach (CommanderPersona p in Shipped())
            Assert.True(Enum.TryParse<Faction>(p.Faction, out Faction f) && Enum.IsDefined(f), $"{p.Id}: '{p.Faction}' is not a Faction");
    }

    [Fact]
    public void Expedition_personas_load_and_are_picked_for_the_Expedition_faction()
    {
        IReadOnlyList<CommanderPersona> all = Shipped();
        Assert.Equal(4, all.Count(p => p.Faction == nameof(Faction.Expedition)));
        string faction = Faction.Expedition.ToString();
        CommanderPersona first = PersonaCatalog.Pick(all, faction, 0);
        CommanderPersona second = PersonaCatalog.Pick(all, faction, 1);
        Assert.StartsWith("leader_exp_", first.Id);
        Assert.StartsWith("leader_exp_", second.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Id, PersonaCatalog.Pick(all, faction, 4).Id);   // rotates by slot, deterministic
    }
}
