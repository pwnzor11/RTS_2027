#!/usr/bin/env python3
"""Tech tree per faction: techtree_<faction>.json and techtree_<faction>.dot, plus techtree_report.md.

For every unit, building and upgrade of a faction:
  producer        building(s) the item comes out of (structure/defence lanes: the construction root; units: buildings whose `produces`
                  matches; ships need a water_building producer; upgrades: any `researches` building)
  requires        prerequisites after tier sugar, each resolved to the buildings / upgrades that satisfy it
  chain           the cheapest set of buildings that must exist before the item can be queued (transitive)
  min_cost        credits for that chain + the item itself (start-kit buildings and their free units cost 0)
  min_ticks       build ticks of the chain's structures one after another in the Structure lane + the item's own build ticks
                  (the fastest possible clock with unlimited money and full power; real games are slower)
  obtainable      False = dead end (nothing in the faction can ever satisfy it)
Cycles in the prerequisite graph are reported.
"""
import json
import os

from resolve_rules import FACTIONS, load_rules

HERE = os.path.dirname(os.path.abspath(__file__))
R = load_rules()
U, B, UP = R["units"], R["buildings"], R["upgrades"]
TICK = R["meta"]["tick_rate"]


def run(f):
    kit = R["start_kits"][f]
    own_b = {i: b for i, b in B.items() if b["faction"] == f}
    own_u = {i: u for i, u in U.items() if u["faction"] == f}
    own_up = {i: u for i, u in UP.items() if u["faction"] == f}
    start_b = set()
    if kit.get("conyard"):
        start_b.add(kit["conyard"])
    for x in kit.get("units", []):
        for key in ("deploys_into", "unpacks_into"):
            if U[x].get(key):
                start_b.add(U[x][key])
    INF = float("inf")
    # cheapest building chain per building: fixed-point (Bellman-Ford style) over "set of buildings needed"
    chain = {i: (frozenset(), ) for i in start_b}   # building -> (frozenset of buildings incl. itself that must be built, )
    for i in start_b:
        chain[i] = frozenset()

    def cost_of(s):
        return sum(B[x]["cost"] for x in s)

    def ticks_of(s):
        return sum(B[x]["build_ticks"] for x in s)

    def satisfiers(p):
        if p in UP:
            return None
        return [x for x in own_b if x == p or p in own_b[x]["provides"]]

    def need_for(prereqs, up_chain):
        """Cheapest building set meeting every prerequisite, or None."""
        total = frozenset()
        for p in prereqs:
            if p in UP:
                if p not in up_chain:
                    return None
                total |= up_chain[p]
                continue
            best = None
            for x in satisfiers(p):
                if x in chain:
                    s = chain[x] | ({x} if x not in start_b else set())
                    if best is None or cost_of(s) < cost_of(best):
                        best = frozenset(s)
            if best is None:
                return None
            total |= best
        return total

    changed = True
    while changed:
        changed = False
        for i, b in own_b.items():
            if i in start_b:
                continue
            cands = []
            if b["buildable"]:
                n = need_for(b["prerequisites"], {})
                if n is not None:
                    cands.append(n)
            for j, src in own_b.items():   # morph result
                if src["morphs_into"] == i and j in chain:
                    cands.append(chain[j] | ({j} if j not in start_b else set()))
            for n in cands:
                if i not in chain or cost_of(n) < cost_of(chain[i]):
                    chain[i] = frozenset(n)
                    changed = True

    def producers(u):
        cat = u["production_category"]
        ps = [x for x, b in own_b.items() if b["produces"] == cat]
        if u.get("locomotion") == "naval":
            ps = [x for x in ps if own_b[x]["water_building"]]
        return ps

    nodes, edges = {}, []
    research = [x for x, b in own_b.items() if b["researches"]]
    up_chain = {}
    for _ in range(3):
        for i, u in own_up.items():
            n = need_for(u["prerequisites"], up_chain)
            rb = [x for x in research if x in chain]
            if n is not None and rb:
                best = min((chain[x] | ({x} if x not in start_b else set()) for x in rb), key=lambda s: cost_of(n | s))
                up_chain[i] = frozenset(n | best)

    for i, b in own_b.items():
        ok = i in chain
        c = chain.get(i, frozenset())
        nodes[i] = dict(id=i, kind="building", category=b["category"], tier=b["tier"], cost=b["cost"], build_ticks=b["build_ticks"],
                        buildable=b["buildable"], start=i in start_b, obtainable=ok, producer=[kit.get("conyard") or "(start unit: " + ", ".join(sorted(start_b)) + ")"] if b["buildable"] else [],
                        requires=b["prerequisites"], chain=sorted(c),
                        min_cost=(cost_of(c) + (0 if i in start_b else b["cost"])) if ok else None,
                        min_ticks=(ticks_of(c) + (0 if i in start_b else b["build_ticks"])) if ok else None,
                        provides=b["provides"], produces=b["produces"], researches=b["researches"])
        for p in b["prerequisites"]:
            for x in satisfiers(p) or []:
                edges.append((x, i, "prereq" if p == x else f"prereq ({p})"))
        if b["morphs_into"]:
            edges.append((i, b["morphs_into"], "morphs into"))
        if b["free_unit"]:
            edges.append((i, b["free_unit"], "free unit"))
        if b["packs_into"]:
            edges.append((i, b["packs_into"], "packs into"))
    for i, u in own_u.items():
        ps = [x for x in producers(u) if x in chain]
        n = need_for(u["prerequisites"], up_chain)
        in_kit = i in kit.get("units", [])
        free_from = [x for x, b in own_b.items() if b["free_unit"] == i and x in chain]
        ok = n is not None and bool(ps)
        c = None
        if ok:
            c = min((n | chain[x] | ({x} if x not in start_b else set()) for x in ps), key=cost_of)
        nodes[i] = dict(id=i, kind=u["kind"], category=u["production_category"], tier=u["tier"], cost=u["cost"], build_ticks=u["build_ticks"],
                        start=in_kit, free_with=free_from, obtainable=ok or in_kit or bool(free_from), producible=ok,
                        producer=sorted(ps), requires=u["prerequisites"], chain=sorted(c) if c is not None else [],
                        min_cost=cost_of(c) + u["cost"] if ok else (0 if in_kit else None),
                        min_ticks=ticks_of(c) + u["build_ticks"] if ok else (0 if in_kit else None),
                        supply=u["supply_resolved"])
        for p in u["prerequisites"]:
            for x in satisfiers(p) or []:
                edges.append((x, i, "prereq" if p == x else f"prereq ({p})"))
        for x in producers(u):
            if x not in u["prerequisites"]:
                edges.append((x, i, "built at"))
        for key in ("deploys_into", "unpacks_into"):
            if u.get(key):
                edges.append((i, u[key], key.replace("_", " ")))
    for i, u in own_up.items():
        ok = i in up_chain
        c = up_chain.get(i, frozenset())
        nodes[i] = dict(id=i, kind="upgrade", category="upgrade", tier=u["tier"], cost=u["cost"], build_ticks=u["build_ticks"], obtainable=ok,
                        producer=sorted(x for x in research if x in chain), requires=u["prerequisites"], chain=sorted(c),
                        min_cost=cost_of(c) + u["cost"] if ok else None, min_ticks=ticks_of(c) + u["build_ticks"] if ok else None)
        for p in u["prerequisites"]:
            for x in (satisfiers(p) or [p]):
                edges.append((x, i, "prereq" if p == x else f"prereq ({p})"))
    for n in nodes.values():
        if n["min_ticks"] is not None:
            n["min_seconds"] = round(n["min_ticks"] / TICK, 1)

    # cycles in the prerequisite graph (DFS over prereq edges only)
    adj = {}
    for a, b_, lab in edges:
        if lab.startswith("prereq"):
            adj.setdefault(a, set()).add(b_)
    cycles, state = [], {}

    def dfs(x, path):
        state[x] = 1
        for y in adj.get(x, ()):
            if state.get(y) == 1:
                cycles.append(path[path.index(y):] + [y] if y in path else [x, y])
            elif y not in state:
                dfs(y, path + [y])
        state[x] = 2

    for x in list(adj):
        if x not in state:
            dfs(x, [x])
    dead = sorted(i for i, n in nodes.items() if not n["obtainable"])
    # leaf buildings: built for nothing (no produces/provides/research/weapon/support role) -- informational
    needed = {a for a, _, lab in edges}
    tree = dict(faction=f, start_buildings=sorted(start_b), start_units=kit.get("units", []), tier2=kit.get("tier2"), tier3=kit.get("tier3"),
                nodes=nodes, edges=[dict(src=a, dst=b_, label=lab) for a, b_, lab in sorted(set(edges))], cycles=cycles, dead_ends=dead)
    json.dump(tree, open(os.path.join(HERE, f"techtree_{f}.json"), "w"), indent=1, sort_keys=True)

    shape = {"building": "box", "vehicle": "ellipse", "infantry": "ellipse", "upgrade": "hexagon"}
    fill = {1: "#e8f0e8", 2: "#fff3cc", 3: "#f8d8d0"}
    lines = [f'digraph "{f}" {{', "  rankdir=LR; node [fontname=Helvetica, fontsize=10, style=filled]; edge [fontname=Helvetica, fontsize=8];"]
    for i, n in sorted(nodes.items()):
        label = f"{i}\\n{n['cost']} cr / {n['build_ticks'] / TICK:.0f} s"
        if n.get("min_cost") is not None:
            label += f"\\nreach: {n['min_cost']} cr, {n.get('min_seconds', 0):.0f} s"
        extra = ", peripheries=2" if n.get("start") else ""
        extra += ", color=red, fontcolor=red" if not n["obtainable"] else ""
        lines.append(f'  "{i}" [label="{label}", shape={shape[n["kind"]]}, fillcolor="{fill.get(n["tier"], "#ffffff")}"{extra}];')
    for a, b_, lab in sorted(set(edges)):
        style = "" if lab == "prereq" else (' [style=dashed, label="%s"]' % lab)
        lines.append(f'  "{a}" -> "{b_}"{style};')
    lines.append("}")
    open(os.path.join(HERE, f"techtree_{f}.dot"), "w").write("\n".join(lines) + "\n")
    return tree


if __name__ == "__main__":
    with open(os.path.join(HERE, "techtree_report.md"), "w") as rep:
        rep.write("# Tech tree report (generated by techtree.py)\n\nmin_cost / min time = cheapest prerequisite chain + the item; time assumes one Structure lane, unlimited money, full power.\n\n")
        for f in FACTIONS:
            t = run(f)
            n = t["nodes"]
            rep.write(f"## {f}\n- start: buildings {t['start_buildings']}, units {t['start_units']}; tier gates {t['tier2']} / {t['tier3']}\n")
            rep.write(f"- cycles: {t['cycles'] or 'none'}\n- dead ends (never obtainable): {t['dead_ends'] or 'none'}\n")
            notprod = sorted(i for i, x in n.items() if x["kind"] in ("vehicle", "infantry") and not x["producible"])
            rep.write(f"- units that cannot be produced (start kit / free only): {notprod or 'none'}\n\n")
            rep.write("| id | kind | tier | cost | build s | producer | requires | reach cost | reach s |\n|---|---|---|---|---|---|---|---|---|\n")
            for i, x in sorted(n.items(), key=lambda kv: (kv[1]["kind"] != "building", kv[1]["kind"] == "upgrade", kv[1]["min_cost"] if kv[1]["min_cost"] is not None else 1e9)):
                rep.write(f"| {i} | {x['kind']} | {x['tier']} | {x['cost']} | {x['build_ticks'] / TICK:.0f} | {', '.join(x['producer'])} | {', '.join(x['requires'])} | {x['min_cost']} | {x.get('min_seconds')} |\n")
            rep.write("\n")
            print(f, "nodes", len(n), "dead", t["dead_ends"], "cycles", t["cycles"], "not producible", notprod)
