// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Lan;
using Rts.Game.Menu;
using Rts.Game.Sandbox;

namespace Rts.Game.Tests.Menu;

/// <summary>REPO-01: replays go under the per-user application-data folder (the root saves use), not the working directory.</summary>
public sealed class ReplayLocationTests
{
    [Fact]
    public void Replay_folder_is_under_the_same_app_data_root_as_saves()
    {
        string root = Path.GetDirectoryName(Settings.DefaultPath)!;
        Assert.Equal(Path.Combine(root, "replays"), ReplayCatalog.DefaultFolder);
        Assert.Equal(Path.Combine(root, "saves"), SaveCatalog.DefaultRoot);
    }

    [Fact]
    public void Recorded_replays_are_written_under_app_data()
    {
        string root = Path.Combine(Path.GetDirectoryName(Settings.DefaultPath)!, "replays") + Path.DirectorySeparatorChar;
        string[] paths =
        {
            SimSnapshotSource.ReplayPath,
            SimSnapshotSource.SkirmishReplayPath,
            SimSnapshotSource.MissionReplayPath,
            LanSessionOptions.DefaultReplayPath,
        };
        foreach (string path in paths)
            Assert.StartsWith(root, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
    }
}
