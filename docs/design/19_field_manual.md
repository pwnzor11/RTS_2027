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

## 6b. Directorate lore expansion (approved by the owner, session 22, D-228)
Same shape as the approved Authority entries: four entries for the Directorate chapter, in-universe voice, no campaign secrets
(the Director's death, Order Seven and the Sunrise Device stay out of the manual; premise only). Vocabulary from docs/lore and the
Archive: the Annex, levels, shifts, the Voice, the Commissariat, service year, "the tenancy", Bores, Hatches, the Land-Train.

**The Service Year.** Every citizen of the Interior is in service from the day they come of age, and a service year is not a year: it
is however long the Commissariat needs. Conscripts are issued a number, a red armband and a ration of light, and they are told
that each acre reclaimed pays down the debt the surface owes the Director. They march to the Voice, eat to the Voice and sleep to
the lullaby. A conscript who comes back below is moved up one level, nearer the blast doors and the dim red lamps that pass for
daylight. A conscript who does not come back is read out at dawn shift as "a redistribution of effort", and his level is given
to someone else.

**The Bores.** The Authority has roads. The Directorate has the dark. Forty years of digging have left the ground under the Rust
Belt hollow as a honeycomb: service shafts, flooded galleries, and the new Bores, cutting heads that chew a tunnel in a night and
leave a Hatch behind them like a seed. A Hatch is a steel lid in a field of ash; by morning it is a squad, by evening a
pillbox. Supplies move below on the Land-Trains, ore comes up through the Smelter Vents, and the surface sees only what the
Directorate chooses to show it. Authority patrols have learned to listen to the ground, and to distrust any field that is too quiet.

**First Light.** Nothing below prepares a conscript for the sky. The first ones up were found standing still in the open,
staring, until the Commissars walked the lines and reminded them where to look. Now every surfacing squad is told the sky is
"unreclaimed ceiling" and is given a task for the first hour so that nobody has time to look up. It mostly works. The Agitprop
Crawlers follow close behind with the Voice at full volume, and the Commissariat keeps a quiet count of the ones who still
glance upward when they think no one is watching.

**Doctrine for Commanders** (issued by the Commissariat of Order). The Interior does not attack; it reclaims. Open every
operation from below: a Hatch you place is a front the tenancy cannot see. Numbers are the Directorate's armour. Conscripts are
plentiful and the Annex is not, so spend the first and guard the second. Keep a Commissar with every company, because a company
without one has been known to reconsider. Bury what you cannot hold and return for it later. Report losses in the approved form.
Report doubts in person.

## 6c. Left Behind lore expansion (DRAFT for sign-off, session 23)
Same shape as the approved Authority and Directorate entries: four entries for the Left Behind chapter, in-universe voice, no
campaign secrets (premise only; no mission outcomes, no quarrels inside the leadership, nothing about what happens to the
caravans or their enemies). Each entry adds an angle the Archive pages do not carry: a custom, a day in camp, the weather,
doctrine. Vocabulary from docs/lore and the Archive: the Drift, rigs, the flag, the Gathering, the Speaker, the ledger, chalk
boards, rig radio, Tern Point, the Mother Rig and its neighbours.

**The Evening Count.** When the rigs stop for the night, somebody on every flatbed reads the flag aloud, oldest name first. The
children answer for the ones too small to speak, and the names of the dead are read in the same breath as the living, with no
pause between them. A stranger who has eaten at the fire is asked for a name before the plates are cleared, and the Muster Rig
chalks it on the board. Nobody explains the count to outsiders. The caravans only say that a rig which has been counted has
not been left, and that the count is what you do instead of a funeral when there is no time for one.

**Making Camp.** A caravan arrives in an order. The Dune Buggy goes out to smell the place and comes back. The Tow Truck lifts a
Packed Rig down off the road and the Mother Rig unfolds into a street. The Still Rig is turned to catch whatever rain there is,
because nobody works well before they have had water. Children stake the Spike Nest ring while their elders argue about the
wind. The Scrap Wall goes up out of whatever is lying about, and if the camp will stay for the season, the Salvage Yard is
laid out last, near the gate where the wrecks will come in.

**Reading the Weather.** Ash is a calendar, a map and a clock to the Drift. Children learn the sky before they learn to read:
how a grey sky sounds when it is about to turn, and how the dunes smell before the wind changes. They have more words for grey
than the Authority has forms. The listeners at the Lookout Mast watch the signals; the Veilwalkers wear the colour of the
drifts and read the ground; and when the Gleaners are called in from the field half full, nobody asks why. A caravan that
loses an afternoon to weather is lucky. It has not yet lost a rig.

**Doctrine for Commanders** (issued at the Gathering, in the Speaker's name). Keep the rigs together; a rig left behind is a
debt, and the ledger remembers. Build nothing you cannot fold. Raise the Spike Nest before the wall, and the wall before the
workshop. Let the Authority come to you, and fight where the wind and the wrecks are yours. Salvage what you break. Call the
count before an action and again after it, aloud, from the flag. Spend scrap before you spend people. Never say goodbye
to a crew; say you will see them at the next fire, and then be there.

## 6d. Expedition lore expansion (DRAFT for sign-off, session 23)
Same shape again: four entries for the Expedition chapter, in-universe voice, no campaign secrets (premise only; nothing from
the leaders' private papers, nothing about the first relief fleet beyond what the Archive prints, and nothing about how the
voyage ends). Each entry adds an angle the Archive pages do not carry: a custom, a day ashore, a working rule, doctrine.
Vocabulary from docs/lore and the Archive: the Concordat, the Second Relief Fleet, the ministries, telex, the Anchorage, the
Billet, the Landing Beacon, the Plotting Room, hull numbers, the roster.

**Telex Discipline.** Every order in the fleet is typed, in capitals, in short sentences, and ends with the words TELEX ENDS,
so that the operator at the far end, who is tired, damp and often seasick, knows the message is whole. A fleet telex opens with
the call-sign of the sender and says one thing at a time, because a second thing is how a telex gets answered with a question. Officers who speak at
length over the fleet circuit are said to be "talking to the sea". Cadets are taught that a telex is the one document
aboard that can be read twice, by two people who disagree about it.

**Hull Numbers.** A hull is not armed until it is painted. The number goes on first, in white, by a rating chosen from the
crew, and the name follows in a smaller hand beneath it. Then the thin ribbon of the home port's colour is laid along the
rail, though no hull in this fleet has been within a month of that port. Painters sign the inside of the hatch coaming where
nobody will look, and the Billet keeps a tin of white paint for touching up numbers worn thin by salt. The fleet says a ship with a clean number comes home, and every sailor knows exactly how much that is
worth.

**Ship's Time.** Ashore, the fleet keeps ship's time. Bells are struck, watches are stood and the Billet serves soup at six
whether or not the sun has been seen. The first thing a Landing Beacon does when it is lit is give the camp a clock to set
by, and the Plotting Room corrects its own against the Anchorage. The crews do not trust a field that has no tide, and they miss the sound of one at night. They say
that the only way to be sure of the hour on this continent is to carry it ashore with you.

**Doctrine for Commanders** (issued by the Ministry of Passage, countersigned by the Office of Return). Light the Beacon before
you land the guns. Hold the Anchorage; it is the fleet's only road home. Fire on the spotter's word and not before. Fight at
range, break off early and mend what you can recover. A hull is a month of home production, and a crew is more than that, so
spend fuel before you spend either. Report losses in tonnage and in names, and report them early, because the telex home takes longer than any of us. The fleet was sent to return, and it returns with the tide.

## 7. Questions (answered in session 18, see the top)
- Q-B1 Art: sprite plates for every unit plus about 10 hero renders (recommended), or a full hero render of every unit and building
  (about 150 renders on the PC, many hours)?
- Q-B2 The pilot look: keep (paper tint, condensed headings, hazard stripes, teletype boxes), or change direction?
- Q-B3 The draft text in section 4 and the lore draft in section 6: approve, edit, or redirect the voice?
- Q-B4 Unit pages: one full page per main unit plus half pages for the rest (pilot), or a uniform half page per unit (shorter)?
