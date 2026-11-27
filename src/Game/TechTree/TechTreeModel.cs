// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.TechTree;

/// <summary>
/// A faction's tech tree built from the loaded rules (U-12): nodes are its buildable structures, defences, units, aircraft and upgrades;
/// an edge runs from every node that satisfies a prerequisite (a building id, a provides name such as the tier gates, or an upgrade id) to
/// the node that lists it. Pure data, no graphics. Columns: nodes without prerequisites in column 0, every other node one column right of
/// its deepest prerequisite, so no edge runs backwards.
/// </summary>
public sealed class TechTreeModel
{
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    private TechTreeModel(Faction faction, IReadOnlyList<TechNode> nodes, IReadOnlyList<string> unresolved)
    {
        Faction = faction;
        Nodes = nodes;
        Unresolved = unresolved;
        for (int i = 0; i < nodes.Count; i++) _index[nodes[i].Id] = i;
        Columns = nodes.Count == 0 ? 0 : nodes.Max(n => n.Column) + 1;
    }

    /// <summary>The faction this tree belongs to.</summary>
    public Faction Faction { get; }

    /// <summary>All nodes, ordered by column, then category, then id.</summary>
    public IReadOnlyList<TechNode> Nodes { get; }

    /// <summary>Number of columns.</summary>
    public int Columns { get; }

    /// <summary>Prerequisite names no node of the faction satisfies, as "id: name" (empty when the data is consistent; such an entry can never be built).</summary>
    public IReadOnlyList<string> Unresolved { get; }

    /// <summary>The node with this id, or null.</summary>
    public TechNode? Find(string id) => _index.TryGetValue(id, out int i) ? Nodes[i] : null;

    /// <summary>Builds the tree of <paramref name="faction"/> from the rules.</summary>
    public static TechTreeModel Build(SimRules rules, Faction faction)
    {
        var raw = new List<Raw>();
        var provides = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        FactionStartKit? kit = (int)faction < rules.StartKits.Count ? rules.StartKits[(int)faction] : null;
        var startBuildings = new HashSet<string>(StringComparer.Ordinal);
        if (kit is not null && kit.Conyard.Length > 0) startBuildings.Add(kit.Conyard);
        foreach (VehicleType v in rules.Vehicles)
            if (v.Faction == faction && v.UnpacksInto >= 0 && v.UnpacksInto < rules.Buildings.Count) startBuildings.Add(rules.Buildings[v.UnpacksInto].Id);
        foreach (VehicleType v in rules.Vehicles)
            if (v.Faction == faction && v.DeploysInto >= 0 && v.DeploysInto < rules.Buildings.Count) startBuildings.Add(rules.Buildings[v.DeploysInto].Id);

        for (int i = 0; i < rules.Buildings.Count; i++)
        {
            BuildingType b = rules.Buildings[i];
            if (b.Faction != faction) continue;
            bool start = startBuildings.Contains(b.Id);
            if (!b.Buildable && !start) continue;
            raw.Add(new Raw(b.Id, b.Category, i, b.Tier, b.Cost, SimRules.BuildTicks(b.Cost, b.BuildTime, rules.Economy), b.Prerequisites, start && !b.Buildable));
            foreach (string p in b.Provides) Add(provides, p, b.Id);
        }
        for (int i = 0; i < rules.Infantry.Count; i++)
        {
            InfantryType u = rules.Infantry[i];
            if (u.Faction != faction) continue;
            raw.Add(new Raw(u.Id, ProductionCategory.Infantry, i, u.Tier, u.Cost, SimRules.BuildTicks(u.Cost, u.BuildTime, rules.Economy), u.Prerequisites, false));
        }
        for (int i = 0; i < rules.Vehicles.Count; i++)
        {
            VehicleType v = rules.Vehicles[i];
            if (v.Faction != faction || !v.Buildable) continue;
            ProductionCategory cat = v.Locomotion == Locomotion.Air ? ProductionCategory.Aircraft : ProductionCategory.Vehicle;
            raw.Add(new Raw(v.Id, cat, i, v.Tier, v.Cost, SimRules.BuildTicks(v.Cost, v.BuildTime, rules.Economy), v.Prerequisites, false));
        }
        for (int i = 0; i < rules.Upgrades.Count; i++)
        {
            UpgradeType u = rules.Upgrades[i];
            if (u.Faction != faction) continue;
            raw.Add(new Raw(u.Id, ProductionCategory.Upgrade, i, u.Tier, u.Cost, SimRules.BuildTicks(u.Cost, u.BuildTime, rules.Economy), u.Prerequisites, false));
        }

        var byId = new Dictionary<string, Raw>(StringComparer.Ordinal);
        foreach (Raw r in raw) byId[r.Id] = r;

        // Needs: per prerequisite name, the node of that id, else every building that provides the name.
        var needs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var unresolved = new List<string>();
        foreach (Raw r in raw)
        {
            var list = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string p in r.Prereqs)
            {
                if (byId.ContainsKey(p)) list.Add(p);
                else if (provides.TryGetValue(p, out List<string>? who)) foreach (string w in who) list.Add(w);
                else unresolved.Add(r.Id + ": " + p);
            }
            list.Remove(r.Id);   // a gate structure that provides its own tier name does not need itself
            needs[r.Id] = list.ToList();
        }

        // Columns: longest path from the roots; an edge that would close a cycle is ignored for the layout.
        var column = new Dictionary<string, int>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        int Depth(string id)
        {
            if (column.TryGetValue(id, out int c)) return c;
            if (!visiting.Add(id)) return -1;
            int d = 0;
            foreach (string n in needs[id]) d = Math.Max(d, Depth(n) + 1);
            visiting.Remove(id);
            column[id] = d;
            return d;
        }
        foreach (Raw r in raw) Depth(r.Id);

        var unlocks = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (Raw r in raw)
            foreach (string n in needs[r.Id]) Add(unlocks, n, r.Id);

        var nodes = raw
            .OrderBy(r => column[r.Id]).ThenBy(r => Order(r.Category)).ThenBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => new TechNode(r.Id, r.Category, r.Index, r.Tier, r.Cost, r.Ticks, needs[r.Id],
                unlocks.TryGetValue(r.Id, out List<string>? u) ? u.OrderBy(x => x, StringComparer.Ordinal).ToList() : new List<string>(), column[r.Id], r.Start))
            .ToList();
        return new TechTreeModel(faction, nodes, unresolved);
    }

    /// <summary>
    /// The status of every node (same order as <see cref="Nodes"/>) for one player. <paramref name="ownedBuildings"/> are the rules indices of
    /// the player's finished structures; availability comes from the player's HUD (the sidebar's own rule), so prerequisites, tier gates and
    /// mission gating are exactly what the sidebar shows. <paramref name="missionFilter"/> is <c>Simulation.UnlockFilter</c> (null outside missions):
    /// a node it rejects is <see cref="TechNodeStatus.MissionLocked"/>.
    /// </summary>
    public TechNodeStatus[] StatusFor(PlayerHud hud, IReadOnlyCollection<int> ownedBuildings, byte player,
        Func<byte, ProductionCategory, int, bool>? missionFilter)
    {
        var available = new HashSet<(ProductionCategory, int)>();
        foreach (BuildOption o in hud.Options) available.Add((o.Category, o.TypeIndex));
        foreach (int u in hud.BuildableUpgrades) available.Add((ProductionCategory.Upgrade, u));
        var result = new TechNodeStatus[Nodes.Count];
        for (int i = 0; i < Nodes.Count; i++)
        {
            TechNode n = Nodes[i];
            bool built = n.Category switch
            {
                ProductionCategory.Structure or ProductionCategory.Defence => ownedBuildings.Contains(n.TypeIndex),
                ProductionCategory.Upgrade => n.TypeIndex < 64 && (hud.Upgrades & (1UL << n.TypeIndex)) != 0,
                _ => false,
            };
            if (built) result[i] = TechNodeStatus.Built;
            else if (available.Contains((n.Category, n.TypeIndex))) result[i] = TechNodeStatus.Available;
            else if (n.Category != ProductionCategory.Upgrade && missionFilter is not null && !missionFilter(player, n.Category, n.TypeIndex)) result[i] = TechNodeStatus.MissionLocked;
            else result[i] = TechNodeStatus.Locked;
        }
        return result;
    }

    private static int Order(ProductionCategory c) => c switch
    {
        ProductionCategory.Structure => 0, ProductionCategory.Defence => 1, ProductionCategory.Infantry => 2,
        ProductionCategory.Vehicle => 3, ProductionCategory.Aircraft => 4, _ => 5,
    };

    private static void Add(Dictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out List<string>? list)) map[key] = list = new List<string>();
        if (!list.Contains(value)) list.Add(value);
    }

    private sealed record Raw(string Id, ProductionCategory Category, int Index, int Tier, int Cost, int Ticks, IReadOnlyList<string> Prereqs, bool Start);
}
