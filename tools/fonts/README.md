# UI font atlases

The game draws UI text from bitmap atlases generated from Noto Sans (SIL Open Font License 1.1, see
`data/fonts/OFL.txt` and `docs/CREDITS.md`). The generated files are committed, so the game and the tests do not need
Python or the TTF files at run time.

## Files

| File | Contents |
|---|---|
| `data/fonts/NotoSans-Regular.ttf`, `NotoSans-Bold.ttf` | The source faces, unmodified (from Debian `fonts-noto-core` 20201225-2). |
| `data/fonts/noto-sans-<weight>_<size>.png` | Atlas: white glyphs, straight alpha (RGB 255, A = coverage), 16 columns, one grid cell per glyph. |
| `data/fonts/noto-sans-<weight>_<size>.font.toml` | Metrics: `line_height`, `baseline`, `[[glyph]]` (`codepoint`, `x`, `y`, `w`, `h`, `xoffset`, `yoffset`, `advance`), `[[kerning]]` (`left`, `right`, `amount`). |

Sizes are 9, 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 40, 48, 56 and 64 px (M11-10h: the UI scale 75 to 200 % and the Text size setting pick the nearest) (`FontLibrary.Sizes` in `src/Game/Ui/Fonts/FontLibrary.cs` must match the build). Glyphs cover
U+0020..U+00FF except the C1 controls U+007F..U+009F, plus U+2013, U+2014, U+2018, U+2019, U+201C, U+201D, U+2022 and
U+2026. Any other character draws as '?'.

The game loads the metrics without a graphics device, so text can be measured before drawing. The first draw loads the PNG
and premultiplies it, which suits the default `SpriteBatch` alpha blend.

## Regenerate

Requirements: Python 3.11 or later, Pillow built with raqm (`python -c "from PIL import features; print(features.check('raqm'))"`
prints `True`). Pillow alone is needed; no network access.

```
python tools/fonts/build_atlas.py                 # all faces, sizes 9 10 12 14 16 18 20 24 28 32 36 40 48 56 64, written to data/fonts
python tools/fonts/build_atlas.py --sizes 12 16   # a subset
```

Kerning and ligatures come from Pillow's raqm layout. Ligatures are off (`-liga -clig -calt -dlig`) because the game draws
each character alone. Output is deterministic for a given Pillow and FreeType version: no timestamps, fixed glyph order,
integer metrics.

If you change the sizes, update `FontLibrary.Sizes` in the same change; `EveryGeneratedMetricsFileParses` in
`tests/Game.Tests/FontAtlasTests.cs` fails when a file is missing.

## Check

```
dotnet test tests/Game.Tests -m:1 --filter "FullyQualifiedName~FontAtlas"
python tools/fonts/proof_render.py               # writes scratch/font_proof.png and prints its path
```

The proof draws `RTS_2027 — Authority Refinery 1,250 ¢ äöü` at three sizes from the metrics alone. Look at it to check
glyph placement, kerning and the accented letters.

## Fallback

If the metrics or the PNG are missing or invalid, `BitmapFont` falls back to the embedded 5x7 pixel font. Its drawing and
measuring code is unchanged, so the game never fails because of a font. `FontLibrary.LastError` says why.
