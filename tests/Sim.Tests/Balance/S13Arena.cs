// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Balance;

/// <summary>One in-memory rules edit for a what-if run: file, [section], key, new value text. Never written to disk.</summary>
internal readonly record struct S13Edit(string File, string Section, string Key, string Value);

/// <summary>
/// S13 harness: like <see cref="BalanceArena"/> but the rules can carry in-memory edits (the shipped <c>data/rules</c> files are read
/// and patched as text, never written back). Used only to MEASURE candidate numbers for the owner (D-152).
/// </summary>
internal sealed class S13Arena
{
    private static ushort _seq;
    public readonly SimRules Rules;
    public readonly Simulation Sim;

    public S13Arena(IReadOnlyList<S13Edit>? edits = null, ulong seed = 7)
    {
        Rules = LoadRules(edits);
        Sim = new Simulation(new SimSetup(seed, MapData.CreateFlat(96, 96), 2, Rules, 0xBA1AUL) { AllowDebug = true });
        Assert.False(Sim.PlaceBuilding(0, Building("auth_power"), new CellCoord(4, 4)).IsNone);    // keep-alive, out of every fight
        Assert.False(Sim.PlaceBuilding(1, Building("auth_power"), new CellCoord(90, 90)).IsNone);
    }

    /// <summary>Shipped rules with the edits applied to the file text. Throws if an edit does not match exactly one key.</summary>
    public static SimRules LoadRules(IReadOnlyList<S13Edit>? edits)
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        string? Read(string file)
        {
            string path = Path.Combine(dir!, file);
            if (!File.Exists(path)) return null;
            string text = File.ReadAllText(path);
            if (edits != null)
                foreach (S13Edit e in edits.Where(x => x.File == file)) text = Patch(text, e);
            return text;
        }
        return RulesLoader.Load(Read).Sim;
    }

    private static string Patch(string text, S13Edit e)
    {
        var sec = Regex.Match(text, @"^\[" + Regex.Escape(e.Section) + @"\][^\n]*\n(?<body>(?:(?!\[)[^\n]*\n?)*)", RegexOptions.Multiline);
        Assert.True(sec.Success, $"section {e.Section}");
        Group body = sec.Groups["body"];
        var key = Regex.Match(body.Value, @"^" + Regex.Escape(e.Key) + @"\s*=\s*[^#\r\n]*", RegexOptions.Multiline);
        Assert.True(key.Success, $"key {e.Section}.{e.Key}");
        int at = body.Index + key.Index;
        return text.Substring(0, at) + e.Key + " = " + e.Value + " " + text.Substring(at + key.Length);
    }

    public int Building(string id) { int i = SimRules.IndexOf(Rules.Buildings, id, b => b.Id); Assert.True(i >= 0, id); return i; }
    public int Inf(string id) { int i = SimRules.IndexOf(Rules.Infantry!, id, t => t.Id); Assert.True(i >= 0, id); return i; }
    public int Veh(string id) { int i = SimRules.IndexOf(Rules.Vehicles, id, t => t.Id); Assert.True(i >= 0, id); return i; }
    public int InfCost(string id) => Rules.Infantry![Inf(id)].Cost;

    public Command Cmd(byte player, CommandKind kind, EntityId subject = default, EntityId target = default, CellCoord cell = default,
        int arg = 0, int arg2 = 0) => new(Sim.Tick, player, _seq++, kind, subject, target, cell, arg, arg2);

    public void Step(params Command[] cmds) => Sim.Step(cmds);

    /// <summary>Spawns infantry in rows of <paramref name="perRow"/> cells starting at <paramref name="origin"/>, five to a cell.</summary>
    public List<EntityId> Infantry(byte player, string type, int count, CellCoord origin)
    {
        var list = new List<EntityId>();
        for (int k = 0; k < count; k++)
            list.Add(Sim.SpawnInfantry(player, Inf(type), new CellCoord(origin.X + (k / 5) % 3, origin.Y + k / 15), k % 5));
        return list;
    }

    public bool InfAlive(EntityId id) => Sim.Infantry.Slots.IsAlive(id) && Sim.Infantry.Health[id.Index] > 0;
    public int InfHealth(EntityId id) => Sim.Infantry.Slots.IsAlive(id) ? Math.Max(0, Sim.Infantry.Health[id.Index]) : 0;

    public Command[] AttackMoveInf(byte player, IEnumerable<EntityId> units, CellCoord cell) =>
        units.Select(u => Cmd(player, CommandKind.AttackMove, u, default, cell, (int)ActorKind.Infantry)).ToArray();
}
