fix-screens (branch wp/s18-fix-screens, worktree /home/claude/wt/s18-fix-screens). All 12 items are done and committed.

COMMITS (newest first; every subject ends with the Sonnet 5.5 trailer, except the first two, which have the "Co-Authored-By: Claude" trailer from the brief):
- 3a06e1b chore(repo): statics collection, probes skipped honestly, CI timeouts and permissions, README status [S18-TESTS-REPO-01, TESTS-REPO-05, TESTS-REPO-03] (item 11; includes the removal of cls_kA.txt)
- ccf3cfc fix(about): ship docs/CREDITS.md with the build and list every credit in full [S18-TESTS-REPO-02] (item 12)
- f1795e3 fix(setup): skirmish panel fits at 200%/XL, fixed slot label column, cached key check [S18-UX-LM-02, UX-LM-03, GS-13] (items 2, 3, 6)
- f31127a fix(setup): log dropped maps; validator check on hand-made maps [S18-GS-16, DATA-TOOLS-03] (items 7, 9)
- e81db74 fix(screens): data errors show a Notice instead of crashing; Restart guarded [S18-GS-10] (item 5)
- a35c79c fix(menu): Back label fits, help rows not selectable, editor rows/status fit [S18-UX-LM-10, UX-LM-12, UX-LM-14] (item 4)
- 54f8e6e fix(llm): one key per provider, no key over plain http, migrate old key [S18-AI-07] (item 8)
- 579cceb fix(tools): ai timeline builds the map ai match plays on; pin the start-share label [S18-DATA-TOOLS-02, DATA-TOOLS-04] (item 10)
- 6ef67dd fix(menu): main menu shrinks to fit the window at any UI scale and text size [S18-UX-LM-01] (item 1)
Items 2 to 9 were committed late, in grouped commits, because files overlap (SkirmishSetupScreen, MenuPanel). Intermediate commits may not each build on their own; the branch tip does.
Scope note: the only change under src/Game/Saves is the widened catch clause in GameLoader.cs (`or MapFileException or FormatException or KeyNotFoundException or IndexOutOfRangeException`). fix-missions owns the rest of that folder.

VERDICT TABLE (finding | verdict | test | fixed? | note)
- UX-LM-01 | CONFIRMED | S18/MenuMainLayoutTests (4 text sizes x 100/150/200% at 1280x720, with and without a save) | yes | The main menu overflowed the footer and window. It now shrinks in steps, like MenuPanel.Fitted. The test failed on the old code.
- UX-LM-02 | CONFIRMED (code) | S18/MenuPanelLayoutTests | yes | The setup panel width changed with its row text. It now keeps one width via `WidthTexts` and the shrink floor is 0.3 (was 0.5). Weak point: the FloorShare 0.5 mutation did not clearly fail a panel-fit test. The tests for the fixed label column and constant width did fail on the old code.
- UX-LM-03 | PARTLY | S18/SetupSlotTextTests | yes for (a) and (b) | Fixed: a fixed-width label column with ellipsis, a separate colour swatch column, and a constant panel width. The no-key hint moved to an Info row. Not touched: the silent switch to Spectate (design question below).
- UX-LM-10 | CONFIRMED | S18/MenuBackLabelTests | yes | The Back label was wider than its box on About and How to play. `ButtonSizing.WidthFor` now sizes the button, and the label is centred.
- UX-LM-12 | CONFIRMED | MenuPanelLayoutTests, MenuBackLabelTests | yes | Help, empty-list and detail rows were selectable. They are now `Info` rows: dim, skipped by keys and mouse.
- UX-LM-14 | PARTLY | MenuBackLabelTests (editor layout) | partly | Fixed: editor row and bar heights follow the font, and issue, header and status text is cut with an ellipsis. Not done: "validate only after the first edit". That is a behaviour choice, and I did not change when the validator runs.
- GS-10 | CONFIRMED | S18/MenuGuardTests | yes | `ScreenGuard.TryCreate/Push` shows a Notice for data errors. `GameFlow.Restart` is guarded and restores a main menu if the stack is empty. `LoadGameScreen.Start` and the `GameLoader` catch are widened.
- GS-13 | CONFIRMED | SetupLlmKeyTests | yes | `HasCommanderKey` is cached and refreshed only when the settings object changes.
- GS-16 | PARTLY | SetupMapCheckTests / map list tests | logging yes; hiding no: proposal | Dropped maps now log one line to stderr: "map list: N map file(s) not offered: name (reason); ...". No dev-map notion exists, so hiding test_32/test_64 is a proposal. `LoadedGame.Note` is still never shown (not touched).
- AI-07 | CONFIRMED | Llm.Tests/S18/LlmKeyPolicyTests, Game.Tests/S18/SetupLlmKeyTests | yes | One key per provider (`ProviderKeyring`; files llm.anthropic.key, llm.openai.key). A key is never sent over plain http except to localhost/loopback. The connection test follows the same rule through the providers. The migration rule is under QUESTIONS.
- DATA-TOOLS-03 | CONFIRMED | S18/SetupMapCheckTests | yes (skirmish hand-made maps) | The validator was never run on load. All shipped maps and mission maps have 0 errors (proved in a test first). Errors now show a "This map cannot be played" Notice.
- DATA-TOOLS-02 / M12-12b | CONFIRMED | Tools.Tests/S18/AiTimelineTests | yes | Timeline and match now share `AiCommands.BuildMatchMap`. Before, the Expedition got no Command Ship or landing there; now it does. Timeline also accepts left_behind and expedition.
- DATA-TOOLS-04 | pinned | AiTimelineTests (label regex `start 0 \(\d+,\d+\) (\d+), start 1 \(mirror\) (\d+)`, plus a test that the .ps1 scripts still contain it) | n/a | The label is unchanged.
- TESTS-REPO-01 | CONFIRMED | UiStaticsCollection | yes | `[Collection("UiStatics")]` (DisableParallelization) is on UiScaleRuleTests, FxLookTests, PlayerColoursTests, M4SkirmishIntegrationTests and the S18 menu layout tests. I did NOT put CardRoutingTests (GameKeys.Active) in it, because fix-ui already put that class in its own collection. See merge note below.
- TESTS-REPO-05 | CONFIRMED | n/a | yes | New `ProbeFact("ENVVAR")` attribute (tests/Sim.Tests/S18 and tests/Game.Tests/S18) reports as Skipped with a reason. It is on CarrierBalanceProbe, SpectatorPerfBench, HouseColourProof and VisionPerformanceTests. The S13 `>= 0` check is replaced with InRange bounds. SfxrTests now fails on CI when Python is missing and still returns locally.
- TESTS-REPO-02 | CONFIRMED | S18/SetupCreditsTests (11 cases) | yes | Game.csproj now ships docs/CREDITS.md at docs/CREDITS.md beside data/, and a real `dotnet publish` produced the folder. `ParseCredits` no longer cuts names at 75 characters and uses the Licence column of the asset tables. Credits are not reworded. 7 of 11 tests failed on the old code.
- Repo hygiene | done | n/a | yes | `cls_kA.txt` removed. ci.yml has `permissions: contents: read` and timeouts (90 min for build-test, 30 for publish-game). The README status section is updated from ROADMAP and HANDOFF.

Verify-only items (all proposals; nothing changed):
- UX-LM-04 / GS-06, UX-LM-05 / GS-15, UX-LM-07 / GS-12, UX-LM-08, UX-LM-09, UX-LM-11, UX-LM-13, UX-LM-15, GS-11: I did not build tests for them. Each is an owner choice (player-visible wording or design). The proposal list is the original findings text in /home/claude/audit/ux-live-menus and game-screens.

CHECK (scoped runs on the final tree; all foreground or awaited)
- Game.Tests filter "S18|UiScaleRuleTests|FxLookTests|PlayerColoursTests|M4SkirmishIntegrationTests|SfxrTests|SpectatorPerfBench|HouseColourProof|Settings|Skirmish|CatalogAndTextTests": 436 passed, 2 skipped, 0 failed.
- Sim.Tests filter "CarrierBalanceProbe|VisionPerformanceTests|S13CampaignBalanceTests.Ashthrower": 3 passed, 2 skipped (the probes, with their reason), 0 failed.
- Llm.Tests (whole project): 152 passed.
- Tools.Tests (whole project): 22 passed. The AI timeline tests print console errors by design.
- Fail-on-old-code: done for items 1, 3 to 10 and 12, and checked by a mutation run on a copy of the old behaviour (31 expected failures). Item 2 is the weak one, as noted above.
- I did not run the full suite or the CI yml itself.

NEW FINDINGS
- `GameFlow` is now `public static` (needed so the guard test can call Restart).
- ROADMAP M12-12b is now fixed for the `ai timeline` half. The Glass Coast Expedition start skew half stays open. I did not edit docs.
- The README says M8 is done, but ROADMAP still lists M8-18 (voiced lines) as open. I wrote that.
- SfxrTests failing on CI without Python is unverified on the Windows runner. If Python is not on PATH there as `python3` or `python`, CI goes red. Please check the first CI run.
- MenuPanel's old shrink floor (0.5 of the requested scale) could still overflow at 200%/XL on some row counts. I changed it to 0.3.

QUESTIONS FOR THE OWNER
1. Old single LLM key: I trust it only if the saved provider is Anthropic. Otherwise it is deleted and the key must be re-entered. Alternative: keep it for the saved provider whatever that is. I recommend what I did, because an OpenAI-compatible key could be sent to a custom host.
2. Spectate: when no key exists, picking a commander slot silently switches the match to Spectate. Options: (a) keep, (b) show a Notice first, (c) block Start. I recommend (b).
3. Hide test_32 and test_64 from the skirmish map list? There is no dev-map flag today. Option: a `dev = true` key in the map TOML (a data change, so yours to decide).
4. Editor validation: show validator issues only after the first edit (UX-LM-14)? I recommend yes, but I left it.
5. Merge note for the lead: fix-ui's collection for GameKeys.Active and my "UiStatics" collection are different collections. UiStatics has DisableParallelization, so it runs alone. No file overlap that I know of, except possibly the attribute line in UiScaleRuleTests / FxLookTests if fix-ui touched them.

USER CHECK
- Main menu: Settings, UI scale 200%, Text XL, window 1280x720. The footer and tagline no longer overprint the entries.
- Skirmish setup at 200%/XL: the slot rows keep a fixed label column with "...", the colour swatch has its own column, and the panel does not change width when you cycle a slot.
- About and How to play: the "Back" label sits inside its button. List and help screens: the hint and detail rows cannot be selected.
- Map editor: validation and status lines are cut with "..." instead of running off the panel at large text.
- Load a corrupt save or restart a failed mission: a Notice appears instead of a crash.
- Settings, Commander: switch provider between Anthropic and OpenAI. Each keeps its own key. An http base URL to a remote host refuses to send a key, and localhost still works. The Test connection button obeys the same rule.
- Start a skirmish on a hand-made map that has validator errors: the Notice "This map cannot be played" appears.
- About: scroll through the credits. Skorpio, Sharm, LPC authors, Kenney and 0 A.D. (CC BY-SA 3.0) are listed in full, in a published build too.
- Console on starting the setup screen: one "map list: ... not offered" line if any map files were dropped.
- Tools: `ai timeline --faction-a expedition` now shows a Command Ship, landing craft and depot, the same as `ai match`.