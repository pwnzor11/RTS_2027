// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Game.Ui.Spectator;

/// <summary>Builds the spectator stats rows and formats them as text (shared by the live panel and the game-over table).</summary>
public static class SpectatorTable
{
    /// <summary>One row per non-neutral player, in slot order, read from the sim as it stands.</summary>
    /// <param name="sim">The simulation (read between steps).</param>
    /// <param name="income">Supplies income per minute; null gives 0.</param>
    /// <param name="commanders">M5: per sim player, true when an LLM commander leads it (null or short = false).</param>
    public static IReadOnlyList<SpectatorRow> Collect(Simulation sim, IncomeTracker? income = null, IReadOnlyList<bool>? commanders = null)
    {
        int players = sim.Setup.PlayerCount;
        var army = new int[players];
        var structures = new int[players];
        var rules = sim.Setup.Rules;
        for (int i = 1; i < sim.Units.Capacity; i++)
        {
            if (!sim.Units.Slots.IsIndexAlive(i)) continue;
            int owner = sim.Units.Player[i];
            int type = sim.Units.TypeIndex[i];
            if (owner < players && type >= 0 && type < rules.Vehicles.Count) army[owner] += rules.Vehicles[type].Cost;
        }

        for (int i = 1; i < sim.Infantry.Capacity; i++)
        {
            if (!sim.Infantry.Slots.IsIndexAlive(i)) continue;
            int owner = sim.Infantry.Player[i];
            int type = sim.Infantry.TypeIndex[i];
            if (owner < players && type >= 0 && type < rules.Infantry.Count) army[owner] += rules.Infantry[type].Cost;
        }

        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] < players) structures[sim.Buildings.Player[i]]++;

        var rows = new List<SpectatorRow>(players);
        for (int p = 0; p < players; p++)
        {
            if (sim.Players.IsNeutral((byte)p)) continue;
            rows.Add(new SpectatorRow(p, sim.Players.Team[p], sim.Players.Faction[p], sim.TierOf((byte)p), sim.Players.Cash[p],
                sim.Players.Stored[p], income?.IncomePerMinute(p) ?? 0, army[p], structures[p], sim.Players.PowerOut[p],
                sim.Players.PowerUse[p], sim.StatsOf((byte)p), System.Numerics.BitOperations.PopCount(sim.Players.Upgrades[p]),
                sim.Players.Defeated[p], commanders is not null && p < commanders.Count && commanders[p]));
        }

        return rows;
    }

    private static string Tag(SpectatorRow r) => r.Commander ? " " + Screens.SetupText.SpectatorTag : "";

    /// <summary>Compact one-line form for the top strip.</summary>
    public static string Compact(SpectatorRow r) =>
        $"P{r.Player + 1}{Tag(r)} {r.Faction} T{r.Tier}  ${r.Cash + r.Stored}  +{r.IncomePerMinute}/m  army {r.ArmyValue}  bld {r.Structures}  K{r.Stats.Kills}/L{r.Stats.UnitsLost}"
        + (r.Defeated ? "  OUT" : "");

    /// <summary>Header for <see cref="Detail"/> and <see cref="Final"/>.</summary>
    public const string DetailHeader = "PLAYER  FACTION    TIER  CASH  INCOME/M  ARMY  STRUCT  POWER  BUILT  LOST  KILLS  UPG";

    /// <summary>Detail line (Tab): every column of the design table.</summary>
    public static string Detail(SpectatorRow r) =>
        $"{"P" + (r.Player + 1) + Tag(r),-7} {r.Faction,-10} {r.Tier,-5} {r.Cash + r.Stored,-5} {r.IncomePerMinute,-9} {r.ArmyValue,-5} {r.Structures,-7} "
        + $"{r.PowerOut}/{r.PowerUse,-4} {r.Stats.UnitsBuilt,-6} {r.Stats.UnitsLost,-5} {r.Stats.Kills,-6} {r.Upgrades}"
        + (r.Defeated ? "  OUT" : "");

    private static string[] DetailCells(SpectatorRow r) => new[]
    {
        "P" + (r.Player + 1) + Tag(r), r.Faction.ToString(), r.Tier.ToString(), (r.Cash + r.Stored).ToString(), r.IncomePerMinute.ToString(),
        r.ArmyValue.ToString(), r.Structures.ToString(), $"{r.PowerOut}/{r.PowerUse}", r.Stats.UnitsBuilt.ToString(), r.Stats.UnitsLost.ToString(),
        r.Stats.Kills.ToString(), r.Upgrades.ToString(), r.Defeated ? "OUT" : "",
    };

    private static readonly string[] HeaderCells =
        { "PLAYER", "FACTION", "TIER", "CASH", "INCOME/M", "ARMY", "STRUCT", "POWER", "BUILT", "LOST", "KILLS", "UPG", "" };

    /// <summary>
    /// S18 UXG-10: the final table with its columns lined up in a proportional font. Each cell is followed by as many spaces as bring the
    /// next column to the same pixel x on every line, using <paramref name="measure"/> (pixel width of a string at the size it is drawn),
    /// so the error is under one space width. The padding-by-character-count of <see cref="Final(IReadOnlyList{SpectatorRow})"/> only
    /// lines up in a fixed-width font.
    /// </summary>
    public static IReadOnlyList<string> Final(IReadOnlyList<SpectatorRow> rows, Func<string, int> measure)
    {
        var table = new List<string[]>(rows.Count + 1) { HeaderCells };
        foreach (SpectatorRow r in rows) table.Add(DetailCells(r));
        int columns = HeaderCells.Length;
        int space = Math.Max(1, measure("i i") - measure("ii"));
        var start = new int[columns + 1];
        for (int c = 0; c < columns; c++)
        {
            int widest = 0;
            foreach (string[] row in table) widest = Math.Max(widest, measure(row[c]));
            start[c + 1] = start[c] + widest + 2 * space;
        }

        var lines = new List<string>(table.Count);
        foreach (string[] row in table)
        {
            var line = new System.Text.StringBuilder();
            for (int c = 0; c < columns; c++)
            {
                line.Append(row[c]);
                if (c == columns - 1) break;
                int target = start[c + 1];
                while (true)
                {
                    int now = measure(line.ToString());
                    int next = measure(line.ToString() + " ");
                    if (Math.Abs(next - target) >= Math.Abs(now - target)) break;
                    line.Append(' ');
                }
            }

            lines.Add(line.ToString().TrimEnd());
        }

        return lines;
    }

    /// <summary>Lines of the final table shown at game over: the header, then one line per player.</summary>
    public static IReadOnlyList<string> Final(IReadOnlyList<SpectatorRow> rows)
    {
        var lines = new List<string>(rows.Count + 1) { DetailHeader };
        foreach (SpectatorRow r in rows) lines.Add(Detail(r));
        return lines;
    }
}
