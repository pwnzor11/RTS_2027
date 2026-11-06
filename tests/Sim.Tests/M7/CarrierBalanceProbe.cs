// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Naval;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M7;

/// <summary>Balance probe for D-156 (docs/balance/s12_ammo.md): carrier group at sea against groups on the shore, with and without ammunition.
/// Runs only when the environment variable RTS_BALANCE_PROBE names an output file; otherwise it does nothing.</summary>
public sealed class CarrierBalanceProbe
{
    private const int W = 64, H = 40, Shore = 28;

    private static (int Own, int Total, int OwnHp) Run(SimRules rules, string carrierSpec, string foeSpec, int gap, int ticks, bool noAmmo, out int endTick, out string log)
    {
        if (noAmmo) rules = rules with { Vehicles = rules.Vehicles.Select(v => v with { Ammo = 0, RearmTicks = 0 }).ToArray() };
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = x < Shore ? LandType.Water : LandType.Clear;
        var map = new MapData("probe", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(2, 2), new CellCoord(W - 3, H - 3) });
        var sim = new Simulation(new SimSetup(7, map, 2, rules, 0xABCDUL) { AllowDebug = true });
        int cy = H / 2;
        int T(string id) => SimRules.IndexOf(rules.Vehicles, id, t => t.Id);
        var mine = new List<EntityId>(); EntityId carrier = default;
        var mineAir = new List<EntityId>();
        foreach (string part in carrierSpec.Split(','))
        {
            var p = part.Split(':'); int n = p.Length > 1 ? int.Parse(p[1]) : 1;
            for (int k = 0; k < n; k++)
            {
                EntityId id = sim.SpawnVehicle(0, T(p[0]), new CellCoord(Shore - gap, cy + (mine.Count % 3) - 1));
                mine.Add(id);
                if (p[0] == "exp_carrier") carrier = id; else mineAir.Add(id);
            }
        }
        var foes = new List<(EntityId Id, ActorKind Kind)>();
        int slot = 0;
        foreach (string part in foeSpec.Split(','))
        {
            var p = part.Split(':'); int n = p.Length > 1 ? int.Parse(p[1]) : 1;
            for (int k = 0; k < n; k++, slot++)
            {
                if (T(p[0]) >= 0) foes.Add((sim.SpawnVehicle(1, T(p[0]), new CellCoord(Shore + 1 + slot / 8, cy - 4 + slot % 8)), ActorKind.Vehicle));
            }
        }
        ushort seq = 0;
        var cmds = new List<Command>();
        // dock the aircraft
        foreach (EntityId a in mineAir) cmds.Add(new Command(0, 0, seq++, CommandKind.Enter, a, carrier, default, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8)));
        foreach (var (id, kind) in foes) cmds.Add(new Command(0, 1, seq++, CommandKind.AttackMove, id, EntityId.None, new CellCoord(Shore - gap, cy), (int)kind));
        int ownMax = 0; foreach (EntityId id in mine) ownMax += rules.Vehicles[sim.Units.TypeIndex[id.Index]].MaxHealth;
        var sb = new StringBuilder();
        endTick = ticks;
        for (int t = 0; t < ticks; t++)
        {
            sim.Step(cmds); cmds.RemoveAll(c => c.ExecTick < sim.Tick);
            if (t % 10 == 0 && sim.Units.Slots.IsAlive(carrier))
            {
                // launch policy: a full wave, i.e. every aircraft of the hangar is docked and (if ammo is used) full
                int docked = 0, total = 0, full = 0;
                for (int j = 1; j < sim.Units.Capacity; j++)
                    if (sim.Naval.HangarOf[j] == carrier.Index && sim.Units.Slots.IsIndexAlive(j))
                    {
                        total++;
                        if (sim.Naval.AirState[j] == NavalState.AirDocked) { docked++; VehicleType vt = rules.Vehicles[sim.Units.TypeIndex[j]]; if (vt.Ammo == 0 || sim.Naval.AmmoLeft[j] >= vt.Ammo) full++; }
                    }
                if (total > 0 && full == total)
                {
                    ActorRef best = default; long bd = long.MaxValue;
                    LeptonCoord cp = sim.Units.Position[carrier.Index];
                    foreach (var (id, kind) in foes)
                    {
                        if (!sim.Units.Slots.IsAlive(id) || sim.Units.Health[id.Index] <= 0) continue;
                        long d = LeptonMath.DistanceSquared(cp, sim.Units.Position[id.Index]);
                        if (d < bd) { bd = d; best = new ActorRef(kind, id); }
                    }
                    if (!best.Id.IsNone)
                        cmds.Add(new Command(sim.Tick, 0, seq++, CommandKind.Attack, carrier, best.Id, default, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8)));
                }
            }
            if (t % 4 == 0)
            {
                bool foesAlive = foes.Any(f => sim.Units.Slots.IsAlive(f.Id) && sim.Units.Health[f.Id.Index] > 0);
                bool mineAlive = mine.Any(m => sim.Units.Slots.IsAlive(m) && sim.Units.Health[m.Index] > 0);
                if (!foesAlive || !mineAlive) { endTick = sim.Tick; break; }
            }
        }
        int own = mine.Count(m => sim.Units.Slots.IsAlive(m) && sim.Units.Health[m.Index] > 0);
        int hp = mine.Where(m => sim.Units.Slots.IsAlive(m)).Sum(m => Math.Max(0, sim.Units.Health[m.Index]));
        int foeAlive = foes.Count(f => sim.Units.Slots.IsAlive(f.Id) && sim.Units.Health[f.Id.Index] > 0);
        int foeHp = foes.Where(f => sim.Units.Slots.IsAlive(f.Id)).Sum(f => Math.Max(0, sim.Units.Health[f.Id.Index]));
        int foeMax = foes.Sum(f => rules.Vehicles[sim.Units.TypeIndex[f.Id.Index]].MaxHealth);
        log = $"{foeAlive}/{foes.Count} foes ({foeHp * 100 / Math.Max(1, foeMax)}% hp)";
        return (own, mine.Count, hp * 100 / Math.Max(1, ownMax));
    }

    private static int Cost(SimRules r, string spec) =>
        spec.Split(',').Sum(part => { var p = part.Split(':'); int n = p.Length > 1 ? int.Parse(p[1]) : 1; return n * r.Vehicles[SimRules.IndexOf(r.Vehicles, p[0], t => t.Id)].Cost; });

    [Fact]
    public void Probe()
    {
        string? outFile = Environment.GetEnvironmentVariable("RTS_BALANCE_PROBE");
        if (string.IsNullOrEmpty(outFile)) return;
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        SimRules rules = RulesLoader.Load(dir!).Sim;
        string[] fleets = (Environment.GetEnvironmentVariable("RTS_PROBE_FLEETS") ?? "exp_carrier:1,exp_jet:4|exp_carrier:1,exp_gunship:4|exp_carrier:1,exp_bomber:2,exp_gunship:2").Split('|');
        string[] foes = (Environment.GetEnvironmentVariable("RTS_PROBE_FOES") ?? "dir_rocket_truck:7|auth_interceptor:10|auth_tank:7|auth_howitzer:7|exp_sparker:10").Split('|');
        int gap = int.Parse(Environment.GetEnvironmentVariable("RTS_PROBE_GAP") ?? "6");
        var sb = new StringBuilder();
        sb.AppendLine("| carrier group | cost | foe group | cost | ammo | result | own alive | own hp | foes left | end tick |");
        sb.AppendLine("|---|---:|---|---:|---|---|---:|---:|---|---:|");
        foreach (string f in fleets)
            foreach (string e in foes)
                foreach (bool noAmmo in new[] { true, false })
                {
                    var (own, tot, hp) = Run(rules, f, e, gap, 8000, noAmmo, out int end, out string log);
                    string res = own > 0 && log.StartsWith("0/") ? "carrier group wins" : own == 0 ? "foes win" : "stalemate";
                    sb.AppendLine($"| {f} | {Cost(rules, f)} | {e} | {Cost(rules, e)} | {(noAmmo ? "unlimited" : "ammo")} | {res} | {own}/{tot} | {hp}% | {log} | {end} |");
                }
        File.WriteAllText(outFile, sb.ToString());
    }
}
