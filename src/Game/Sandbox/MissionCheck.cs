// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Game.Host;
using Rts.Scripting;
using Rts.Sim;

namespace Rts.Game.Sandbox;

/// <summary>
/// <c>--mission-check &lt;id&gt; &lt;ticks&gt;</c>: runs a campaign mission with no window and the human player idle (the AI plays its slot
/// through the same <see cref="SimHost"/> loop the game uses), then prints
/// <c>mission ok id I tick n outcome O errors E objectives done/total messages M hash H</c>. Exit codes: 0 ok, 1 script errors or load
/// failure, 2 usage. The mission outcome is not asserted: an idle player normally loses.
/// </summary>
public static class MissionCheck
{
    private const string Flag = "--mission-check";

    /// <summary>Runs the check. <paramref name="args"/> is the full command line.</summary>
    public static int Run(string[] args)
    {
        int flag = Array.IndexOf(args, Flag);
        if (flag < 0 || flag + 2 >= args.Length
            || !int.TryParse(args[flag + 2], NumberStyles.None, CultureInfo.InvariantCulture, out int ticks))
        {
            Console.Error.WriteLine($"usage: {Flag} <mission id> <ticks>");
            return 2;
        }

        string id = args[flag + 1];
        string? rulesDir = Rts.Data.RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        if (rulesDir is null)
        {
            Console.Error.WriteLine($"error: data/rules not found above {AppContext.BaseDirectory}");
            return 1;
        }

        MissionGame game;
        try { game = MissionGame.Create(rulesDir, id); }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or IOException)
        {
            Console.Error.WriteLine($"error: mission '{id}' failed to load: {e.Message}");
            return 1;
        }

        var host = new SimHost(game.Sim) { Ai = game.Ai };
        host.StepTicks(ticks);

        MissionHost script = game.Host;
        foreach (string error in script.Errors) Console.Error.WriteLine($"script error: {error}");
        int done = script.Objectives.Count(o => o.Status == ObjectiveStatus.Completed);
        Simulation sim = game.Sim;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"mission {(script.Errors.Count == 0 ? "ok" : "FAILED")} id {id} tick {sim.Tick} outcome {script.Outcome} errors {script.Errors.Count} "
            + $"objectives {done}/{script.Objectives.Count} messages {script.Messages.Count} game_over {sim.GameOver} hash {sim.ComputeStateHash():x16}"));
        return script.Errors.Count == 0 ? 0 : 1;
    }
}
