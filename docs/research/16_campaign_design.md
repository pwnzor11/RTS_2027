# Campaign and Mission Design in Classic and Modern RTS

**Summary**

- The classic C&C campaigns used a simple shape: one campaign per side, roughly 13 to 15 missions each, a choice of target map at some points, and a tech tree that opened as the player built the right buildings. Tiberian Dawn's GDI and Nod campaigns had 15 and 13 base missions. Story came through FMV briefings and cutscenes, not through the unlock system.
- Newer RTS campaigns moved toward mission variety and story-tied unlocks. StarCraft II lets the player choose mission order, and those choices change units and research. Tempest Rising runs two parallel factions as one canon storyline with 11 missions each plus an expansion.
- Mission archetypes are cheap to vary when designers reuse maps with new triggers. Red Alert did this, and Zero-K (an open-source Spring-engine game) built its whole campaign as a branching web of short missions.
- The best-documented failure mode is pacing. Dawn of War: Dark Crusade's hand-made stronghold battles lost tension after the first 30 seconds, and critics called RTS campaigns too long and grindy.
- Story on a hobby budget works best as text briefings with portraits, music, and a few still or in-engine cutscenes. Full FMV and voice acting are expensive. Tools such as Yarn Spinner can handle branching dialogue as plain text files.
- OpenRA's Lua trigger API (Trigger, Actor, Reinforcements, MissionObjectives, Media) is the closest open model for a scripted campaign. Its docs do not confirm a direct per-mission tech-unlock call.

## 1. How classic and modern campaigns are built

| Game | Shape | Missions | Unlock approach | Story delivery |
|---|---|---|---|---|
| Tiberian Dawn (1995) | Two campaigns (GDI in Europe, Nod in Africa) | GDI 15, Nod 13 in the base game. Covert Operations (1996) adds 7 GDI and 8 Nod | Buildings open advanced units through the tech tree. Not tied to specific missions | FMV briefings mixing CG and live action; Joseph D. Kucan (Kane) filmed |
| Red Alert (1996) | Allied and Soviet campaigns | Sources disagree: 14 per side in the brief, 15 per side in one retrospective. Unverified | Units unlock by building the right structure; no upgrades | FMV and pre-rendered CG before and after each mission; Retaliation added 19 briefing clips |
| StarCraft (1998) | Three episodes, one race each | 10 per race, 30 total; Enslavers adds 5 | Not described in the source | Manual, briefings, in-mission conversation, cinematics at key points |
| StarCraft: Brood War (1998) | Three new episodes | Not stated | Not stated | In-mission scripted events; objectives reflect the story; fewer pure annihilation missions |
| Warcraft III (2002) | Four campaigns in fixed order | Not stated | Heroes carry over with experience | In-engine cutscenes; pre-rendered endings |
| Homeworld (1999) | One campaign, 16 levels | 16 | Fleet persists; new ships come from capture, research at a research vessel, or plot events | In-engine cutscenes with narrated voiceover; text briefings |
| Dawn of War: Dark Crusade (2006) | Meta map plus battles | Not stated | Territory and sector bonuses on a strategic map | Briefings and in-engine cutscenes; "lore without story" |
| Tempest Rising (2025) | GDF and Tempest Dynasty run in parallel, then Veti expansion | 11 + 11 + 11 = 33 | Doctrine and Armory loadouts before each mission; specialists introduced during the campaign | In-engine CGI cutscenes; superior briefs the player and can be talked to |
| Zero-K (open source, Spring engine) | Branching web of connected missions | About 70 planned; 36 released at one point | Winning missions unlock 2 or 3 units per factory; commander modules carry over | Team chose branching structure because they were not confident writing a linear story |

**How map selection works.** Tiberian Dawn and Red Alert let the player pick a target on the campaign map for some missions. In Red Alert the map and scenery change, but the objective often stays the same. Some maps come back with different objectives later, and structures the player built earlier stay on the map. Tiberian Dawn's sources say the early titles had "branching paths," but how the branches connect is not documented in what I read. Red Alert's campaign is still mostly linear with a few choice points.

**Parallel versus mirrored campaigns.** Red Alert's two campaigns end differently. The Allied ending follows the siege of Moscow. The Soviet ending reveals Kane as the real power behind Stalin. A retrospective says designer Adam Isgreen confirmed that completing the Allied campaign leads toward Tiberian Dawn. Tempest Rising takes the other path: its two main factions run in parallel as one canon story, and the article says there are no "good" factions, so the player makes morally grey choices.

**Pacing and difficulty.** Dark Crusade's stronghold battles were hand-crafted, with special victory conditions and scripted events. A reviewer found they started strong, then dropped off: the first 30 seconds brought a major attack, then the AI sent a constant trickle and never expanded or grew its waves. The reviewer's conclusion was that pacing matters as much as level type. Challenge should escalate over the match, and oversized maps with no threat drag. The regular skirmish-style battles, by contrast, kept tension for about 30 minutes because the AI pressured the player throughout.

## 2. Mission archetypes

Each archetype has a different build cost. Most of them can reuse a map with new triggers, which is the cheapest variety a small team can get.

- **Build and destroy.** The baseline. Good for teaching the economy. Cheap to write because it needs no special triggers. Risk: it becomes the whole campaign if overused. Brood War deliberately cut the count of pure annihilation missions.
- **Defend or hold out.** Tempest Rising uses it for GDF 04 and GDF 08. Needs a wave script and a timer. Works well with an "until reinforcements arrive" end state.
- **Escort.** Tempest Rising Veti 09 (convoy and base protection); Twisted Insurrection's "Friend in Need" (a convoy must reach a point intact). Needs a unit that the trigger tracks and a failure condition on its death.
- **Commando or infiltration (no base).** Tempest Rising GDF 03 and Dynasty 07 use strike missions. Red Alert's spies and thieves on the Allied side fit here. Cheap to build because the map is small, but testing takes longer because players must navigate without a safety net. Red Alert's "baseless and indoor" missions were called trial-and-error designs.
- **Capture or secure a location.** Tempest Rising Dynasty 11 and Veti 11. Trigger on a proximity area held for N seconds.
- **Timed.** Red Alert's timers and end-of-mission statistics add replay pressure. Use for a rescue or a demolition.
- **Ambush.** Tempest Rising GDF 05 ("The Pass"). Hides forces until a trigger fires.
- **Hold a stronghold or assassinate a commander.** Dark Crusade's hand-made battles. Works for climaxes; costly to tune.

**Mixing.** Good campaigns alternate. Tempest Rising's list shows a pattern: a territory mission, then an ambush, then a hold-out, then a strike, then an escort. Designers commonly give the first mission of each side a simple build-and-destroy so the player learns the controls, then add a no-base mission about one-third of the way in to break the rhythm.

## 3. Story delivery on a hobby budget

**What the classics did.** FMV was the expensive option. Tiberian Dawn and Red Alert used live actors, and the tradition let designers spend money on big set pieces. Homeworld used the game's own engine for its cutscenes with narrated voiceover. Warcraft III used in-engine 3D cinematics with a side-view camera, and pre-rendered CG for the endings.

**What modern games did.**
- Tempest Rising gives each mission a briefing from the player's superior, plus in-engine CGI cutscenes. The superior can be talked to for more background. Primary and secondary objectives are marked on the minimap.
- StarCraft II gave most missions a gimmick, and the best made the objective part of the story (for example, collecting artifacts from a doomed planet). Between missions, NPC conversations and upgrade screens carry the story.
- Homeworld and Warcraft III used loading screens on the world map to explain the setting between levels.

**The cheapest effective kit.** Based on these sources:
1. A briefing screen with text, a portrait, and music. Tempest Rising and the classic C&C games used this format alongside FMV.
2. In-mission messages from named characters, triggered by game events (a unit dies, a building is captured). OpenRA exposes this through `Media.DisplayMessage` and `UserInterface.SetMissionText`.
3. A small number of still-art or in-engine cutscenes at act breaks, which is what StarCraft and Homeworld did at key points.
4. Voice acting last. It can be added later without changing the scripts, if the text is stored in a separate file (Yarn Spinner and OpenRA's `.ftl` localization files both work this way).

**Objective updates.** OpenRA has a MissionObjectives trait: `AddPrimaryObjective`, `AddSecondaryObjective`, `MarkCompletedObjective`, `MarkFailedObjective`, plus hooks `OnObjectiveAdded` and `OnObjectiveCompleted`. Completing all primary objectives wins. This is a good model for an objectives list that updates mid-mission.

## 4. Tech-tree gating as narrative

The principle from the design essays is that the story should introduce a unit before it is available. Designers do this by giving the unit a reason to exist in the plot. Homeworld's ships unlock through capture, research or plot events. StarCraft II lets the player's mission choices change which units and research they get. Zero-K's unlock is structural: factories are themselves unlocked and each holds a cluster of missions.

**The conflict with skirmish.** Skirmish trees are balanced for simultaneous play. Campaign trees are paced over time. A unit that is strong in skirmish may be introduced too early or too late in a campaign. Zero-K handles this by making each factory "soft" (it can be built later at moderate cost) and balancing all factories so none become obsolete. That lets unlock order stay flexible.

**Two campaigns, two paces.** Red Alert's two sides share many units but emphasize different ones; the Soviet side's tech felt more distinct. A practical approach is one shared tech list with a per-side unlock schedule, so the skirmish tree is unchanged and each campaign just decides when a unit appears.

**Persistence.** Some games carry units from mission to mission (Homeworld's fleet, Warcraft III's heroes), others rebuild each time (Tiberian Dawn). Carry-over makes the player attached to units but makes failure costlier. Zero-K's own note: "restarting a mission does not destroy too much progress" only works because missions are short.

## 5. Writing: factions, villains, arcs, tools

**Making both sides sympathetic.** The sources point to three approaches:
- **Opposed but equal** (Tiberian Dawn, Tempest Rising): both sides have a reason. Tempest Rising makes choices in each mission morally grey.
- **Deliberately unequal** (Red Alert): the Allied route is the heroic one, and the Soviet route is "openly villainous", including a massacre and a purge. The writing still gives the Soviet side a character arc; a reviewer called the tone "decidedly serious if not grim."
- **Hidden villain** (Red Alert's Kane reveal): one side's ending changes the meaning of the other.

**Villains.** Game essays recommend establishing the villain through the world before the first battle. One example is Atriox in Halo Wars 2, which is referenced in a critique rather than verified here. Red Alert's Stalin and Tiberian Dawn's Kane both appear in cutscenes before players fight them.

**Arcs.** Warcraft III's campaigns each follow one hero (Arthas, Thrall, Tyrande) and link through a shared story. Each campaign opens with a simple level that introduces its hero and units, then one or two building missions, then an "epic battle" at the end. That is a repeatable template for a 10- to 12-mission side.

**Tools.** Yarn Spinner is a screenplay-style dialogue tool. Scripts are plain `.yarn` files with a `title:` header, `---` and `===` markers, `Speaker: text` lines, `->` choices, `<<set $var>>` variables, and `<<command>>` calls into game code. It has integrations for Unity (2022.3 or later), Godot 4.6 or later, and Unreal 5.7 (pre-release), plus a VS Code extension with a graph view. The core licensing terms were not verified. Ink (Inkle) and Twine appeared in searches but I did not read their docs, so their details are unverified.

## 6. Production reality

- **Volume.** Zero-K had about 70 planned missions and released 5 to 7 per week, with 36 out at one point and about 50 complete. That is a large team's pace, and the team said the large count drove them to short missions.
- **Short missions.** Zero-K's argument: a short mission reduces the cost of failure, so players will retry, and the team gets feedback on a few missions at a time. Most winning playthroughs took 10 to 20 minutes.
- **Map reuse.** Red Alert reused maps with new objectives and kept structures from earlier missions. This is the cheapest way to get campaign length.
- **Scripting.** OpenRA maps are Lua scripts inside the map archive next to `map.yaml` and `map.bin`. The Lua API has `Actor`, `Trigger` (`AfterDelay`, `OnKilled`, `OnAllKilled`, `OnEnteredProximityTrigger`, `OnIdle`, `OnPlayerWon`), `Reinforcements`, `Media`, `UserInterface`, `Map` queries, and `Utils`. Movies play through `Media.PlayMovieFullscreen`. The docs do not confirm a per-mission tech-unlock call; `Player.GrantCondition` and an ExternalConditions trait might work, but this is unverified.
- **Campaign chaining.** The OpenRA map-scripting page does not describe how campaign missions link together. The campaign YAML was not read, so that is unverified.
- **Time per mission.** I found no sourced number for how long one mission takes a hobby team to build and test. Treat any estimate as unverified and measure it on the first two missions.
- **Difficulty.** Tempest Rising has four difficulty levels that change enemy force size and starting veterancy, and campaign balance differs from skirmish. Homeworld adjusts difficulty slightly based on fleet size at mission start.

## Implications for RTS_2027

- **Recommendation: about 10 to 12 story missions per side, plus a short tutorial.** This matches the Warcraft III template and the Tiberian Dawn counts. Fewer than 8 per side feels thin; more than 15 starts to show the pacing and grind problems Dark Crusade had. Trade-off: longer campaigns need more maps.
- **Recommendation: one spine per side with two or three choice points.** Each choice point offers two target maps (the Tiberian Dawn and Red Alert pattern), and both choices lead back to the spine. This is cheaper than a full branching web, which Zero-K needed a large team to produce.
- **Recommendation: reuse maps across the two sides.** Each map appears in both campaigns with different triggers and objectives, the way Red Alert did. This roughly halves map-building cost.
- **Archetype mix.** Roughly half build-and-destroy, a quarter defend or escort, and a quarter commando or strike. Mark one mission per side as no-base to break the rhythm. Avoid long hold-out battles; Dark Crusade's example shows they lose tension.
- **Story delivery.** Text briefings with portrait, music, and a per-mission objectives list. Two or three in-engine or still-art cutscenes per side, at act breaks only. Store all text in one locale file so voice can be added later.
- **Unlock pacing.** Tie unit unlocks to mission completion and the story beat that introduces the unit. Keep one skirmish tech tree and let each campaign decide when each unit appears. Do not let a campaign unlock change skirmish balance.
- **Scripting needs.** A trigger system with at least: time delay, unit killed, all killed, proximity enter or hold, idle, mission won or lost; reinforcements with transports; objectives with primary and secondary flags; per-mission availability flags for units and buildings; messages and music. This is the OpenRA Lua surface, which is a useful checklist even if RTS_2027 uses a different language.
- **Carry-over.** Start with no unit carry-over, except possibly a named hero or commander. Carry-over adds failure cost and adds a state the scripts must track.
- **Recommendation: use a structured dialogue format for branching story.** Yarn Spinner-style plain text makes the writing testable without the engine. Confirm its license before committing.

## Open questions for the owner

1. **Canon or parallel?** Should the two campaigns form one canon storyline (Tempest Rising), be opposed but mirrored (Tiberian Dawn), or have one side's ending change the other (Red Alert's Kane)? Each has different writing cost.
2. **Mission count per side.** 8, 10 or 12 story missions per side? The count sets the map budget and the pacing curve.
3. **Morality.** Are the two factions equally justified, or is one clearly the protagonist? The answer decides the villain design and how much the story needs to explain each side.
4. **Voice or text?** Is voice acting worth it, or should the campaign ship text-only with portraits and music? A text-only campaign can be finished sooner; voice can be added later if text is stored separately.
5. **Carry-over or fresh start?** Do units or heroes carry between missions, or does each mission start with a fresh base?
6. **Meta layer or none?** A Dark Crusade-style strategic map adds a lot of design work. Option A: none, just linear missions with choice points. Option B: a simple sector map that only picks the next mission. Option C: a full meta layer as a later stretch goal.

## Sources

Web sources read for this file:

- [Command & Conquer: Tiberian Dawn, Wikipedia](https://en.wikipedia.org/wiki/Command_%26_Conquer:_Tiberian_Dawn)
- [Command & Conquer: Red Alert, Wikipedia](https://en.wikipedia.org/wiki/Command_%26_Conquer:_Red_Alert)
- [Command and Conquer: Red Alert 1 - A Retrospective, Rambling Nerd](https://ramblingsnerd.substack.com/p/command-and-conquer-red-alert-1)
- [Command Conquer Tiberian Dawn, Let's Play Archive table of contents](https://lparchive.org/Command-Conquer-Tiberian-Dawn)
- [StarCraft (video game), Wikipedia](https://en.wikipedia.org/wiki/StarCraft_(video_game))
- [StarCraft: Brood War, Wikipedia](https://en.wikipedia.org/wiki/StarCraft:_Brood_War)
- [Warcraft III: Reign of Chaos, Wikipedia](https://en.wikipedia.org/wiki/Warcraft_III:_Reign_of_Chaos)
- [Homeworld, Wikipedia](https://en.wikipedia.org/wiki/Homeworld)
- [Tempest Rising Campaign, Tempest Rising Wiki](https://tempestrising.wiki.gg/wiki/Campaign)
- [Dawn of War Dark Crusade: The promise and peril of pacing, Matchstick Eyes](https://www.matchstickeyes.com/tag/dawn-of-war/)
- [What makes RTS games fun: meta campaigns, Wayward Strategy](https://waywardstrategy.com/2019/08/10/what-makes-rts-games-fun-meta-campaigns)
- [Campaign design and the world, Wayward Strategy](https://waywardstrategy.com/2018/06/22/campaign-design-world/)
- [One man war: examining single-player RTS design, Game Wisdom](https://game-wisdom.com/critical/one-man-war-examining-single-player-rts-design)
- [Map scripting, OpenRA wiki](https://github.com/OpenRA/OpenRA/wiki/Map-scripting)
- [Lua API reference, OpenRA docs](https://docs.openra.net/en/release/lua/)
- [Yarn Spinner](https://www.yarnspinner.dev/)
- [Singleplayer campaign halfway released, Zero-K devlog](https://zerok.itch.io/zero-k/devlog/16346/singleplayer-campaign-halfway-released)

Not read, so unverified: Liquipedia's StarCraft campaign page (fetch failed); the Tiberian Dawn and Red Alert mission lists on cnc.fandom.com and StrategyWiki (fetch failed); Ink and Twine documentation; OpenRA campaign YAML; the Rambling Nerd Tiberian Dawn article (fetch failed).

## Gaps

- Red Alert mission count: 14 per side in the brief, 15 per side in one retrospective. Not resolved.
- Red Alert Tanya: one summary says she appears only in skirmish; the retrospective says the Allied route introduces her. Conflicting; not resolved.
- Per-mission tech unlock lists for Tiberian Dawn, Red Alert, StarCraft and Tempest Rising were not found in any read source.
- No sourced build-and-test time per mission for a small team.
- No sourced FMV or voice cost figures.
