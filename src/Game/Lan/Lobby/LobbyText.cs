// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;
using Rts.Net.Discovery;

namespace Rts.Game.Lan.Lobby;

/// <summary>The words on the LAN browser and lobby screens (M9-05), kept pure so they can be tested.</summary>
public static class LobbyText
{
    /// <summary>"Rust Belt (yours)", "Generated Glass Coast Medium seed 12" ...</summary>
    public static string MapLabel(LobbyMap map)
    {
        if (map.Generated is { } g) return $"Generated {MapPicker.BiomeName(g.Biome)} {MapPicker.SizeName(g.Size)} seed {g.Seed}";
        string name = DisplayNamesOrPlain(MapPicker.LabelOf(map.File ?? "?"));
        return map.IsCustom ? name + " (yours)" : name;
    }

    private static string DisplayNamesOrPlain(string name) => name.Replace('_', ' ');

    /// <summary>One slot row: "1 Alice (host) Authority T1 ready", "2 Open", "3 AI Veteran Directorate T2", "4 Closed".</summary>
    public static string SlotLine(LobbySlotState s, int index, IReadOnlyList<string> tierNames, bool isLocal)
    {
        string head = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " ";
        string faction = SkirmishSetupScreen.FactionName(s.Slot.Faction);
        string team = $"T{s.Slot.Team + 1}";
        switch (s.Slot.Kind)
        {
            case SlotKind.Closed: return head + "Closed";
            case SlotKind.Human when s.IsOpen: return head + "Open";
            case SlotKind.Human:
                string who = s.Name + (s.Peer == 0 ? " (host)" : "") + (isLocal ? " (you)" : "");
                string state = s.Peer == 0 ? "" : s.Ready ? " ready" : " not ready";
                return $"{head}{who} {faction} {team}{state}";
            default:
                string tier = tierNames[Math.Clamp(s.Slot.AiTier, 1, tierNames.Count) - 1];
                return $"{head}AI {(s.Slot.Commander ? SetupText.CommanderLabel + " " : "")}{tier} {faction} {team}";
        }
    }

    /// <summary>Title of the notice shown when this machine could not start the match (the lobby is left).</summary>
    public const string StartFailedTitle = "Could not start the match";

    /// <summary>The options line of a slot (handicap, credits, start units), as in skirmish setup.</summary>
    public static string OptionsLine(LobbySlotState s, int index) => SkirmishSetupScreen.OptionsLabel(s.Slot, index);

    /// <summary>"Delay: 3 ticks".</summary>
    public static string DelayLabel(int ticks) => $"Command delay: {ticks} ticks";

    /// <summary>"No spectators" or "Watching: Sam, Alex".</summary>
    public static string SpectatorLine(IReadOnlyList<LobbySpectator> spectators) =>
        spectators.Count == 0 ? "No spectators" : "Watching: " + string.Join(", ", spectators.Select(s => s.Name));

    /// <summary>One row of the join list: "Sam's game  2/6  open  ", "Sam's game  full", "old build" for a host with another protocol.</summary>
    public static string HostLine(DiscoveredHost host)
    {
        DiscoveryInfo i = host.Info;
        string name = i.LobbyName.Length > 0 ? i.LobbyName : host.GameEndPoint.ToString();
        if (!i.Compatible) return $"{name}  (different game version)";
        string state = i.Started ? "started" : i.Open ? "open" : "full";
        return $"{name}  {i.Players}/{i.MaxPlayers}  {state}  {host.GameEndPoint.Address}";
    }

    /// <summary>True when a host in the list can be joined now.</summary>
    public static bool CanJoin(DiscoveredHost host) => host.Info.Compatible && host.Info.Open && !host.Info.Started;

    /// <summary>The tier names from the difficulty file, "Tier N" for any it lacks.</summary>
    public static string[] TierNames(string rulesDir)
    {
        var names = new string[DifficultyLoader.TierIds.Count];
        for (int i = 0; i < names.Length; i++) names[i] = $"Tier {i + 1}";
        try
        {
            foreach (var profile in DifficultyLoader.Load(DifficultyLoader.PathBesideRules(rulesDir)))
                if (profile.Tier >= 1 && profile.Tier <= names.Length && profile.Id.Length > 0)
                    names[profile.Tier - 1] = char.ToUpperInvariant(profile.Id[0]) + profile.Id[1..];
        }
        catch (RulesException)
        {
            // keep "Tier N"
        }

        return names;
    }
}
