// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Profile;
using Rts.Scripting;

namespace Rts.Game.Drills;

/// <summary>S47 U-12: what the end screen of a won drill shows (no graphics): the medal earned, the run, the thresholds.</summary>
public sealed class DrillEndModel
{
    private DrillEndModel(string title, DrillMedal medal, IReadOnlyList<string> lines)
    {
        Title = title;
        Medal = medal;
        Lines = lines;
    }

    /// <summary>The mission title.</summary>
    public string Title { get; }

    /// <summary>The medal this run earned.</summary>
    public DrillMedal Medal { get; }

    /// <summary>The lines drawn on the screen.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>Builds the model.</summary>
    /// <param name="title">Mission title.</param>
    /// <param name="def">The thresholds.</param>
    /// <param name="run">The measured run.</param>
    /// <param name="before">The record before this run (default = none).</param>
    /// <param name="after">The record after this run was merged.</param>
    public static DrillEndModel Build(string title, DrillDef def, DrillRun run, DrillRecord before, DrillRecord after)
    {
        DrillMedal medal = DrillScoring.Medal(def, run);
        var lines = new List<string>
        {
            "Medal: " + DrillText.MedalName(medal),
            "Time: " + DrillText.Time(run.Seconds),
            "Lost: " + run.Lost,
            "Units kept: " + run.Kept,
            "",
            DrillText.Threshold(def, DrillMedal.Gold),
            DrillText.Threshold(def, DrillMedal.Silver),
            DrillText.Threshold(def, DrillMedal.Bronze),
            "",
            "Best so far: " + DrillText.MedalName(after.BestMedal) + ", " + DrillText.Time(Math.Max(1, after.BestSeconds)),
        };
        if ((int)medal > before.Medal) lines.Add("New best medal");
        else if (before.Won && run.Seconds < before.BestSeconds) lines.Add("New best time");
        return new DrillEndModel(title, medal, lines);
    }
}
