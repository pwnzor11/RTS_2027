# s23d laptop series read (session 24)

Laptop, commit 26a12e84 (Rocket Truck 950 D-239, Grapeshot D-236/237, endgame fixes D-235), 64 games per row, tier 3, size 96, 30k ticks.
Compared with s23after (commit 40cb4dfd). A share = A wins / decisive games. Target 45-55 %.

| Match (A v B) | Biome | s23after A-B-D | s23d A-B-D | A share before -> after |
|---|---|---|---|---|
| Authority v Directorate | rust_belt | 16-37-11 | 21-33-10 | 30 % -> 39 % |
| Authority v Left Behind | rust_belt | 40-15-9 | 42-16-6 | 73 % -> 72 % |
| Authority v Expedition | rust_belt | 31-31-2 | 25-37-2 | 50 % -> 40 % |
| Directorate v Left Behind | rust_belt | 31-23-10 | 28-26-10 | 57 % -> 52 % |
| Directorate v Expedition | rust_belt | 55-8-1 | 28-29-7 | 87 % -> 49 % |
| Left Behind v Expedition | rust_belt | 40-18-6 | 21-39-4 | 69 % -> 35 % |
| Expedition v Authority | glass_coast | 34-27-3 | 43-20-1 | 56 % -> 68 % |
| Expedition v Directorate | glass_coast | 26-34-4 | 39-20-5 | 43 % -> 66 % |
| Expedition v Left Behind | glass_coast | 35-13-16 | 50-5-9 | 73 % -> 91 % |

- **Rocket Truck 950 (D-239) worked:** Directorate v Expedition 87 % -> 49 %, v Left Behind 52 %; Authority v Directorate still 39 % (closer).
- **Grapeshot overshot:** every Expedition row moved 10-35 points toward Expedition; worst against Left Behind (infantry-heavy). Damage 14 -> 11 (D-244).
- **Left Behind v Authority 72 %** is unchanged from before Grapeshot: a separate weakness, left for after the Grapeshot fix (owner, s24).
- **Draws** 162 -> 140 of 1088 games (14.9 % -> 12.9 %); Expedition mirrors still draw 10-13 of 64.
- **Start share:** Expedition mirror Rust Belt 71 % -> 59 %, but Ash Plains 61 % -> 74 % and Glass Coast 55 % -> 65 %; Authority v Expedition 68 %.
- **Left Behind mirror slot bias:** side A wins 34-16 (layout 1) and 36-24 (layout 2) in an identical-AI mirror with start swaps (also in s23after:
  35-19, 36-24), so it is not the corner. Something favours player slot 0 for Left Behind; worth a look.
