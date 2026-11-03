// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Data;
using Rts.Game.Host;
using Rts.Sim;

namespace Rts.Game.Sandbox;

/// <summary>
/// <c>--skirmish-check &lt;ticks&gt;</c>: builds the menu's skirmish with no window (map seed 0, AI tier 1), leaves player 0
/// idle, lets the AI play player 1 through the same <see cref="SimHost"/> loop the game uses, and prints
/// <c>skirmish ok tick n ai a cash0 x cash1 y buildings0 b0 buildings1 b1</c>. Exit codes: 0 ok, 1 error, 2 usage.
/// No replay is written.
/// </summary>
public static class SkirmishCheck
{
    private const string Flag = "--skirmish-check";

    /// <summary>Runs the check. <paramref name="args"/> is the full command line.</summary>
    public static int Run(string[] args)
    {
        int flag = Array.IndexOf(args, Flag);
        if (flag < 0 || flag + 1 >= args.Length
            || !int.TryParse(args[flag + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int ticks))
        {
            Console.Error.WriteLine($"usage: {Flag} <ticks>");
            return 2;
        }

        string? rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        if (rulesDir is null)
        {
            Console.Error.WriteLine($"error: data/rules not found above {AppContext.BaseDirectory}");
            return 1;
        }

        SkirmishGame game = SkirmishGame.Create(rulesDir, 0, 1);
        var host = new SimHost(game.Sim) { Ai = game.Ai };
        host.StepTicks(ticks);

        Simulation sim = game.Sim;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"skirmish ok tick {sim.Tick} ai {game.AiId} cash0 {sim.Players.Cash[0]} cash1 {sim.Players.Cash[1]} "
            + $"buildings0 {CountBuildings(sim, 0)} buildings1 {CountBuildings(sim, 1)} game_over {sim.GameOver}"));
        return 0;
    }

    private static int CountBuildings(Simulation sim, byte player)
    {
        int n = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player) n++;
        return n;
    }
}
