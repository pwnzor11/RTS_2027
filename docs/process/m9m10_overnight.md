# M9 + M10 overnight build (parallel to session 19)

Owner request 2026-11-12 (late evening): build M9 LAN and the code-only parts of M10 overnight, unattended, and land them after
session 19 has landed. Owner answers before leaving: base = a snapshot of integ-s19 (`4db2516`), M10 scope = all code-only parts
(weather, battle juice, water, graphs, map previews, medals/records, loading/credits, faction-aware music, installer, releases + update
notice), landing = merge each PR when Windows CI is green, with a restore branch first. Death animations, portraits and the faction UI
skin wait for art / a pilot. Plans built to: docs/design/20_m9_lan_plan.md §9 and docs/design/21_m10_polish_plan.md (PRs #64, #65).

Integration branch: `integ-m9m10` (from origin/integ-s19 at 4db2516). This file is folded into HANDOFF/SESSION_LOG at landing.

## Baseline (Linux, before any change)
Sim 1795/1800 (5 skipped), AI 347/348, Llm 152, Tools 22, Game 1781/1800 before `git lfs pull` (17 art-pointer failures, all
pass after LFS), Scripting 332/336 with 2 failures already on integ-s19: exp_04 tier 5 never finishes (BalanceTableTests,
ExpeditionMission04Tests). Not ours; reported to session 19 through the owner.

## Lead decisions taken unattended (assumed - confirm at landing)
- Build order: the owner asked for M9 and M10 code now, before M13/M14 land (supersedes the D-171/D-205/D-206 order for this work only).
- Contracts: `src/Net` project (Sim-only dependency) with NetMessage, INetTransport, NetProtocol, NetCodec; SimHost.Outbox +
  SimHost.StepWithBundle as the only SimHost seam. No sim, save, replay or rules-hash format change in wave 1.
- Settings gains weather, screen_shake, check_for_updates, player_name, lan_game_port, lan_discovery_port (pre-added once).

## Waves
- Wave 1: M9-A codec/UDP/discovery, M9-B lockstep core + soak, M10-A weather + juice, M10-B water, M10-C graphs + medals,
  M10-D previews + loading + credits, M10-E music states, M10-F installer + releases + update notice.
- Wave 2 (after wave 1 merges): M9 lobby, discovery UI, in-game LAN (waiting, chat, pause, drop to AI, host-leave continue vs AI,
  desync reports + resync, spectators, map transfer, LAN replays), `lan soak --udp`.

## Open for the owner (from the overnight run)
- Q-M10C-1: par-time medals need per-mission par times; no campaign data has one. Supply times, or say how to derive them (stub in MedalRules.cs).
- M10-F assumptions: dev builds never show the update notice; "-pre" tags are never offered; release zip is RTS_2027-<tag>-win-x64.zip; the installer needs admin rights (Program Files).
- M10-B: ships leave a wake while dived (UnitView has no dived flag); water animation runs on the game tick, so it freezes while paused.
