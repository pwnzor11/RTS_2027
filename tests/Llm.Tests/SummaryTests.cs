// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;
using Rts.Sim;
using Rts.Sim.Core;

namespace Llm.Tests;

public class SummaryTests
{
    private static string Norm(string s) => s.Replace("\r\n", "\n");

    /// <summary>Fixed scene: player 0 has a small base, army and a damaged building; player 1 has a conscript in view.</summary>
    private static (Simulation Sim, SummaryHistory History) Scene()
    {
        var sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        sim.PlaceBuilding(0, SummaryRig.Building("auth_refinery"), new CellCoord(10, 5));
        sim.PlaceBuilding(0, SummaryRig.Building("auth_power"), new CellCoord(5, 10));
        sim.PlaceBuilding(0, SummaryRig.Building("auth_barracks"), new CellCoord(10, 10));
        sim.SpawnVehicle(0, SummaryRig.Vehicle("auth_scraper"), new CellCoord(8, 8));
        sim.SpawnVehicle(0, SummaryRig.Vehicle("auth_tank"), new CellCoord(14, 8));
        sim.SpawnVehicle(0, SummaryRig.Vehicle("auth_tank"), new CellCoord(15, 8));
        sim.SpawnInfantry(0, SummaryRig.Infantry("auth_rifleman"), new CellCoord(12, 14));
        sim.SpawnInfantry(0, SummaryRig.Infantry("auth_rifleman"), new CellCoord(13, 14), 1);
        sim.SpawnInfantry(1, SummaryRig.Infantry("dir_conscript"), new CellCoord(18, 8));
        sim.PlaceBuilding(1, SummaryRig.Building("dir_shaft"), new CellCoord(55, 55));
        sim.Players.Cash[0] = 1234;
        SummaryRig.Run(sim, 20 * 95 + 7);
        var h = new SummaryHistory();
        h.Observe(sim.ViewFor(0));
        h.RecordDirective(new DirectiveDraft(1, 0, 255, 0, false, -1, -1), 20 * 30);
        h.RecordDirective(new DirectiveDraft(2, 2, 1, 2, true, 20, 22), 20 * 60);
        return (sim, h);
    }

    [Fact]
    public void GoldenSummaryMatches()
    {
        var (sim, h) = Scene();
        string text = Norm(BattlefieldSummary.Build(sim.ViewFor(0), h, "Come and \"get\" me,\ncoward!"));
        string path = Path.Combine(SummaryRig.GoldenDir(), "summary_basic.txt");
        if (Environment.GetEnvironmentVariable("RTS_UPDATE_GOLDEN") == "1") File.WriteAllText(path, text);
        Assert.Equal(Norm(File.ReadAllText(path)), text);
    }

    [Fact]
    public void SummaryIsDeterministic()
    {
        var (s1, h1) = Scene();
        var (s2, h2) = Scene();
        Assert.Equal(BattlefieldSummary.Build(s1.ViewFor(0), h1, "hi"), BattlefieldSummary.Build(s2.ViewFor(0), h2, "hi"));
    }

    [Fact]
    public void ChatIsQuotedAndCapped()
    {
        var (sim, h) = Scene();
        string text = BattlefieldSummary.Build(sim.ViewFor(0), h, new string('x', 900));
        string chat = text[text.IndexOf("PLAYER CHAT", StringComparison.Ordinal)..];
        Assert.True(chat.Length <= "PLAYER CHAT (to you): \"\"".Length + BattlefieldSummary.MaxChatChars);
        Assert.DoesNotContain("PLAYER CHAT", BattlefieldSummary.Build(sim.ViewFor(0), h));
    }

    [Fact]
    public void LossesAndAttacksAreReported()
    {
        var (sim, h) = Scene();
        EntityId tank = sim.Units.Slots.IdOf(2);   // second vehicle spawned: first tank
        SummaryRig.Kill(sim, 0, tank, ActorKind.Vehicle);
        h.Observe(sim.ViewFor(0));
        string text = BattlefieldSummary.Build(sim.ViewFor(0), h);
        Assert.Contains("You lost: 1xauth_tank", text);
        h.Observe(sim.ViewFor(0));
        Assert.DoesNotContain("You lost", BattlefieldSummary.Build(sim.ViewFor(0), h));
    }

    [Fact]
    public void BigGameStaysUnderTheCap()
    {
        var sim = SummaryRig.Sim(4, 96);
        var rules = SummaryRig.Rules.Sim;
        int n = 0;
        for (byte p = 0; p < 4; p++)
        {
            for (int v = 0; v < rules.Vehicles.Count; v++)
                for (int k = 0; k < 3; k++) sim.SpawnVehicle(p, v, new CellCoord(2 + (n++ % 60), 2 + (n / 60) * 2));
            for (int i = 0; i < rules.Infantry.Count; i++)
                for (int k = 0; k < 4; k++) sim.SpawnInfantry(p, i, new CellCoord(2 + (n++ % 60), 2 + (n / 60) * 2), k % 5);
            for (int b = 0; b < rules.Buildings.Count; b++)
                try { sim.PlaceBuilding(p, b, new CellCoord(2 + (b % 12) * 7, 60 + p * 8 + (b / 12) * 3)); } catch (InvalidOperationException) { }
        }
        SummaryRig.Run(sim, 5);
        var h = new SummaryHistory();
        h.Observe(sim.ViewFor(0));
        for (int i = 0; i < 5; i++) h.RecordDirective(new DirectiveDraft(3, 4, 2, 4, true, 10, 10), 600 * i);
        string text = BattlefieldSummary.Build(sim.ViewFor(0), h, new string('y', 1000));
        Assert.True(text.Length <= BattlefieldSummary.MaxChars, "length " + text.Length);
        Assert.Contains("TIME 00:00", text);
        Assert.Equal(3, h.Directives.Count);
    }
}
