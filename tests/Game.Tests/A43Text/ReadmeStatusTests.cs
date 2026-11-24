// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 TEXT-06: the README status block must carry the current session date, not the session 34 one.</summary>
public sealed class ReadmeStatusTests
{
    [Fact]
    public void Status_block_is_dated_after_session_34()
    {
        string readme = File.ReadAllText(Path.Combine(RepoPath.Root, "README.md"));
        Assert.DoesNotContain("Updated 2026-11-18 (session 34)", readme);
        Assert.Contains("Updated 2026-11-24 (session 43)", readme);
    }
}
