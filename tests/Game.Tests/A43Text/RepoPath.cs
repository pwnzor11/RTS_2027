// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests.A43Text;

/// <summary>The source repository root (the folder holding RTS_2027.slnx), found from the test binary's folder.</summary>
internal static class RepoPath
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RTS_2027.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("RTS_2027.slnx not found above the test binary");
    }
}
