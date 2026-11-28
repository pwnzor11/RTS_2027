# Session 49 — shared brief for campaign mission packages (C-12 waves A and B: acts 2-4)

Read after docs/COMMON.md and docs/process/s49_wave_rules.md. Your package brief names ONE mission id (or a map / a fix); build it end to end,
like the session-47 pilot auth_07 and the session-48 act-1 missions did.

## Read only these
- docs/design/22_campaign_expansion.md: §2 (structure rules, all of it), your faction's table and your mission's paragraph in §3,
  §4.1-4.2 (timeline and characters for your faction), §6 (the auth_07 pilot spec: the level of detail your mission needs).
- Templates: the pilot (`git show --stat b5a8ea4f`: data/missions/auth_07/{mission.toml,mission.lua,briefing.md,locale.toml}, its campaign
  wiring, tests/Scripting.Tests/S47/S47PilotTests.cs) and ONE session-48 mission of your faction (`git log --oneline -- data/missions/<f>_06`),
  with its rows in tests/Scripting.Tests/M8_15/CampaignWalks.cs, MissionParTests.cs, S19/CampaignAllowListTests.cs,
  S22/MissionScriptKeysTests.cs, tests/Game.Tests/Campaign/CampaignFilesTests.cs and MissionLinksTests.cs.
- The mission just before yours in play order (its starts_with and unlocks_on_win), and the one after it (its starts_with), if they exist.
- docs/lore/ for your leader and the characters your paragraph names only. Do not read other factions' missions.
- Map: read your map's header comment (zones, starts, rivers, bridges) and look at the tiles; check every zone you use against the terrain.

## Owner rules for every mission (D-337, D-341, D-342, D-345)
- Ids, play order, map, mission type, unlocks: exactly as the §3 table says. The campaign list (data/campaigns/<faction>.toml) sets play order:
  insert your mission at its play position; `requires` = the mission before yours in play order. **If that mission does not exist yet** (another
  helper is building it in parallel), require the nearest earlier mission that exists and add a comment `# s49: requires <planned id> at merge`;
  the lead fixes the chain. Fix `requires` on the next existing mission only if it is a built one that now follows yours directly.
- Set your mission's `next` to the next id in play order (even if it is not built yet; the lead updates MissionLinksTests).
- Act stills (§2 "Stills"): the act-3 still moves to play position 7, the act-4 still to play position 10 (one line in the campaign file, by the
  package that builds that mission).
- Tech slot-in: your `starts_with` is the previous mission's starts_with plus its unlocks (the §3 tables); your `unlocks_on_win` is the table's
  unlock list. **Upgrades (Q5 b, D-341):** list your faction's upgrade ids in starts_with like the s48 missions do. Do NOT edit any other
  mission's upgrade list; if your mission unlocks a "(Q5 b)" upgrade, say so under ASSUMED and the lead removes it from the earlier missions.
- Choice-point slots (play positions 6 and 10, marked C-03): build the mission the table describes (the main path). The alternative is later.
- D-163 template: one leader on the radio, primary objectives plus one hidden bonus, at least one visible timer or clear clock; enemy pressure
  by unit count and AI tier only (D-061); tier tables for 1-5 like auth_07. Targets (12-seed sweep): tier 1 >= 10/12, tier 3 >= 8/12,
  tier 5 >= 3/12; aim tier 5 at 4-9/12 (D-234): do not make tier 5 a walkover (six s48 missions won 12/12 at tier 5).
- No-base (commando) missions must lose explicitly: `Trigger.OnAllDestroyed(0, ...)`. Use `Player.KeepAlive` (S47 C-06) for enemy players that
  must not be defeated early, never hidden reserve buildings.
- Leader secrets (D-341 Q9): hint them on screen only; never state an Archive secret in a briefing or radio line.
- Wrenfield is Brigadier Hester Wrenfield, "she/her" (D-342). Her brother is never named.
- Text: briefing in the telegram style of the built missions of your faction; all player-visible lines in locale.toml under your locale prefix
  (§2: auth_08 -> a8., dir_10 -> d10., lb_07 -> l7., exp_12 -> e12.). Voice lines stay text-only.
- `par_seconds`: leave the placeholder (`# PENDING par from sweep`); the lead sets it from the cluster sweep.
- Map edits: you own your mission folder, not the map. If your mission needs a map change, list it under SKIPPED; never edit a map file
  unless your brief owns that map.

## Tests (scoped; the box is shared)
- Your own test class `tests/Scripting.Tests/S49/S49<Id>Tests.cs` (loads, objectives fire, the lose condition fires, allow-list holds).
- Add your mission to the pinned lists the pilot touched (keep every existing row; other helpers add their own rows to the same lists).
  Prefer computed counts over pinned counts where a test pins a number every package bumps.
- A scripted bot walk in CampaignWalks.cs (one Campaigns row, one TickLimit row, one switch case, one walk method; keep the method
  self-contained so the lead can move it by brace matching) so `SeedSweep` runs your mission at tiers 1, 3 and 5. Check it with 2 seeds at
  tiers 3 and 5 (`RTS_SWEEP="<id>:3" RTS_SWEEP_N=2`, see how SeedSweep reads its env). Story missions are mostly seed-independent, so a
  1-seed probe per tier answers tuning questions in seconds; tune tier 5 so the bot does not win trivially. The lead runs 12-seed sweeps.
- Before committing, once: `--filter "FullyQualifiedName~S49|FullyQualifiedName~Campaign|FullyQualifiedName~Mission"` in Scripting.Tests and
  `FullyQualifiedName~Campaign` in Game.Tests. Commit as soon as each step works (`feat(missions): <id> <Title> [M16-C-12]`).

## Reply (under ~25 lines)
COMMITS / CHECK (tier 1/3/5 probe results) / ASSUMED (every number or story choice no doc gave; upgrades to gate) / TEXT (keys added) /
SKIPPED / USER CHECK.
