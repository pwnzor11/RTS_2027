# 19 · Field Manual (game booklet) — design and pilot

Status: **pilot approved** (session 18, 2026-11-11). Owner answers: Q-B1 full Blender render of every unit and building on his laptop;
Q-B2 keep the pilot look; Q-B3 draft text and the Authority lore draft approved; Q-B4 full page for main units, half pages for the rest.
Owner brief (session 18 Q&A): a 1990s-style printed manual like Red Alert / Tiberian Sun / Age of Empires. Print-ready PDF,
half-letter (5.5 x 8.5 in) facing spreads, hybrid voice (in-universe lore and briefings, plain out-of-universe rules and stats),
full manual of about 80-120 pages, existing art plus new renders on the owner's PC (D-196), lore for the two thin factions drafted
by Claude and signed off by the owner, spoilers limited to premise plus a mission list with teasers (no endings).
Full build waits until the session 18 audit fixes land, because the stat pages are generated from `data/rules`.

## 1. How it is built
- Source: `tools/booklet/` (pilot scripts in `pilot/`: `prep.py` sprite plates and stat cards, `tree.py` tech trees split so no box
  crosses the gutter, `build.py` HTML pages and print CSS, `render.py` Chromium via Playwright; generators in `data/`; hero renders in
  `render_heroes.*`). The full build extends these. The pilot scripts are in `tools/booklet/pilot/`.
- Data: `rules_resolved.json` from the audit's `resolve_rules.py` (inheritance applied exactly as the loaders do), display names
  and body text from `data/archive` (never from ids: 80 sidebar names differ from the Archive titles today, audit UX-02).
- Fonts: TeX Gyre Heros Condensed (headings, stats), TeX Gyre Schola (body), Courier 10 Pitch (teletype, field notes); all free,
  embedded in the PDF.
- Re-run after every balance change: the numbers on the page are always the numbers in the game.

## 2. Look
Off-white paper tint, black condensed headings with a heavy rule, two-column serif body, hazard-stripe bars in the faction's
accent, stat tables in condensed type, a "damage against" bar strip per weapon (from the warhead's versus table), teletype boxes
for field notes and briefings, pixel-sprite plates on a warm card. Each faction gets an accent: Authority olive and safety yellow
(pilot); Directorate, Left Behind, Expedition accents to be picked with their pilots.

## 3. Page plan (about 108 pages + a separate quick-reference card)
| Part | Pages | Contents |
|---|---|---|
| Front | 4 | Cover, inside cover (credits, licence, "open source forever"), contents, how to use this manual |
| I · The World | 8 | The Exchange; the First Winter; the Charter; the Interior Annex; ashfall and pitchblende; timeline 1962-2001; the four powers at a glance; glossary of the setting |
| II · Getting Started | 12 | Running the game; main menu; campaign, skirmish, replays, LAN; the screen (annotated screenshot); sidebar and production; selection and orders; economy; power; supply cap; veterancy; saving and loading; difficulty tiers |
| III · The Factions | 4 x 17 = 68 | Per faction: opener spread (2), society and leaders (2), commander's notes (1), infantry (2-3), vehicles / aircraft / ships (3-4), structures and defences (3-4), upgrades (1), tech tree spread (2) |
| IV · Theatres of War | 6 | The four biomes (one page each); map design: lanes, start shapes, expansions, resources (design 16 / 17); building your own maps in the editor |
| V · Campaigns | 6 | Tutorial; one page per campaign: premise and mission list with one-line teasers (no endings); how tech is issued between missions |
| Appendices | 4 | Counter chart (who beats whom, from the warhead tables); keyboard reference; full glossary; credits and licences |
| Card | 2 | Quick-reference card: hotkeys, the four tech trees in miniature, counter chart |

## 4. Pilot (what the owner is reviewing)
`tools/booklet/pilot/out/RTS_2027_Field_Manual_pilot.pdf` (built by `tools/booklet/README.md`), 9 pages: cover; Authority opener spread; Authority field guide spread (Heavy Tank full entry,
Scraper, Rifleman, Rocketeer); Authority tech tree spread; world timeline plus Authority campaign list, and the four theatres.
All lore text on these pages is the existing, approved Archive / docs/lore text, except the items marked DRAFT below.

New text in the pilot (needs sign-off): the cover tagline "Ashfall · The War for the Surface"; the Authority "Commander's notes"
box; the world page's opening paragraph (adapted from history_the_exchange); the four theatre captions and "Reading a map"; the
one-line mission teasers (from the briefings, no endings).

## 5. Art plan and render list (renders run on the owner's PC, D-196)
**Owner decision (Q-B1): every unit and building gets a full hero render on the owner's laptop.** The sprite plates below stay
as the fallback for any id whose render is not ready. Original options for the record:
- **Unit and building plates: the in-game sprites**, enlarged with nearest-neighbour scaling and recoloured to the faction's
  house colour (as in the pilot). They match the game exactly, cost nothing, and regenerate automatically.
- **New hero renders, only where a big picture earns its place:** 4 faction opener paintings (Authority can reuse
  still_authority_act2; the other three can reuse their act-2 stills or get new ones), 1 cover painting, 4 biome panoramas for
  Part IV, 1 annotated gameplay screenshot for Part II. About 10 renders, Cycles at 2400 x 1600, same look as the act stills.
- **Blocked until art exists:** 26 ids have no sprite (audit GAMEREND-06): auth_carrier, auth_seismic_mortar, auth_foam_truck,
  auth_interceptor, auth_bomber, auth_howitzer, auth_bulwark, dir_ash_sower, dir_delver, dir_great_dirigible, auth_engineer,
  auth_marksman, dir_assessor, dir_volunteer, auth_ration_hall, auth_signals_hq, auth_blockhouse, auth_flak, auth_airfield,
  auth_atomic_battery, dir_bunk_gallery, dir_redoubt, dir_committee_hall, neu_ruin_tenement, neu_ruin_depot, lb_still_rig.
  The manual can show a silhouette frame for these, but the game needs their sprites anyway (the M4-ART item), and the booklet
  should follow the sprite pass rather than lead it.

## 6. Authority lore expansion (DRAFT for sign-off; pilot for the two thin factions)
The Authority and Directorate have about 600 words of lore each, against about 5 000 for the other two. These four entries
would extend the Authority chapter; the Directorate draft follows once the owner approves the voice.

**Read Before Operating (manual foreword, in the Administration's voice).** This manual is issued to every commander of the
citizen army under Charter Article Nine, and it is to be returned on discharge. It contains everything the Administration knows
about the war and several things it would rather not. Read it in order. Do not lend it out. Where it disagrees with an order from
the field, obey the order and file a correction. Every page was paid for by a district that went without something else to print
it, and the paper is to be recycled at the end of your labour year.

**The Labour Year.** Every citizen of the Authority owes one year in twelve to the common work: road crews, decon teams, grain
convoys or the militia. Most serve it at nineteen and again at forty, and few people choose the militia twice. A labour-year
rifleman keeps her trade card in her breast pocket under the ration book; when she is discharged she goes back to it, and the
Authority considers a soldier who has forgotten her trade to have been badly commanded.

**The Convoy Roads.** The Authority is held together by four thousand miles of graded road and the people who drive it. A convoy
is a town on wheels: scrapers in the middle, a decon truck at each end, riflemen riding the hoppers, a jeep half a mile ahead with
a radio and a flare gun. Convoy drivers are the closest thing the Authority has to heroes, and the ones who come home every time
are known by name from one district hall to the next.

**Doctrine for Commanders.** Power first, then a perimeter, then a road out. Never send infantry into the ash without a carrier
behind them. Count your shells and your credits before every attack, because the Administration will. Tanks are expensive to
build and cheaper to keep alive; repair them. Hold ground you can supply and give up ground you cannot. The Authority does not
win quickly. It wins by still being there.

## 7. Questions (answered in session 18, see the top)
- Q-B1 Art: sprite plates for every unit plus about 10 hero renders (recommended), or a full hero render of every unit and building
  (about 150 renders on the PC, many hours)?
- Q-B2 The pilot look: keep (paper tint, condensed headings, hazard stripes, teletype boxes), or change direction?
- Q-B3 The draft text in section 4 and the lore draft in section 6: approve, edit, or redirect the voice?
- Q-B4 Unit pages: one full page per main unit plus half pages for the rest (pilot), or a uniform half page per unit (shorter)?
