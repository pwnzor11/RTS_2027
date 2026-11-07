// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>Small helpers shared by the Expedition mission tests (acts 2-4): cell lists for buildings and floating docks, and a "keep training" loop.</summary>
internal static class ExpeditionMissionTestKit
{
    /// <summary>Top-left cell of the first live building of <paramref name="typeId"/> owned by <paramref name="player"/>.</summary>
    public static CellCoord BuildingCell(Simulation s, string typeId, byte player = 0)
    {
        int t = SimRules.IndexOf(s.Setup.Rules.Buildings, typeId, b => b.Id);
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == player && s.Buildings.TypeIndex[i] == t) return s.Buildings.Cell[i];
        throw new InvalidOperationException("no " + typeId);
    }

    /// <summary>Land cells on rings around <paramref name="c"/> (radius <paramref name="min"/>..<paramref name="max"/>), spread over a 3-cell grid so footprints up to 3x3 fit, in ring order.</summary>
    public static CellCoord[] Spots(Simulation s, CellCoord c, int min, int max, Func<CellCoord, bool>? keep = null)
    {
        var list = new List<CellCoord>();
        for (int r = min; r <= max; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var p = new CellCoord(c.X + dx, c.Y + dy);
                    if (!s.Setup.Map.Contains(p) || (p.X + p.Y) % 2 != 0) continue;
                    if (keep != null && !keep(p)) continue;
                    list.Add(p);
                }
        return list.ToArray();
    }

    private static bool IsWater(Simulation s, int x, int y)
    {
        var c = new CellCoord(x, y);
        return s.Setup.Map.Contains(c) && s.Map.LandAt(c) is LandType.Water or LandType.Shallow;
    }

    /// <summary>Top-left cells of 2x2 spots wholly on water that touch the shore (the floating-dock rule, D-156), nearest to <paramref name="near"/> first.</summary>
    public static CellCoord[] DockSpots(Simulation s, CellCoord near, int radius)
    {
        var list = new List<CellCoord>();
        for (int y = near.Y - radius; y <= near.Y + radius; y++)
            for (int x = near.X - radius; x <= near.X + radius; x++)
            {
                if (!IsWater(s, x, y) || !IsWater(s, x + 1, y) || !IsWater(s, x, y + 1) || !IsWater(s, x + 1, y + 1)) continue;
                bool touches = !IsWater(s, x - 1, y) || !IsWater(s, x + 2, y) || !IsWater(s, x, y - 1) || !IsWater(s, x, y + 2)
                    || !IsWater(s, x - 1, y + 1) || !IsWater(s, x + 2, y + 1) || !IsWater(s, x + 1, y - 1) || !IsWater(s, x + 1, y + 2);
                if (touches) list.Add(new CellCoord(x, y));
            }
        list.Sort((a, b) => (Math.Abs(a.X - near.X) + Math.Abs(a.Y - near.Y)).CompareTo(Math.Abs(b.X - near.X) + Math.Abs(b.Y - near.Y)));
        return list.ToArray();
    }

    /// <summary>Queues <paramref name="typeId"/> every 40 ticks (the queue ignores it when full) until <paramref name="count"/> stand or the tick limit is reached.</summary>
    public static void TrainUntil(MissionBot bot, string typeId, int count, int tickLimit)
    {
        for (int t = 0; bot.Count(typeId) < count && bot.Sim.Tick < tickLimit && !bot.Sim.GameOver; t++)
        {
            if (t % 40 == 0) bot.Train(typeId);
            else bot.Idle();
        }
    }

    private static ushort _seq = 40000;

    /// <summary>Orders every soldier and every ground (non-ship, non-air, non-harvester) vehicle of player 0 in one tick. Ships and aircraft are left alone so landing craft finish their landing.</summary>
    public static void OrderLand(Simulation s, CommandKind kind, CellCoord cell, byte player = 0)
    {
        var cmds = new List<Command>();
        for (int i = 1; i < s.Units.Capacity; i++)
        {
            if (!s.Units.Slots.IsIndexAlive(i) || s.Units.Player[i] != player) continue;
            VehicleType vt = s.Setup.Rules.Vehicles[s.Units.TypeIndex[i]];
            if (vt.HarvestCapacity > 0 || vt.Locomotion is Locomotion.Naval or Locomotion.Air) continue;
            cmds.Add(new Command(s.Tick, player, _seq++, kind, s.Units.Slots.IdOf(i), default, cell, (int)ActorKind.Vehicle));
        }
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == player)
                cmds.Add(new Command(s.Tick, player, _seq++, kind, s.Infantry.Slots.IdOf(i), default, cell, (int)ActorKind.Infantry));
        s.Step(cmds);
    }

    /// <summary>The cell of an enemy (any other player) soldier or ground vehicle within <paramref name="radius"/> cells (Chebyshev) of <paramref name="center"/>, nearest first; null when none.</summary>
    public static CellCoord? EnemyNear(Simulation s, CellCoord center, int radius, byte me = 0)
    {
        CellCoord? best = null; int bestD = int.MaxValue;
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] != me) Consider(s.Infantry.Position[i]);
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] != me && s.Setup.Rules.Vehicles[s.Units.TypeIndex[i]].Locomotion is not (Locomotion.Naval or Locomotion.Air)) Consider(s.Units.Position[i]);
        return best;

        void Consider(LeptonCoord p)
        {
            int x = p.X >> 8, y = p.Y >> 8;
            int d = Math.Max(Math.Abs(x - center.X), Math.Abs(y - center.Y));
            if (d <= radius && d < bestD) { bestD = d; best = new CellCoord(x, y); }
        }
    }

    /// <summary>Like <see cref="MissionBot.BuildAndPlace"/>, but the army keeps answering enemies near <paramref name="center"/> (else gathers on <paramref name="hold"/>) while the building is made.</summary>
    public static bool BuildDefending(MissionBot bot, string typeId, IReadOnlyList<CellCoord> spots, CellCoord center, CellCoord hold, int maxTicks = 3000)
    {
        int before = bot.Count(typeId);
        bot.Train(typeId);
        for (int t = 0; t < maxTicks && !bot.Sim.GameOver; t++)
        {
            if (t % 60 == 0) OrderLand(bot.Sim, CommandKind.AttackMove, EnemyNear(bot.Sim, center, 20) ?? hold);
            if (t % 10 == 0)
                foreach (CellCoord c in spots)
                    if (bot.TryPlace(typeId, c)) return true;
            bot.Idle();
        }
        return bot.Count(typeId) > before;
    }
}
