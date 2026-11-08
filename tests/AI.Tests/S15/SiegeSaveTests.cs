// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Xunit;

namespace AI.Tests.S15;

/// <summary>S15: the siege memory is part of the AI save (save -> load continues exactly like a game that never saved).</summary>
public sealed class SiegeSaveTests
{
    [Fact]
    public void AMilitaryManagerWithSiegeMemoryRoundTripsByteForByte()
    {
        var m = new MilitaryManager(8) { Options = MilitaryOptions.Recruit with { Tier = 4, Artillery = true } };
        var w1 = new MemoryStream();
        using (var bw = new BinaryWriter(w1, System.Text.Encoding.UTF8, true)) m.Snapshot(bw);
        var m2 = new MilitaryManager(8) { Options = m.Options };
        w1.Position = 0;
        using (var br = new BinaryReader(w1)) m2.Restore(br);
        var w2 = new MemoryStream();
        using (var bw = new BinaryWriter(w2, System.Text.Encoding.UTF8, true)) m2.Snapshot(bw);
        Assert.Equal(w1.ToArray(), w2.ToArray());
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void AnAiSavedAfterItsFirstSiegeWavesContinuesLikeOneThatNeverSaved(Faction faction)
    {
        // a defended enemy base (four armed structures beside its yard) so the siege rules engage; tier 4 sieges and holds artillery back
        const int Before = 7000, After = 1500;
        DifficultyProfile tier = TestWorld.Tiers[3];
        const string gun = "auth_turret";
        void Setup(TierRig rig)
        {
            CellCoord yard = rig.Yard(1);
            for (int i = 0; i < 4; i++) rig.PlaceNear(1, gun, new CellCoord(yard.X + 3 * (i % 2), yard.Y + 3 * (i / 2)));
        }
        var straight = new TierRig(faction, tier, Setup);
        straight.Run(Before + After);
        var r = new TierRig(faction, tier, Setup);
        r.Run(Before);
        byte[]? blob = r.CaptureAi();
        Assert.NotNull(blob);
        Assert.Equal(straight.Sim.ComputeStateHash(), r.ResumeWithBlob(blob!, After));
    }
}
