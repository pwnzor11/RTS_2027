# Licensing

## Our license
RTS_2027 is licensed under the **GNU GPL v3** (see `LICENSE` at the repo root). The owner's stated
intent: this game is open source forever. GPL v3 was chosen because it is the license of the
reference source we may reuse (below), so reuse carries no license conflict.

## Linking exception for MonoGame (D-055)
MonoGame is licensed under the Ms-PL (with MIT for some portions), which the FSF classes as
GPL-incompatible. The top of `LICENSE` therefore carries a GPL v3 §7 additional permission allowing
RTS_2027 to be combined with and conveyed alongside MonoGame and its bundled native libraries. We can
grant it because every line of code here is ours (D-050). Not legal advice.

## Reference source: study only, never copy (D-050)
Because of the linking exception above, **no EA code may be copied or ported into this repo**: we cannot
add a permission to EA's code. The EA repo below is a read-only reference for understanding mechanics;
everything here is written fresh. The rules that follow are kept for the record and would only matter
if D-050 were ever reversed.
**EA, "Command & Conquer Remastered Collection" source** —
https://github.com/electronicarts/CnC_Remastered_Collection — GPL v3 with EA additional terms
(GPL §7). What the additional terms require of us:

1. **No trademarks.** We get no rights to "Command & Conquer", "Tiberian Dawn", "Red Alert", "GDI",
   "Nod", "Tiberium", "Westwood", "EA" or any EA mark. Our game, factions, resources and units need
   their own names. We never claim affiliation with EA.
2. **Keep the notice.** Any file containing EA code keeps EA's copyright header and a pointer to
   these terms.
3. **Mark modifications.** Any EA-derived file carries a line such as
   `// Modified from the C&C Remastered Collection source (EA, GPL v3). See docs/LICENSING.md.`
4. **No misrepresentation.** We never present our code as the original program.
5. **Indemnity** clause applies only if we assume contractual liability to recipients; we don't.

The EA repo contains **code only**: no art, audio, music, video, maps or data files. Those ship with
the paid game and are not licensed to us. Everything visual and audible in RTS_2027 must be original
or from a compatible open license (CC0, CC-BY, CC-BY-SA, OFL for fonts), with attribution recorded in
`docs/CREDITS.md`.

## Register of EA-derived files
Every file in this repo that copies or ports EA code is listed here. Keep this current.

| Our file | Derived from | What was taken | Notes |
|---|---|---|---|
| (none — forbidden by D-050) | | | |

## Third-party assets and libraries
Recorded in `docs/CREDITS.md` once we have any.
