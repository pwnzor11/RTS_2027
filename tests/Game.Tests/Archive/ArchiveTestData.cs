// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Archive;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Archive;

/// <summary>Loads the real archive and rules data once per call.</summary>
internal static class ArchiveTestData
{
    public static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;

    public static ArchiveCatalog Catalog() => ArchiveCatalog.Load(ArchiveCatalog.DirectoryFor(RulesDir));

    public static SimRules Rules() => RulesLoader.Load(RulesDir).Sim;
}
