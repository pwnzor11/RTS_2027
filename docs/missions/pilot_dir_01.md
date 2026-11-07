# Directorate 01 "The First Hatch" (M8-10 D1)

Story: 2001, the Directorate surfaces openly. The player raises a Reactor Vent, opens a Hatch on the marked ground of the Rust Belt pit and
stands four Conscripts at it. The Authority answers in two waves (second one on a visible timer); a hidden bonus is the Authority picket in the north-east.
Leader on the radio: the Voice (Odette Larkin, leader_dir_larkin), who adds a true weather remark (her lore secret, hinted only).
Tech: start with dir_reactor_vent, dir_muster_hatch, dir_hatch, dir_pillbox, dir_conscript, dir_ashthrower (the pre-placed dir_shaft is the root); unlocks dir_sapper for dir_02.
Map: rustbelt_m2 (shared act map, D-030); zones `site` (hatch ground) and `picket`. Wave strength by tier: wave 1 rifles 1,2,2,3,4; wave 2 rifles 2,2,3,4,5 plus a jeep from tier 4.
Agent time: about 1.2 agent hours (read 0.3, write 0.3, tuning and tests 0.6).

API gaps and notes
1. Build radius is 2 cells from an own building, so the hatch zone must sit next to the shaft (vent first, then hatch); no way to say "build here" in the script.
2. Directorate conscripts (40 hp) die fast to Authority riflemen; the first wave sizes had to be lowered. Balance is untouched (not my files).
3. AI.Defend(1, "site") is called so the Authority base does not attack on its own; `--mission-check` with an idle player ends Lost at 6000 ticks with 0 errors.
