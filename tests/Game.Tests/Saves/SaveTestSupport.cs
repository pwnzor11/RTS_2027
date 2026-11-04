// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Saves;

/// <summary>Shared set-up for the save and replay tests: the real rules folder and a scratch folder per test.</summary>
internal sealed class SaveTestSupport : IDisposable
{
    public SaveTestSupport()
    {
        RulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        Folder = Path.Combine(Path.GetTempPath(), "rts_save_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        Catalog = new SaveCatalog(Path.Combine(Folder, "saves"));
    }

    public string RulesDir { get; }
    public string Folder { get; }
    public SaveCatalog Catalog { get; }

    /// <summary>A skirmish host with no AI (so a run is a pure function of the sim and the commands), plus the game it came from.</summary>
    public (SimHost Host, SkirmishGame Game) NewSkirmish(ulong mapSeed = 0)
    {
        SkirmishGame game = SkirmishGame.Create(RulesDir, mapSeed, 2);
        return (new SimHost(game.Sim), game);
    }

    /// <summary>Issues a build order and a move for the first unit player 0 owns, if any, so the run has something to do.</summary>
    public static void GiveOrders(SimHost host)
    {
        host.Issue(new Command(0, 0, 0, CommandKind.Build, EntityId.None, EntityId.None, default, (int)Rts.Sim.Rules.ProductionCategory.Infantry, 0));
        var sim = host.Sim;
        for (int i = 1; i < sim.Units.Capacity; i++)
        {
            if (!sim.Units.Slots.IsIndexAlive(i) || sim.Units.Player[i] != 0) continue;
            host.IssueOrder(0, CommandKind.Move, new UnitKey(ActorKind.Vehicle, sim.Units.Slots.IdOf(i)), new CellCoord(20, 20));
            return;
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(Folder, recursive: true); }
        catch (IOException) { }
    }
}
