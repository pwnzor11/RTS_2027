// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Numerics;
using System.Text;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Llm;

/// <summary>
/// Builds the compact plain-text battlefield report the LLM commander reads (design 09 §5). The ONLY game information used
/// comes from the slot's <see cref="PlayerView"/> (own actors, visible actors, known enemy buildings, telegraphs, HUD, rules
/// tables) and the <see cref="SummaryHistory"/> fed from the same view, so the text can never mention an enemy actor the player
/// has not seen. Integer math only; ordering is by type index then id, so equal input gives byte-identical output.
/// </summary>
public static class BattlefieldSummary
{
    /// <summary>Hard cap on the summary length in characters (about 1 500 tokens).</summary>
    public const int MaxChars = 5500;

    /// <summary>Longest quoted player chat in characters.</summary>
    public const int MaxChatChars = 300;

    private const int TicksPerSecond = 20;
    private const int DamagedBelowPct = 75;
    private const int MaxDamagedListed = 6;
    private const int TenderDangerCells = 8;

    private enum Role { Harvester, Infantry, Vehicle, Air, Support }
    private static readonly string[] RoleNames = { "harvesters", "infantry", "vehicles", "air", "support" };
    private static readonly string[] Postures = { "turtle", "balanced", "aggressive", "all-in" };
    private static readonly string[] Focuses = { "economy", "tech", "army", "air", "superweapon" };
    private static readonly string[] Mixes = { "auto", "infantry", "vehicles", "anti-air", "anti-structure" };

    /// <summary>Builds the summary. <paramref name="history"/> should have had <see cref="SummaryHistory.Observe"/> called with the
    /// same view. <paramref name="playerChat"/> is what the human typed to this commander since the last call.</summary>
    public static string Build(PlayerView view, SummaryHistory history, string? playerChat = null)
    {
        SimRules rules = view.Rules;
        var own = new List<ViewActor>();
        var visible = new List<ViewActor>();
        var known = new List<ViewActor>();
        view.CopyOwn(own);
        view.CopyVisible(visible);
        view.CopyKnownEnemyBuildings(known);
        own.Sort(Compare);
        visible.Sort(Compare);
        known.Sort(Compare);

        var sections = new List<string>
        {
            Header(view, rules),
            Army(view, rules, own),
            Base(rules, own),
        };
        if (view.Faction == Faction.Expedition) sections.Add(Expedition(view, visible));
        sections.Add(Others(view, rules, visible, known, history));
        string naval = NavalThreat(view, rules, visible);
        if (naval.Length > 0) sections.Add(naval);
        sections.Add(Events(view, rules, history));
        if (!string.IsNullOrWhiteSpace(playerChat))
            sections.Add("PLAYER CHAT (to you): \"" + Clean(playerChat!, MaxChatChars) + "\"");
        return Fit(sections);
    }

    // ---- sections ------------------------------------------------------------------------------

    private static string Header(PlayerView view, SimRules rules)
    {
        PlayerHud hud = view.Hud;
        var sb = new StringBuilder();
        sb.Append("TIME ").Append(Clock(view.Tick)).Append(" | you are player ").Append(view.Player).Append(" (")
          .Append(view.Faction).Append("), tier ").Append(view.Tier).Append(", cash ").Append(view.Cash)
          .Append(", power ").Append(hud.PowerOut).Append('/').Append(hud.PowerUse)
          .Append(hud.PowerOut < hud.PowerUse ? " LOW" : "").Append(", map ").Append(view.MapWidth).Append('x').Append(view.MapHeight)
          .Append(", ").Append(view.PlayerCount).Append(" players\n");
        sb.Append("Upgrades: ");
        int n = 0;
        for (int i = 0; i < rules.Upgrades.Count; i++)
            if (view.HasUpgrade(i)) { sb.Append(n++ > 0 ? ", " : "").Append(rules.Upgrades[i].Id); }
        if (n == 0) sb.Append("none");
        foreach (SuperweaponView s in hud.Superweapons)
        {
            sb.Append("\nYour superweapon ").Append(s.Superweapon).Append(": ")
              .Append(s.Ready ? "READY" : (s.Total > 0 ? (int)((long)s.Charge * 100 / s.Total) : 0) + "% charged")
              .Append(s.Paused ? " (paused, low power)" : "");
        }
        return sb.ToString();
    }

    private static string Army(PlayerView view, SimRules rules, List<ViewActor> own)
    {
        int refineries = 0;
        foreach (ViewActor a in own)
            if (a.Kind == ActorKind.Building && rules.Buildings[a.TypeIndex].Refinery) refineries++;
        var sb = new StringBuilder();
        sb.Append("YOUR ECONOMY: ").Append(CountRole(rules, own, Role.Harvester)).Append(" harvesters, ").Append(refineries).Append(" refineries\n");
        sb.Append("YOUR ARMY: ").Append(RoleLines(rules, own, includeHarvesters: false));
        return sb.ToString().TrimEnd('\n');
    }

    private static string Base(SimRules rules, List<ViewActor> own)
    {
        var sb = new StringBuilder("YOUR BASE: ");
        var counts = new SortedDictionary<int, int>();
        var damaged = new List<ViewActor>();
        foreach (ViewActor a in own)
        {
            if (a.Kind != ActorKind.Building) continue;
            counts[a.TypeIndex] = counts.GetValueOrDefault(a.TypeIndex) + 1;
            if (a.MaxHealth > 0 && (long)a.Health * 100 < (long)a.MaxHealth * DamagedBelowPct) damaged.Add(a);
        }
        if (counts.Count == 0) sb.Append("no structures");
        else AppendCounts(sb, counts, i => rules.Buildings[i].Id);
        if (counts.Count > 0)
        {
            var c = Centroid(own.Where(a => a.Kind == ActorKind.Building));
            sb.Append(" (around ").Append(c.X).Append(',').Append(c.Y).Append(')');
        }
        if (damaged.Count > 0)
        {
            sb.Append("\nDamaged: ");
            for (int i = 0; i < damaged.Count && i < MaxDamagedListed; i++)
            {
                ViewActor d = damaged[i];
                sb.Append(i > 0 ? ", " : "").Append(rules.Buildings[d.TypeIndex].Id).Append(' ').Append((int)((long)d.Health * 100 / d.MaxHealth)).Append('%');
            }
            if (damaged.Count > MaxDamagedListed) sb.Append(" +").Append(damaged.Count - MaxDamagedListed).Append(" more");
        }
        return sb.ToString();
    }

    /// <summary>M7-10: the Expedition section (only for an Expedition player): the home shipment timer, the next payload, the beachhead
    /// size (set cells of the own zone mask) and whether an own tender has a visible enemy within <see cref="TenderDangerCells"/>.
    /// Own state plus visible actors only.</summary>
    private static string Expedition(PlayerView view, List<ViewActor> visible)
    {
        int eta = view.ShipmentEtaTicks;
        int cells = 0;
        foreach (ulong word in view.BeachheadMask) cells += BitOperations.PopCount(word);
        var tenders = new List<TenderView>();
        view.CopyOwnTenders(tenders);
        bool danger = false;
        foreach (TenderView t in tenders)
        {
            foreach (ViewActor v in visible)
            {
                if (view.RelationOf(v.Owner) != Relation.Enemy) continue;
                if (Math.Max(Math.Abs(v.Cell.X - t.Cell.X), Math.Abs(v.Cell.Y - t.Cell.Y)) <= TenderDangerCells) { danger = true; break; }
            }
            if (danger) break;
        }
        return "EXPEDITION:\n"
            + "shipment_eta: " + (eta < 0 ? "none" : (eta / TicksPerSecond) + "s") + "\n"
            + "shipment_payload: " + view.NextShipmentPayload + "cr\n"
            + "beachhead_cells: " + cells + "\n"
            + "convoy_in_danger: " + (danger ? "true" : "false");
    }

    /// <summary>M7-10: visible enemy ships (surface, and dived submarines the viewer's team detects) as one line, or "" when there are none.
    /// Strength is the sum of the ships' credit costs, rounded to 100 so it reads as a rough figure.</summary>
    private static string NavalThreat(PlayerView view, SimRules rules, List<ViewActor> visible)
    {
        int surface = 0, dived = 0, strength = 0;
        foreach (ViewActor a in visible)
        {
            if (a.Kind != ActorKind.Vehicle || view.RelationOf(a.Owner) != Relation.Enemy) continue;
            VehicleType v = rules.Vehicles[a.TypeIndex];
            if (v.Locomotion != Locomotion.Naval) continue;
            if (a.Submerged) dived++; else surface++;
            strength += v.Cost;
        }
        int count = surface + dived;
        if (count == 0) return "";
        int rough = (strength + 50) / 100 * 100;
        return "naval_threat: " + count + " (" + surface + " surface, " + dived + " dived), strength ~" + rough + "cr";
    }

    private static string Others(PlayerView view, SimRules rules, List<ViewActor> visible, List<ViewActor> known, SummaryHistory history)
    {
        var lines = new List<string>();
        for (int slot = 0; slot < view.PlayerCount; slot++)
        {
            byte p = (byte)slot;
            if (p == view.Player) continue;
            Relation rel = view.RelationOf(p);
            var buildings = known.Where(a => a.Owner == p).ToList();
            var liveBuildings = visible.Where(a => a.Owner == p && a.Kind == ActorKind.Building).ToList();
            var mobiles = visible.Where(a => a.Owner == p && a.IsMobile).ToList();
            var allBuildings = rel == Relation.Enemy ? buildings : liveBuildings;
            string faction = FactionOf(rules, allBuildings.Concat(mobiles));
            var sb = new StringBuilder();
            sb.Append("PLAYER ").Append(slot).Append(' ').Append(rel.ToString().ToLowerInvariant()).Append(", faction ").Append(faction).Append(": ");
            if (allBuildings.Count == 0 && mobiles.Count == 0) { sb.Append("no contact"); lines.Add(sb.ToString()); continue; }
            if (allBuildings.Count > 0)
            {
                sb.Append("known buildings ");
                BuildingList(sb, rules, allBuildings, view.Tick, history);
                var c = Centroid(allBuildings);
                sb.Append(" (around ").Append(c.X).Append(',').Append(c.Y).Append(')');
            }
            if (mobiles.Count > 0)
            {
                if (allBuildings.Count > 0) sb.Append("; ");
                var c = Centroid(mobiles);
                sb.Append("army in view now ").Append(RoleLines(rules, mobiles, includeHarvesters: true).TrimEnd()).Append(" (around ").Append(c.X).Append(',').Append(c.Y).Append(')');
            }
            lines.Add(sb.ToString());
        }
        // enemy telegraphs the view exposes (surfacing units / tunnel entrances)
        var tel = new List<TelegraphView>();
        view.CopyTelegraphs(tel);
        int enemyTel = 0;
        foreach (TelegraphView t in tel)
            if (view.RelationOf(t.Owner) == Relation.Enemy) enemyTel++;
        if (enemyTel > 0) lines.Add("Enemy surfacing telegraphs seen: " + enemyTel);
        return lines.Count == 0 ? "OTHER PLAYERS: none" : "OTHER PLAYERS (only what you can see or remember):\n" + string.Join("\n", lines);
    }

    private static string Events(PlayerView view, SimRules rules, SummaryHistory h)
    {
        var sb = new StringBuilder("RECENT EVENTS:");
        bool any = false;
        if (h.Losses.Count > 0)
        {
            sb.Append("\n- You lost: ");
            for (int i = 0; i < h.Losses.Count; i++)
            {
                (ActorKind kind, int type, int count) = h.Losses[i];
                sb.Append(i > 0 ? ", " : "").Append(count).Append('x').Append(TypeName(rules, kind, type));
            }
            any = true;
        }
        if (h.EnemyBuildingsDestroyed.Count > 0)
        {
            sb.Append("\n- Enemy structures you saw destroyed: ");
            for (int i = 0; i < h.EnemyBuildingsDestroyed.Count; i++)
                sb.Append(i > 0 ? ", " : "").Append(h.EnemyBuildingsDestroyed[i].Count).Append('x').Append(rules.Buildings[h.EnemyBuildingsDestroyed[i].Type].Id);
            any = true;
        }
        if (h.Attacked.Count > 0)
        {
            sb.Append("\n- Your buildings under attack: ");
            for (int i = 0; i < h.Attacked.Count; i++)
                sb.Append(i > 0 ? ", " : "").Append(h.Attacked[i].Count).Append('x').Append(rules.Buildings[h.Attacked[i].Type].Id);
            any = true;
        }
        var known = new List<ViewActor>();
        view.CopyKnownEnemyBuildings(known);
        known.Sort(Compare);
        var sw = new List<string>();
        foreach (ViewActor b in known)
            if (rules.Buildings[b.TypeIndex].Superweapon.Length > 0)
                sw.Add(rules.Buildings[b.TypeIndex].Id + " of player " + b.Owner + (b.Ghost ? " (last seen " + AgeText(view.Tick, h.LastSeenTick(b.Owner, b.TypeIndex, b.Cell)) + " ago)" : " (in view now)"));
        if (sw.Count > 0) { sb.Append("\n- ENEMY SUPERWEAPON structure known: ").Append(string.Join(", ", sw)); any = true; }
        if (!any) sb.Append(" nothing notable");
        if (h.Directives.Count > 0)
        {
            sb.Append("\nYOUR LAST DIRECTIVES:");
            foreach ((DirectiveDraft d, int tick) in h.Directives)
            {
                sb.Append("\n- ").Append(Clock(tick)).Append(" posture ").Append(Name(Postures, d.Posture)).Append(", focus ").Append(Name(Focuses, d.Focus))
                  .Append(", target ").Append(d.TargetPlayer == 255 ? "auto" : d.TargetPlayer.ToString()).Append(", mix ").Append(Name(Mixes, d.Mix))
                  .Append(", expand ").Append(d.Expand ? "yes" : "no");
                if (d.FocusX >= 0 && d.FocusY >= 0) sb.Append(", focus cell ").Append(d.FocusX).Append(',').Append(d.FocusY);
            }
        }
        return sb.ToString();
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static string Fit(List<string> sections)
    {
        var sb = new StringBuilder();
        foreach (string s in sections)
        {
            int room = MaxChars - sb.Length - (sb.Length > 0 ? 1 : 0);
            if (room <= 0) break;
            string piece = s;
            if (piece.Length > room)
            {
                // cut at the last line break that fits, else hard cut
                int cut = piece.LastIndexOf('\n', room - 5 > 0 ? room - 5 : 0);
                piece = (cut > 0 ? piece[..cut] : piece[..(room > 4 ? room - 4 : 0)]) + "\n...";
                if (piece.Length > room) piece = piece[..room];
            }
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(piece);
        }
        return sb.ToString();
    }

    private static int Compare(ViewActor a, ViewActor b)
    {
        int c = a.Kind.CompareTo(b.Kind);
        if (c != 0) return c;
        c = a.TypeIndex.CompareTo(b.TypeIndex);
        if (c != 0) return c;
        c = a.Owner.CompareTo(b.Owner);
        if (c != 0) return c;
        c = a.Id.Index.CompareTo(b.Id.Index);
        if (c != 0) return c;
        c = a.Cell.X.CompareTo(b.Cell.X);
        return c != 0 ? c : a.Cell.Y.CompareTo(b.Cell.Y);
    }

    private static Role RoleOf(SimRules rules, ViewActor a)
    {
        if (a.Kind == ActorKind.Infantry)
        {
            InfantryType t = rules.Infantry[a.TypeIndex];
            return t.Engineer || t.PrimaryWeapon < 0 ? Role.Support : Role.Infantry;
        }
        VehicleType v = rules.Vehicles[a.TypeIndex];
        if (v.HarvestCapacity > 0) return Role.Harvester;
        if (v.Locomotion == Locomotion.Air) return Role.Air;
        return v.PrimaryWeapon < 0 ? Role.Support : Role.Vehicle;
    }

    private static int CostOf(SimRules rules, ViewActor a) =>
        a.Kind == ActorKind.Vehicle ? rules.Vehicles[a.TypeIndex].Cost : a.Kind == ActorKind.Infantry ? rules.Infantry[a.TypeIndex].Cost : 0;

    private static string TypeName(SimRules rules, ActorKind kind, int type) => kind switch
    {
        ActorKind.Vehicle => rules.Vehicles[type].Id,
        ActorKind.Infantry => rules.Infantry[type].Id,
        _ => rules.Buildings[type].Id,
    };

    private static int CountRole(SimRules rules, List<ViewActor> list, Role role)
    {
        int n = 0;
        foreach (ViewActor a in list) if (a.IsMobile && RoleOf(rules, a) == role) n++;
        return n;
    }

    /// <summary>"infantry 5 (500cr: auth_rifle x5); vehicles 2 (...)" for the mobile actors in the list, in role then type order.</summary>
    private static string RoleLines(SimRules rules, List<ViewActor> list, bool includeHarvesters)
    {
        var parts = new List<string>();
        for (int r = 0; r < RoleNames.Length; r++)
        {
            Role role = (Role)r;
            if (role == Role.Harvester && !includeHarvesters) continue;
            var byType = new SortedDictionary<(int, int), int>();
            int count = 0, value = 0;
            foreach (ViewActor a in list)
            {
                if (!a.IsMobile || RoleOf(rules, a) != role) continue;
                count++; value += CostOf(rules, a);
                (int, int) k = ((int)a.Kind, a.TypeIndex);
                byType[k] = byType.GetValueOrDefault(k) + 1;
            }
            if (count == 0) continue;
            var sb = new StringBuilder();
            sb.Append(RoleNames[r]).Append(' ').Append(count).Append(" (").Append(value).Append("cr: ");
            bool first = true;
            foreach (KeyValuePair<(int, int), int> p in byType)
            {
                sb.Append(first ? "" : ", ").Append(TypeName(rules, (ActorKind)p.Key.Item1, p.Key.Item2)).Append(" x").Append(p.Value);
                first = false;
            }
            sb.Append(')');
            parts.Add(sb.ToString());
        }
        return parts.Count == 0 ? "none" : string.Join("; ", parts);
    }

    private static void AppendCounts(StringBuilder sb, SortedDictionary<int, int> counts, Func<int, string> name)
    {
        bool first = true;
        foreach (KeyValuePair<int, int> p in counts)
        {
            sb.Append(first ? "" : ", ").Append(name(p.Key)).Append(" x").Append(p.Value);
            first = false;
        }
    }

    private static void BuildingList(StringBuilder sb, SimRules rules, List<ViewActor> buildings, int tick, SummaryHistory history)
    {
        // group by type; a group shows "in view" if any is live, else the age of the freshest sighting
        var groups = new SortedDictionary<int, (int Count, int Live, int Freshest)>();
        foreach (ViewActor b in buildings)
        {
            (int Count, int Live, int Freshest) g = groups.TryGetValue(b.TypeIndex, out var cur) ? cur : (0, 0, -1);
            g.Count++;
            if (!b.Ghost) g.Live++;
            else
            {
                int seen = history.LastSeenTick(b.Owner, b.TypeIndex, b.Cell);
                if (seen > g.Freshest) g.Freshest = seen;
            }
            groups[b.TypeIndex] = g;
        }
        bool first = true;
        foreach (KeyValuePair<int, (int Count, int Live, int Freshest)> p in groups)
        {
            sb.Append(first ? "" : ", ").Append(rules.Buildings[p.Key].Id).Append(" x").Append(p.Value.Count);
            if (p.Value.Live == p.Value.Count) sb.Append(" (in view)");
            else if (p.Value.Live > 0) sb.Append(" (").Append(p.Value.Live).Append(" in view)");
            else sb.Append(" (last seen ").Append(AgeText(tick, p.Value.Freshest)).Append(" ago)");
            first = false;
        }
    }

    private static string AgeText(int tick, int seenTick)
    {
        if (seenTick < 0) return "a while";
        int s = (tick - seenTick) / TicksPerSecond;
        return s < 0 ? "0s" : s >= 60 ? (s / 60) + "m" + (s % 60).ToString("00") + "s" : s + "s";
    }

    private static string FactionOf(SimRules rules, IEnumerable<ViewActor> actors)
    {
        foreach (ViewActor a in actors)
        {
            Faction f = a.Kind switch
            {
                ActorKind.Vehicle => rules.Vehicles[a.TypeIndex].Faction,
                ActorKind.Infantry => rules.Infantry[a.TypeIndex].Faction,
                _ => rules.Buildings[a.TypeIndex].Faction,
            };
            if (f != Faction.Neutral) return f.ToString();
        }
        return "unknown";
    }

    private static (int X, int Y) Centroid(IEnumerable<ViewActor> actors)
    {
        long x = 0, y = 0; int n = 0;
        foreach (ViewActor a in actors) { x += a.Cell.X; y += a.Cell.Y; n++; }
        return n == 0 ? (0, 0) : ((int)(x / n), (int)(y / n));
    }

    private static string Clock(int tick)
    {
        int s = tick / TicksPerSecond;
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }

    private static string Name(string[] names, int i) => i >= 0 && i < names.Length ? names[i] : "auto";

    private static string Clean(string text, int max)
    {
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            if (sb.Length >= max) break;
            sb.Append(c == '"' ? '\'' : char.IsControl(c) ? ' ' : c);
        }
        return sb.ToString().Trim();
    }
}
