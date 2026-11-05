// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.World;

// Balance probe (session 10, package bal). Plays the same games as `Tools ai match` (same seeds, same game-seed rule, same
// slot/start swapping) but samples supply, income, army and veterancy counters every 10 ticks and prints one TSV row per
// side per game. No game code is changed. Usage:
//   BalProbe --rules <data/rules> --fa authority --fb directorate --tier 3 --size 96 --first 1 --seeds 16 --ticks 30000

var flags = new Dictionary<string, string>();
for (int i = 0; i + 1 < args.Length; i += 2) flags[args[i].TrimStart('-')] = args[i + 1];
string F(string k, string d) => flags.TryGetValue(k, out string? v) ? v : d;
int I(string k, int d) => int.Parse(F(k, d.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

string rulesDir = F("rules", "data/rules");
var rules = RulesLoader.Load(rulesDir);
var tiers = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(rulesDir));
Faction fa = Enum.Parse<Faction>(F("fa", "authority").Replace("_", ""), true);
Faction fb = Enum.Parse<Faction>(F("fb", "directorate").Replace("_", ""), true);
var sideA = new AiSide(AiRegistry.TieredId, tiers[I("tier", 3) - 1], fa);
var sideB = new AiSide(AiRegistry.TieredId, tiers[I("tier", 3) - 1], fb);
int size = I("size", 96), first = I("first", 1), seeds = I("seeds", 16), ticks = I("ticks", 30000);
SimRules sr = rules.Sim;
SupplyRules sup = sr.Supply;

if (flags.ContainsKey("header"))
    Console.WriteLine("seed\tgame\tside\tfaction\twinner\tendTick\tsupplyOn\tvetOn\tcapSamples\tcapWindow\tcapSimMismatch\tsupBuildings\tarmy5\tarmy10\tused5\tcap5\tused10\tcap10\tharv10\tharv8to10\tunitsBuilt10"
        + "\tr1_10\tr2_10\tr3_10\tr1_20\tr2_20\tr3_20\talive10\talive20\ta3_10\ta3_20\tfirstRank3\tr3_15\talive15\ta3_15\treached20");

for (int s = first; s < first + seeds; s++)
{
    MapData map = MapGenerator.Generate(size, (ulong)s);
    for (int g = 0; g < 2; g++)
    {
        bool aInSlot0 = g == 0;
        bool swapSlots = s % 2 == 0;                       // "alt" start mode of `ai match`
        var tr = new Tracker(sr, sup);
        AiGameResult r = AiMatchRunner.Play(map, sr, rules.RulesHash, (ulong)(s * 16 + g), sideA, sideB, aInSlot0, ticks, null, "", null, swapSlots,
            AiStartMode.Economy, tr.Observe);
        tr.Finish();
        for (int side = 0; side < 2; side++)
        {
            byte slot = (byte)(side == 0 ? (aInSlot0 ? 0 : 1) : (aInSlot0 ? 1 : 0));
            SideRec x = tr.Sides[slot];
            string res = r.Winner < 0 ? "draw" : r.Winner == side ? "win" : "loss";
            Console.WriteLine(string.Join('\t', s, g + 1, side == 0 ? "A" : "B", side == 0 ? fa : fb, res, r.EndTick, sup.Enabled ? 1 : 0, sr.Veterancy.Enabled ? 1 : 0,
                x.CapSamples, x.CapWindow, x.Mismatch, x.SupBuilt, x.Army5, x.Army10, x.Used5, x.Cap5, x.Used10, x.Cap10, x.Harv10, x.Harv8to10, x.Built10,
                x.R[0], x.R[1], x.R[2], x.R[3], x.R[4], x.R[5], x.Alive10, x.Alive20, x.A3_10, x.A3_20, x.FirstRank3, x.R3_15, x.Alive15, x.A3_15, x.Reached20));
        }
    }
}
return 0;

sealed class SideRec
{
    public int CapSamples, CapWindow, Mismatch, SupBuilt, Army5 = -1, Army10 = -1, Used5 = -1, Cap5 = -1, Used10 = -1, Cap10 = -1;
    public int Harv10 = -1, Harv8to10 = -1, Built10 = -1, Alive10 = -1, Alive15 = -1, Alive20 = -1, A3_10 = -1, A3_15 = -1, A3_20 = -1, R3_15 = -1;
    public int FirstRank3 = -1, Reached20 = 0;
    public int[] R = [-1, -1, -1, -1, -1, -1];    // cumulative units that reached rank >=1,2,3 by min 10, then by min 20
    public int PrevSup, Harv9;
    public int[] Reach = new int[3];
}

sealed class Tracker(SimRules rules, SupplyRules sup)
{
    public readonly SideRec[] Sides = [new SideRec(), new SideRec()];
    private readonly byte[] _lastVeh = new byte[4096], _lastInf = new byte[4096];   // [player-free] last rank per slot
    private bool _init;
    private int _lastTick;
    private readonly bool[] _ended = new bool[2];

    public void Finish() { }

    private int SupplyOfVeh(int t) => sup.SupplyOf(rules.Vehicles[t].Cost, rules.Vehicles[t].Supply);
    private int SupplyOfInf(int t) => sup.SupplyOf(rules.Infantry[t].Cost, rules.Infantry[t].Supply);

    public void Observe(Simulation sim, IAiPlayer a0, IAiPlayer a1, byte slotA, byte slotB)
    {
        int tick = sim.Tick;
        _lastTick = tick;
        if (tick % 10 != 0) return;
        if (!_init) { _init = true; Array.Fill(_lastVeh, (byte)0); }
        bool vet = rules.Veterancy.Enabled;
        // one pass over every actor
        var army = new int[2]; var used = new int[2]; var alive = new int[2]; var a3 = new int[2]; var cap = new int[2]; var supB = new int[2];
        for (int i = 1; i < sim.Units.Capacity; i++)
        {
            if (!sim.Units.Slots.IsIndexAlive(i)) { _lastVeh[i] = 0; continue; }
            int p = sim.Units.Player[i]; if (p > 1) continue;
            VehicleType v = rules.Vehicles[sim.Units.TypeIndex[i]];
            int sp = SupplyOfVeh(sim.Units.TypeIndex[i]);
            used[p] += sp;
            if (v.HarvestCapacity <= 0) { army[p] += sp; }
            if (vet && v.CanRank) { alive[p]++; Rank(sim, ActorKind.Vehicle, i, p, _lastVeh, ref a3[p]); }
        }
        for (int i = 1; i < sim.Infantry.Capacity; i++)
        {
            if (!sim.Infantry.Slots.IsIndexAlive(i)) { _lastInf[i] = 0; continue; }
            int p = sim.Infantry.Player[i]; if (p > 1) continue;
            int sp = SupplyOfInf(sim.Infantry.TypeIndex[i]);
            used[p] += sp; army[p] += sp;
            if (vet && rules.Infantry[sim.Infantry.TypeIndex[i]].CanRank) { alive[p]++; Rank(sim, ActorKind.Infantry, i, p, _lastInf, ref a3[p]); }
        }
        for (int i = 1; i < sim.Buildings.Capacity; i++)
        {
            if (!sim.Buildings.Slots.IsIndexAlive(i)) continue;
            int p = sim.Buildings.Player[i]; if (p > 1) continue;
            BuildingType b = rules.Buildings[sim.Buildings.TypeIndex[i]];
            cap[p] += b.SupplyProvided;
            if (b.SupplyProvided > 0 && b.Cost > 0) supB[p]++;
        }
        for (int p = 0; p < 2; p++)
        {
            SideRec x = Sides[p];
            int capP = Math.Min(cap[p], sup.Ceiling);
            if (supB[p] > x.PrevSup) x.SupBuilt += supB[p] - x.PrevSup;
            x.PrevSup = supB[p];
            bool defeated = sim.Players.Defeated[p];
            if (tick >= 6000 && tick < 18000 && !defeated)
            {
                x.CapWindow++;
                if (capP > 0 && used[p] >= capP) x.CapSamples++;
                if (sup.Enabled && (sim.SupplyUsed((byte)p) != used[p] || sim.SupplyCap((byte)p) != capP)) x.Mismatch++;
            }
            PlayerStats st = sim.StatsOf((byte)p);
            if (tick == 6000) { x.Army5 = army[p]; x.Used5 = used[p]; x.Cap5 = capP; }
            if (tick == 9600) x.Harv9 = st.CreditsHarvested;
            if (tick == 12000)
            {
                x.Army10 = army[p]; x.Used10 = used[p]; x.Cap10 = capP; x.Harv10 = st.CreditsHarvested; x.Harv8to10 = st.CreditsHarvested - x.Harv9; x.Built10 = st.UnitsBuilt;
                x.R[0] = x.Reach[0]; x.R[1] = x.Reach[1]; x.R[2] = x.Reach[2]; x.Alive10 = alive[p]; x.A3_10 = a3[p];
            }
            if (tick == 18000) { x.Alive15 = alive[p]; x.A3_15 = a3[p]; x.R3_15 = x.Reach[2]; }
            if (tick == 24000)
            {
                x.R[3] = x.Reach[0]; x.R[4] = x.Reach[1]; x.R[5] = x.Reach[2]; x.Alive20 = alive[p]; x.A3_20 = a3[p];
            }
            x.Reached20 = x.Reach[2];
            if (x.Reach[2] > 0 && x.FirstRank3 < 0) x.FirstRank3 = tick;
        }
    }

    private void Rank(Simulation sim, ActorKind kind, int i, int p, byte[] last, ref int a3)
    {
        int rk = sim.RankOf(kind, i);
        if (rk == 3) a3++;
        int prev = last[i];
        if (rk > prev) for (int k = prev; k < rk; k++) Sides[p].Reach[k]++;
        last[i] = (byte)rk;
    }
}
