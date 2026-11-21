// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace AI.Tests;

/// <summary>s30: the per-faction <c>mopup_hunt</c> key (default 0 = off, today's behaviour) and what the hunt changes in the sweep.</summary>
public class MopupHuntKeyTests
{
    private static string Write(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), $"diff_{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void ShippedFileLeavesTheKeyOffForEveryFaction()
    {
        var keys = TestWorld.Tiers[2].FactionKeys;
        foreach (Faction f in new[] { Faction.Authority, Faction.Directorate, Faction.LeftBehind, Faction.Expedition })
            Assert.False(keys.MopupHunt((int)f));
    }

    [Fact]
    public void KeyIsReadPerFactionAndRangeChecked()
    {
        string text = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir));
        string path = Write(text.Replace("\n[faction.expedition]\n", "\n[faction.expedition]\nmopup_hunt = 1\n"));
        try
        {
            var keys = DifficultyLoader.Load(path)[2].FactionKeys;
            Assert.True(keys.MopupHunt((int)Faction.Expedition));
            Assert.False(keys.MopupHunt((int)Faction.LeftBehind));
            Assert.False(keys.MopupHunt((int)Faction.Directorate));
            Assert.Equal(30, keys.HarassMinArmy((int)Faction.Expedition));
        }
        finally { File.Delete(path); }
        string bad = Write(text.Replace("\n[faction.expedition]\n", "\n[faction.expedition]\nmopup_hunt = 2\n"));
        try
        {
            var ex = Assert.Throws<RulesException>(() => DifficultyLoader.Load(bad));
            Assert.Contains(ex.Problems, p => p.Key == "faction.expedition.mopup_hunt");
        }
        finally { File.Delete(bad); }
    }

    // a 6 x 6 block grid (8-cell blocks, 48 x 48 cells), every block stale (never seen)
    private const int Blocks = 6, Map = 48;
    private static CellCoord? Pick(MopupPlan.HuntHints? hunt, CellCoord from, int[]? stamp = null, int tick = 20000) =>
        MopupPlan.PickSweep(TestWorld.Rules.Sim, stamp ?? new int[Blocks * Blocks], Blocks, Map, Map, tick, from, new List<ViewActor>(), _ => true, _ => false, hunt);

    [Fact]
    public void WithoutHintsTheNearestStaleBlockIsPicked()
    {
        Assert.Equal(new CellCoord(4, 4), Pick(null, new CellCoord(2, 2)));
        Assert.Equal(new CellCoord(4, 4), Pick(new MopupPlan.HuntHints(new byte[Blocks * Blocks], null), new CellCoord(2, 2)));   // hints with nothing remembered: same goal
    }

    [Fact]
    public void AFormerEnemySiteBeatsANearerBlock()
    {
        var sites = new byte[Blocks * Blocks];
        sites[3 * Blocks + 3] = 1;   // block (3,3), centre (28,28), about 26 cells from the army
        Assert.Equal(new CellCoord(28, 28), Pick(new MopupPlan.HuntHints(sites, null), new CellCoord(2, 2)));
    }

    [Fact]
    public void TheLastSeenCellOfARigPullsTheSweepToItsSurroundings()
    {
        var hints = new MopupPlan.HuntHints(new byte[Blocks * Blocks], new CellCoord(44, 20));
        CellCoord goal = Pick(hints, new CellCoord(2, 2))!.Value;
        Assert.True(Math.Max(Math.Abs(goal.X - 44), Math.Abs(goal.Y - 20)) <= MopupPlan.RecentCells);
    }

    [Fact]
    public void HuntRevisitsSoonerThanTheDefault()
    {
        int tick = 20000, seen = tick - 4000 + 1;   // in view 4000 ticks ago
        Assert.False(MopupPlan.Stale(seen, tick));
        Assert.False(MopupPlan.Stale(seen, tick, false));
        Assert.True(MopupPlan.Stale(seen, tick, true));
        var stamp = new int[Blocks * Blocks];
        Array.Fill(stamp, tick);   // everything just seen
        Assert.Null(Pick(null, new CellCoord(2, 2), stamp));
        stamp[0] = seen;   // block (0,0) seen 4000 ticks ago
        Assert.Null(Pick(null, new CellCoord(2, 2), stamp));
        Assert.Equal(new CellCoord(4, 4), Pick(new MopupPlan.HuntHints(new byte[Blocks * Blocks], null), new CellCoord(2, 2), stamp));
    }

    [Fact]
    public void HuntSplitsASmallWaveIntoMoreParties()
    {
        Assert.Equal(1, MopupPlan.Parties(7));
        Assert.Equal(1, MopupPlan.Parties(7, false));
        Assert.Equal(2, MopupPlan.Parties(7, true));
        Assert.Equal(MopupPlan.MaxParties, MopupPlan.Parties(100, true));
    }

    [Fact]
    public void PackedRigsAndMcvLikeVehiclesAreTheDeployables()
    {
        var vehicles = TestWorld.Rules.Sim.Vehicles;
        Assert.Contains(vehicles, v => v.UnpacksInto >= 0 && v.Id == "lb_mother_rig_packed");
    }

    private static DifficultyProfile HuntTier()
    {
        string text = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir));
        string path = Write(text.Replace("\n[faction.authority]\n", "\n[faction.authority]\nmopup_hunt = 1\n"));
        try { return DifficultyLoader.Load(path)[3].WithoutFactionOverride((int)Faction.Authority); }
        finally { File.Delete(path); }
    }

    private static void FourGuns(TierRig rig)
    {
        CellCoord yard = rig.Yard(1);
        for (int i = 0; i < 4; i++) rig.PlaceNear(1, "auth_turret", new CellCoord(yard.X + 3 * (i % 2), yard.Y + 3 * (i / 2)));
    }

    private static int LayoutWord(RecruitAi ai)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) ((IAiSnapshotable)ai).SaveState(w);
        return BitConverter.ToInt32(ms.ToArray(), 0);
    }

    [Fact]
    public void WithTheKeyOffTheSaveCarriesNoHuntBlock()
    {
        var rig = new TierRig(Faction.Authority, TestWorld.Tiers[3], FourGuns);
        rig.Run(7000);
        Assert.Equal(0, LayoutWord(rig.Ai) & 0x200);
    }

    [Fact]
    public void HuntMemorySavedMidGameContinuesLikeAGameThatNeverSaved()
    {
        const int Before = 7000, After = 1500;
        DifficultyProfile tier = HuntTier();
        Assert.True(tier.FactionKeys.MopupHunt((int)Faction.Authority));
        var straight = new TierRig(Faction.Authority, tier, FourGuns);
        straight.Run(Before + After);
        var r = new TierRig(Faction.Authority, tier, FourGuns);
        r.Run(Before);
        Assert.NotEqual(0, LayoutWord(r.Ai) & 0x200);   // the AI has seen the enemy structures: the hunt block is in the save
        byte[]? blob = r.CaptureAi();
        Assert.NotNull(blob);
        Assert.Equal(straight.Sim.ComputeStateHash(), r.ResumeWithBlob(blob!, After));
    }
}
