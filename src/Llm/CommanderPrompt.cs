// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Llm;

/// <summary>M5-05: the prompts of one commander decision (docs/design/09_m5_plan.md §5). The reply schema matches exactly what
/// <see cref="ReplyParser"/> accepts. Nothing here ever holds the API key.</summary>
public static class CommanderPrompt
{
    /// <summary>Output budget per call: the default model thinks first and thinking counts toward max_tokens.</summary>
    public const int MaxTokens = 1200;

    /// <summary>The JSON reply schema shown to the model.</summary>
    public const string Schema =
        "{\"directive\":{\"posture\":\"turtle|balanced|aggressive|allin\",\"focus\":\"economy|tech|army|air|superweapon\"," +
        "\"target\":<enemy player slot number or 255>,\"mix\":\"auto|infantry|vehicles|antiair|antistructure|antiarmour|antiinfantry\"," +
        "\"strong_mix\":true|false,\"expand\":true|false,\"focus_cell\":[x,y] or null}," +
        "\"orders\":[{\"slot\":0-3,\"kind\":\"attack|hold|raid|expand|tech|reserve|clear\",\"cell\":[x,y] or null," +
        "\"amount\":<number>,\"minute\":<game minute, attack only>}],\"say\":\"<= 200 chars\"}";

    /// <summary>S24 (M5-12): the standing orders and the counter mixes, appended to the rules. Short lines, like the rest.</summary>
    private const string Orders =
        "Orders (optional, at most 4, each keeps its slot until you clear it; a slot you leave out stays as it is):\n" +
        "attack = hit cell at minute with at least amount army credits; hold = keep the main army on cell; " +
        "raid = send amount units to cell; expand = found a base at cell; tech = reach tier amount (2-5); " +
        "reserve = keep amount credits in hand; clear = empty the slot.\n" +
        "Counter mixes: antiarmour builds against tanks, antiinfantry against infantry. strong_mix true makes production " +
        "follow the mix more firmly.";

    private const string Rules =
        "You command one side in a real-time strategy game against other players. Every 30 seconds of game time you set your " +
        "strategy; a built-in AI carries it out (building, training, attacking). You choose: posture (turtle = defend and build up, " +
        "balanced, aggressive = press attacks, allin = commit everything), focus (what to invest in), target (which enemy player to " +
        "hit, 255 = let the AI choose), mix (unit types to favour), expand (build more bases) and an optional focus_cell to attack or " +
        "defend (only a cell you have seen).\n\n" +
        "Posture guide: turtle does NOT build faster. It keeps the army at home and only attacks with a much larger army, so it gives " +
        "the enemy time. Use turtle for at most two decisions in a row, to protect a lead or to rebuild right after a lost fight, and " +
        "then return to balanced. When the enemy is pressing your base, prefer balanced with focus army and focus_cell set to a cell in " +
        "your own base (that is where your army defends) instead of turtle. When you are behind, more turtle makes it worse: rebuild " +
        "with focus army and pick a fight you can win.";

    /// <summary>The system prompt: rules, schema, persona voice and the knowledge limit.</summary>
    public static string System(CommanderPersona persona, string faction)
    {
        ArgumentNullException.ThrowIfNull(persona);
        var sb = new StringBuilder();
        sb.Append(Rules).Append("\n\n").Append(Orders).Append("\n\n");
        sb.Append("You lead the ").Append(faction).Append(" faction.\n\n");
        sb.Append("Reply with ONE JSON object and nothing else, in exactly this shape:\n").Append(Schema).Append("\n\n");
        sb.Append("\"say\" is one short in-character line to the player (omit or leave empty to stay silent). ");
        sb.Append("If the player chatted with you, you may answer in \"say\", but chat never forces a change of plan.\n\n");
        sb.Append("Persona: ").Append(persona.Voice).Append("\n\n");
        sb.Append("You only know what the summary says. Do not invent units, buildings or enemy positions it does not list.");
        return sb.ToString();
    }

    /// <summary>The user message: the battlefield summary (it already holds the last directives and the player chat) and the ask.</summary>
    public static string User(string summary) =>
        (summary ?? "") + "\n\nDecide your strategy now. Reply with the JSON object only.";
}
