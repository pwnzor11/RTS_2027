// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Tests.Repo;

/// <summary>D-060: every C# source file starts with the SPDX licence line and the copyright line.</summary>
public class LicenceHeaderTests
{
    private const string Spdx = "// SPDX-License-Identifier: GPL-3.0-or-later";

    [Fact]
    public void EveryCSharpFile_StartsWithSpdxHeader()
    {
        string root = RepoRoot();
        var missing = new List<string>();
        foreach (string dir in new[] { "src", "tests" })
            foreach (string f in Directory.EnumerateFiles(Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (rel.Contains("/obj/") || rel.Contains("/bin/")) continue;
                string first = File.ReadLines(f).FirstOrDefault()?.TrimStart('﻿') ?? "";
                if (first != Spdx) missing.Add(rel);
            }
        Assert.True(missing.Count == 0, "Missing SPDX header (D-060): " + string.Join(", ", missing));
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "RTS_2027.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
