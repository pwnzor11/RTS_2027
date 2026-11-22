// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>
/// S19 D-205 (M13-15): the per-slot skirmish options and what they mean. <b>Handicap</b> is a percentage of production speed AND income (items
/// build in 100/h of the time, and harvesters and salvagers deliver h percent of what they carry; damage and health are untouched). The
/// <b>credits preset</b> sets the credits the slot starts with, and <b>small army</b> adds a few soldiers and light vehicles to the faction's
/// start kit. The defaults (100 percent, the rules' credits, kit only) build exactly the old game.
/// </summary>
public static class SlotOptions
{
    /// <summary>The handicap choices in percent, strongest handicap first.</summary>
    public static readonly IReadOnlyList<int> HandicapSteps = new[] { 50, 75, 100 };

    /// <summary>S38 (M5-15): the handicap choices of an AI slot: the human steps, then an edge above 100 (more income and build speed).</summary>
    public static readonly IReadOnlyList<int> AiHandicapSteps = new[] { 50, 75, 100, 125, 150 };

    /// <summary>The edge the "Super hard (LLM)" preset gives its AI slot, in percent of production speed and income.</summary>
    public const int SuperHardEdge = 150;   // s38 (D-286): tier 5 at 150 v plain tier 5 won 85-100 % of decided games (Authority, Left Behind, Expedition; Directorate 68 %)

    /// <summary>The AI tier the "Super hard (LLM)" preset sets.</summary>
    public const int SuperHardTier = 5;

    /// <summary>The default handicap (none).</summary>
    public const int NoHandicap = 100;

    /// <summary>Starting credits per preset; 0 means "the rules' amount" (the default preset) and is never sent to the sim.</summary>
    public static readonly IReadOnlyList<int> CreditsPresets = new[] { 0, 2000, 10000, 20000 };

    /// <summary>The next handicap step after <paramref name="percent"/>, wrapping round (a value that is not a step starts at the first).</summary>
    public static int NextHandicap(int percent) => NextHandicap(percent, ai: false);

    /// <summary>The next handicap step for a human slot (50, 75, 100) or, with <paramref name="ai"/>, an AI slot (50 to 150).</summary>
    public static int NextHandicap(int percent, bool ai)
    {
        IReadOnlyList<int> steps = ai ? AiHandicapSteps : HandicapSteps;
        int at = -1;
        for (int i = 0; i < steps.Count; i++) if (steps[i] == percent) at = i;
        return steps[(at + 1) % steps.Count];
    }

    /// <summary>The handicap a slot keeps when it becomes a human: an edge above 100 is only for AI slots.</summary>
    public static int ForHuman(int percent) => Math.Min(percent, NoHandicap);

    /// <summary>The next credits preset index, wrapping round.</summary>
    public static int NextCredits(int preset) => (preset + 1) % CreditsPresets.Count;

    /// <summary>The credits the sim gets for a preset: -1 for the default preset (the rules' amount), else the preset's amount.</summary>
    public static int CreditsFor(int preset) => preset > 0 && preset < CreditsPresets.Count ? CreditsPresets[preset] : -1;

    /// <summary>"Handicap 75%", "Handicap none" at 100, "Edge +50%" above 100.</summary>
    public static string HandicapLabel(int percent) =>
        percent == NoHandicap ? "Handicap none" : percent > NoHandicap ? $"Edge +{percent - NoHandicap}%" : $"Handicap {percent}%";

    /// <summary>"Credits default" or "Credits 10000".</summary>
    public static string CreditsLabel(int preset) => preset > 0 && preset < CreditsPresets.Count ? $"Credits {CreditsPresets[preset]}" : "Credits default";

    /// <summary>"Start: base only" or "Start: small army".</summary>
    public static string StartLabel(bool smallArmy) => smallArmy ? "Start: small army" : "Start: base only";
}
