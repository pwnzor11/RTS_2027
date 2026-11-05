// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Replay;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit.Abstractions;

namespace AI.Tests.Soak;

/// <summary>
/// M6-12 acceptance soak. Plays AI-vs-AI games of the three playable factions, records each into memory, re-runs the replay and
/// compares every recorded hash (desync check), and watches both sides for packed rigs idling and mobile units sitting on a move order.
/// By default it runs one small smoke game. With <c>RTS_SOAK=1</c> it plays a whole cell range, set by <c>RTS_SOAK_PAIRS</c>
/// (e.g. <c>authority-leftbehind,leftbehind-leftbehind</c>), <c>RTS_SOAK_TIERS</c> (<c>1,3,5</c>), <c>RTS_SOAK_SIZES</c> (cells per side,
/// <c>64,96,128</c>), <c>RTS_SOAK_SEEDS</c> (<c>1-2</c>) and <c>RTS_SOAK_TICKS</c> (30000), and appends one line per game to the file in
/// <c>RTS_SOAK_OUT</c>. Each seed is played twice with the sides swapped between the slots; even seeds also swap the start corners.
/// </summary>
public sealed class SoakTests(ITestOutputHelper log)
{
    private const int SampleEvery = 20;
    private const int IdleRigLimit = 600;     // ticks (30 s) a packed rig may stand idle before it counts as stuck
    private const int StuckMoveLimit = 1200;  // ticks (60 s) a unit may keep a Move / AttackMove order without leaving its cell

    private static Faction ParseFaction(string s) => s switch
    {
        "authority" => Faction.Authority, "directorate" => Faction.Directorate, "leftbehind" => Faction.LeftBehind,
        _ => throw new ArgumentException($"unknown faction '{s}'"),
    };

    /// <summary>Smoke: one short Left Behind vs Directorate game replays to the same hashes.</summary>
    [Fact]
    public void ALeftBehindGameReplaysWithoutDesync()
    {
        if (Environment.GetEnvironmentVariable("RTS_SOAK") == "1") { RunSoak(); return; }
        GameLine g = PlayOne(Faction.LeftBehind, Faction.Directorate, 1, 56, 1, 0, 2500);
        log.WriteLine(g.ToString());
        Assert.False(g.Desync, g.ToString());
        Assert.Null(g.Exception);
    }

    private void RunSoak()
    {
        string pairs = Environment.GetEnvironmentVariable("RTS_SOAK_PAIRS") ?? "authority-authority";
        int[] tiers = (Environment.GetEnvironmentVariable("RTS_SOAK_TIERS") ?? "3").Split(',').Select(int.Parse).ToArray();
        int[] sizes = (Environment.GetEnvironmentVariable("RTS_SOAK_SIZES") ?? "64").Split(',').Select(int.Parse).ToArray();
        string[] seedRange = (Environment.GetEnvironmentVariable("RTS_SOAK_SEEDS") ?? "1-2").Split('-');
        int firstSeed = int.Parse(seedRange[0], CultureInfo.InvariantCulture), lastSeed = int.Parse(seedRange[^1], CultureInfo.InvariantCulture);
        int ticks = int.Parse(Environment.GetEnvironmentVariable("RTS_SOAK_TICKS") ?? "30000", CultureInfo.InvariantCulture);
        string? outFile = Environment.GetEnvironmentVariable("RTS_SOAK_OUT");
        foreach (string pair in pairs.Split(','))
        {
            string[] ab = pair.Split('-');
            Faction fa = ParseFaction(ab[0]), fb = ParseFaction(ab[1]);
            foreach (int size in sizes)
                foreach (int tier in tiers)
                    for (int seed = firstSeed; seed <= lastSeed; seed++)
                        for (int game = 0; game < 2; game++)
                        {
                            GameLine g = PlayOne(fa, fb, tier, size, seed, game, ticks);
                            log.WriteLine(g.ToString());
                            if (outFile is not null) File.AppendAllText(outFile, g + "\n");
                        }
        }
    }

    private sealed record GameLine(string Pair, int Tier, int Size, int Seed, int Game, int Winner, int EndTick, bool Desync, string? DesyncDetail,
        int IdleRigA, int IdleRigB, int StuckUnitsA, int StuckUnitsB, string? Exception)
    {
        public override string ToString() =>
            $"pair={Pair} tier={Tier} size={Size} seed={Seed} game={Game} winner={(Winner == 0 ? "A" : Winner == 1 ? "B" : "draw")} end={EndTick} " +
            $"desync={(Desync ? "YES " + DesyncDetail : "no")} idleRig={IdleRigA}/{IdleRigB} stuckUnits={StuckUnitsA}/{StuckUnitsB} exception={Exception ?? "none"}";
    }

    private static GameLine PlayOne(Faction fa, Faction fb, int tier, int size, int seed, int game, int ticks)
    {
        string pair = $"{fa}-{fb}";
        try
        {
            DifficultyProfile profile = TestWorld.Tiers[tier - 1];
            MapData map = MapGenerator.Generate(size, (ulong)seed);
            bool swapStarts = seed % 2 == 0;
            var sideA = new AiSide(AiRegistry.TieredId, profile, fa);
            var sideB = new AiSide(AiRegistry.TieredId, profile, fb);
            bool aInSlot0 = game == 0;
            var watch = new Watch();
            using var ms = new MemoryStream();
            AiGameResult r = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, (ulong)(seed * 16 + game), sideA, sideB,
                aInSlot0, ticks, ms, BuildInfo.Version, null, swapStarts, AiStartMode.Economy, watch.Observe);

            // Desync check: re-run the recording and compare every hash.
            ms.Position = 0;
            ReplayData data = ReplayReader.Read(ms);
            MapData playMap = map;
            if (swapStarts)
            {
                var starts = new List<CellCoord>(map.StartPositions);
                starts.Reverse();
                playMap = new MapData(map.Name, map.Theater, map.Width, map.Height, map.Template.ToArray(), map.Land.ToArray(), map.HeightLevel.ToArray(),
                    map.Overlay.ToArray(), map.Density.ToArray(), starts);
            }
            ReplayResult rr = ReplayPlayer.Run(data, data.Header.ToSetup(playMap, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash));
            bool desync = rr.FirstMismatchTick >= 0 || rr.FinalHash != r.FinalHash;
            string? detail = desync ? $"first mismatch tick {rr.FirstMismatchTick} part {rr.FirstMismatchPart}, final {rr.FinalHash:x16} vs {r.FinalHash:x16}" : null;

            // watch is indexed by slot; report by side A / side B.
            int slotA = aInSlot0 ? 0 : 1, slotB = 1 - slotA;
            return new GameLine(pair, tier, size, seed, game, r.Winner, r.EndTick, desync, detail,
                watch.IdleRig[slotA], watch.IdleRig[slotB], watch.StuckUnits(slotA), watch.StuckUnits(slotB), null);
        }
        catch (Exception e)
        {
            return new GameLine(pair, tier, size, seed, game, -1, 0, false, null, 0, 0, 0, 0, e.GetType().Name + ": " + e.Message.ReplaceLineEndings(" "));
        }
    }

    /// <summary>Per-slot watch: longest idle spell of a packed rig and the set of units that held a move order in one cell too long.</summary>
    private sealed class Watch
    {
        public readonly int[] IdleRig = new int[2];
        private readonly int[] _idleRun = new int[2];
        private readonly HashSet<(int Slot, EntityId Id)>[] _flagged = [new(), new()];
        private readonly Dictionary<(int Slot, EntityId Id), (CellCoord Cell, int Since)> _last = new();
        private readonly List<ViewActor> _own = new();

        public int StuckUnits(int slot) => _flagged[slot].Count;

        public void Observe(Simulation sim, IAiPlayer a, IAiPlayer b, byte slotA, byte slotB)
        {
            if (sim.Tick % SampleEvery != 0) return;
            for (int p = 0; p < 2; p++)
            {
                if (sim.Players.Defeated[p]) continue;
                PlayerView view = sim.ViewFor((byte)p);
                _own.Clear();
                view.CopyOwn(_own, includeBuildings: false);
                bool idlePacked = false;
                foreach (ViewActor v in _own)
                {
                    if (v.Kind == ActorKind.Vehicle && view.IsPackedRig(v.Id) && v.Idle && view.PackProgressPermille(v.Ref) == 0) idlePacked = true;
                    var key = (p, v.Id);
                    bool moving = v.Mission is MissionKind.Move or MissionKind.AttackMove && !v.Underground;
                    if (!moving) { _last.Remove(key); continue; }
                    if (!_last.TryGetValue(key, out var prev) || prev.Cell != v.Cell) { _last[key] = (v.Cell, sim.Tick); continue; }
                    if (sim.Tick - prev.Since >= StuckMoveLimit) _flagged[p].Add(key);
                }
                _idleRun[p] = idlePacked ? _idleRun[p] + SampleEvery : 0;
                IdleRig[p] = Math.Max(IdleRig[p], _idleRun[p]);
            }
        }
    }
}
