# Session 48 — shared brief for campaign mission packages (C-12 wave 1)

Read after docs/COMMON.md and docs/process/s48_wave_rules.md. Your package brief names ONE mission id; build it end to end, like the
session-47 pilot auth_07 did.

## Read only these
- docs/design/22_campaign_expansion.md: §2 (structure rules, all of it), your faction's table and your mission's paragraph in §3,
  §4.1-4.2 (timeline and characters for your faction), §6 (the auth_07 pilot spec: the level of detail your mission needs).
- The pilot package as your template (`git show --stat b5a8ea4f` lists it): data/missions/auth_07/{mission.toml,mission.lua,briefing.md,
  locale.toml}, its wiring in data/campaigns/authority.toml, tests/Scripting.Tests/S47/S47PilotTests.cs, and its rows in
  tests/Scripting.Tests/M8_15/CampaignWalks.cs, MissionParTests.cs, S19/CampaignAllowListTests.cs, S22/MissionScriptKeysTests.cs,
  tests/Game.Tests/Campaign/CampaignFilesTests.cs and MissionLinksTests.cs.
- The built mission just before yours in play order (its starts_with and unlocks_on_win), and the one after it (its starts_with).
- docs/lore/ for your leader only (leaders.md section, plus the faction file). Do not read other factions' missions.

## Owner rules for every mission (D-337, D-341, D-342)
- Ids, play order, map, mission type, unlocks: exactly as the §3 table says. The campaign list (data/campaigns/<faction>.toml) sets play order:
  insert your mission in its play position, fix `requires` on yours and on the next built mission. Act stills: §2 "Stills".
- Tech slot-in: your `starts_with` is a subset of what the previous mission ends with; your `unlocks_on_win` is the table's unlock list.
- D-163 template: one leader on the radio, primary objectives plus one hidden bonus, at least one visible timer or clear clock; enemy pressure
  by unit count and AI tier only (D-061); tier tables for 1-5 like auth_07.
- No-base (commando) missions must lose explicitly: `Trigger.OnAllDestroyed(0, ...)`. Hidden enemy reserve buildings are not needed:
  use `Player.KeepAlive` (S47 C-06) for enemy players that must not be defeated early.
- Leader secrets (D-341 Q9): hint them on screen only; never state an Archive secret in a briefing or radio line.
- Wrenfield is Brigadier Hester Wrenfield, "she/her" (D-342).
- Choice-point slots (play positions 6 and 10) are not in this wave.
- Text: briefing in the telegram style of the built missions of your faction; all player-visible lines go in locale.toml under your
  locale prefix (§2 "Ids": auth_05 -> a5, dir_06 -> d6, ...). Voice lines stay text-only (like auth_07).
- `par_seconds`: leave the placeholder (`# PENDING par from sweep`) like auth_07 did before its sweep; the lead sets it from the cluster sweep.

## Tests (scoped; the box is shared)
- Your own test class `tests/Scripting.Tests/S48/S48<Id>Tests.cs` (loads, objectives fire, the lose condition fires, allow-list holds).
- Add your mission to the pinned lists the pilot touched (keep every existing row; another helper is adding its own row to the same lists).
- A scripted bot walk in CampaignWalks.cs so `SeedSweep` can run your mission at tiers 1, 3 and 5. Check it with 2 seeds at tier 3 only
  (`RTS_SWEEP="<id>:3" RTS_SWEEP_N=2`, see how SeedSweep reads its env); the lead runs the 12-seed sweeps on the cluster.
- Before committing, once: `--filter "FullyQualifiedName~S48|FullyQualifiedName~Campaign|FullyQualifiedName~Mission"` in Scripting.Tests and
  `FullyQualifiedName~Campaign` in Game.Tests.

## Reply (under ~25 lines)
COMMITS / CHECK / ASSUMED (every number or story choice no doc gave) / TEXT (keys added) / SKIPPED / USER CHECK.
