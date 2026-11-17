# Field Manual full build — shared notes for helpers (session 32, M14-05)

Read this first. Design and page plan: `docs/design/19_field_manual.md` §1-§3 (read §3 for your part's page budget). The approved
pilot look is `tools/booklet/pilot/build.py` (read only the parts your brief names) and `tools/booklet/pilot/RTS_2027_Field_Manual_pilot.pdf`.

## Layout
- `tools/booklet/full/common.py` (lead): `Page`, `FACTIONS`, `FACTION_ORDER`, `img()`, `hero()`, `rules()`, `techtree()`, `archive()`,
  `title()`, `paras()`, `E`. Read its docstrings; never edit it (ask the lead in your reply instead).
- `tools/booklet/full/style.css` (lead): the pilot CSS plus the full-build additions. Never edit it. Put page-specific CSS in a
  `<style>` block at the top of the first page body your module returns (prefix your classes with your module name, e.g. `.p2-`).
- `tools/booklet/full/book.py` (lead): assembles parts in order, adds folios, the contents page, notes pages and the PDF.
- Your module: `tools/booklet/full/<module>.py` exposing `pages() -> list[Page]`. One module (plus at most one data file
  `tools/booklet/full/<module>_text.toml` for the prose, and images under `tools/booklet/full/img/<module>_*`) per helper.

## Setup in your worktree (once)
    git lfs pull --include "art/booklet/**,data/images/stills/**,data/sprites/**"
    python3 tools/booklet/data/resolve_rules.py && python3 tools/booklet/data/techtree.py
## Build and look at your part
    python3 tools/booklet/full/book.py <module>            # -> tools/booklet/full/out/part_<module>.pdf
    pdftoppm -r 50 -png tools/booklet/full/out/part_<module>.pdf /tmp/claude-0/<module>/p   # then Read the PNGs and check every page
A page is 5.5 x 8.5 in; text must not overflow the bottom (the page clips silently). Check every page image.

## Content rules (hard)
- Names: display names come from the Archive (`title(id)`), never from ids. Numbers (cost, health, range, damage, build time) come
  from `rules()`; never type a number by hand. Speeds: leptons per tick * 20 / 256 = cells per second.
- Approved text: the Archive entries, `docs/lore/*.md`, and design 19 §6, §6b, §6c, §6d (lore entries, use verbatim). Anything you
  write yourself is NEW TEXT: put it on pages with `draft=True` (the owner reviews one full draft PDF).
- Spoilers: premise and one-line teasers only. No mission endings, no deaths, no twists. Never name Order Seven, the Director's
  death, or how any campaign ends.
- No "Command & Conquer" name, no EA trademarks, no real people. The game's name is RTS_2027; the tagline is "Ashfall · The War for the Surface".
- Facts about how the game plays (keys, menus, rules) must be read from the code or data you were pointed at; if you cannot verify
  a fact, leave it out and list it under QUESTIONS.
- Hyphens and dashes: plain English, short sentences, no em dashes in new text.

## Forbidden
Pushing, scheduling tools, `mcp__remote-devices__*` tools, editing files you do not own, network calls (other than git lfs pull),
building the .NET solution (not needed), deleting files you did not create. Scratch files only under `/tmp/claude-0/<module>/`.

## Finish
Commit only your files: `docs(booklet): <summary> [M14-05]` with your own session's attribution. Reply in the format your brief gives.
