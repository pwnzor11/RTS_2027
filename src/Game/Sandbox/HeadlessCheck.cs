using System.Globalization;
using Rts.Data;
using Rts.Sim.Core;

namespace Rts.Game.Sandbox;

/// <summary>
/// <c>--headless-check &lt;ticks&gt;</c>: builds the sandbox without a window or graphics device, runs the scripted
/// fight through the same <see cref="SimSnapshotSource.Enqueue"/> path the window uses, saves the session replay and
/// prints one line: <c>sandbox ok tick n p0 a p1 b hash 0x...</c>. Player 0 also attack-moves its units at a fixed
/// tick so that a player-0 order is exercised as well. <c>--empty</c> runs the legacy 3-tank setup instead (no scripted
/// fight). Exit codes: 0 ok, 1 error, 2 usage.
/// </summary>
public static class HeadlessCheck
{
    private const string Flag = "--headless-check";
    private const int PlayerZeroAttackTick = 300;

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

        bool empty = args.Contains("--empty", StringComparer.Ordinal);
        var source = new SimSnapshotSource(rulesDir, empty);
        CellCoord enemyStart = source.Map.StartPositions[1];
        while (source.Tick < ticks)
        {
            if (source.Tick == PlayerZeroAttackTick)
            {
                foreach (UnitKey unit in source.AliveUnits(SimSnapshotSource.LocalPlayer))
                {
                    source.Order(CommandKind.AttackMove, unit, enemyStart);
                }
            }
            source.StepTicks(1);
        }

        int p0 = source.AliveUnits(0).Count;
        int p1 = source.AliveUnits(1).Count;
        Console.WriteLine($"sandbox ok tick {source.Tick} p0 {p0} p1 {p1} hash 0x{source.Sim.ComputeStateHash():x16}");
        return source.SaveReplay() ? 0 : 1;
    }
}
