# AOEMods.Essence: A C# Library, CLI and Editor for Age of Empires IV File Formats

**Summary**

- AOEMods.Essence is a community C# (.NET 6) library that reads, and partly writes, the file formats of Age of Empires IV's "Essence" engine: SGA archives, Relic Chunky containers, RGD game-data files, RRTex textures, RRGeom meshes and RRMaterial materials.
- It is a file-format toolkit, not an engine. There is no simulation, rendering, or unit logic in the repo. Its value to us is as a worked example of how a modern RTS packages and describes its data.
- The game-data model is a generic keyed tree (RGD). Nodes have a name, a type tag and a value, and a value can be a list of further nodes. The repo contains no inheritance or override logic; any "unit inherits from base unit" behaviour lives in the game, which this code does not show.
- The repo has no real game data. Its only test archive holds a single file, `hello.txt`, so the unit-data example in section 2 is schematic, not taken from AoE4.
- Licence is MIT (2021, AOE Mods). Code under MIT can be included in a GPL v3 project, so porting is legally clean, subject to the usual attribution.
- Reusable for us: the separation of library, CLI and tests, the keyed-tree idea, and the packing/unpacking workflow. The binary formats themselves are not useful to us, because we will author our own data.

## 1. What It Is, Status and Licence

The README (lines 1-28) describes a library for "working with Age of Empires 4's Essence engine". It lists these formats and capabilities:

| Format | Read | Write / convert | Notes |
|---|---|---|---|
| SGA archive | yes | pack (write), unpack | Relic's archive, the game's package format |
| RRTex texture | yes | to PNG, JPG, BMP, TGA, GIF | Block-compressed (BC1, BC3, BC7) mips |
| RRGeom mesh | yes | to OBJ, glTF/GLB | Positions, normals, UVs, faces |
| RGD game data | yes | XML, JSON; write from XML | The data-model file, see section 2 |
| Other chunky (RRMaterial, etc.) | yes | none | Editor offers hex view |

Status: the project has a CI workflow (`.github/workflows/dotnet.yml`, .NET 6 on ubuntu-latest, runs the library and CLI test projects) and a SonarCloud workflow. The latest commit in the local clone is "Added support for decoding Brotli compression used in new formats (#7)". The tests are small: 13 xUnit facts across `AOEMods.Essence.Test` (`ArchiveReaderTest.cs`, `ChunkyFileReaderTest.cs`, `ChunkyFileTest.cs`) and one in `AOEMods.Essence.CLI.Test/TestCommands.cs:9-50`, which packs a folder and unpacks it again. Coverage of the RGD, RRTex and RRGeom readers is effectively nil. I did not run the tests.

Licence: `LICENSE` is MIT, "Copyright (c) 2021 AOE Mods". It is permissive and compatible with GPL v3.

Dependencies (from the csproj files): BCnEncoder.Net 2.1.0 (BC texture decode), SharpGLTF.Toolkit (glTF export), CommandLineParser 2.8.0 (CLI), Microsoft.Toolkit.Mvvm 7.1.2, Ookii.Dialogs.Wpf and WPFHexaEditor (editor). Their licences are unverified here.

## 2. The Data Model: RGD Keyed Trees

RGD files carry the game's tunable data. The library's reader (`AOEMods.Essence/Chunky/RGD/RGDReader.cs:17-65`) shows the layout.

**Container.** An RGD file is a Relic Chunky file: a 24-byte header (16-byte magic `"Relic Chunky\r\n\x1A\0"` from `Chunky/Core/ChunkyConstants.cs:11`, then version and platform, `ChunkyFileReader.cs:102-109`). It must contain exactly one `DATA KEYS` chunk and exactly one `DATA AEGD` chunk (`RGDReader.cs:24-45`).

**KEYS chunk.** A table of `(UInt64 id, string name)` pairs (`RGDReader.cs:128-147`). This is the dictionary from numeric ids to human-readable names.

**AEGD chunk.** A single table of `(UInt64 key, int type, int offset)` rows. The values sit after the table, and each row points to its value by offset (`RGDReader.cs:91-116`). Lists use the same table layout recursively. The type enum (`RGDDataType.cs:33-59`) is:

- `Float` = 0, `Int` = 1, `Boolean` = 2, `CString` = 3
- `List` = 100 and `List2` = 101, both nested tables

Reading resolves every id through the KEYS dictionary (`RGDReader.cs:50-64`), so the in-memory result is a plain tree of `RGDNode(Key: string, Value: object)`. A node's value is either a scalar or an array of child nodes.

**Illustrative XML.** `GameDataXmlUtil.cs:18-72` writes the tree as XML, using a `Type` attribute that is the enum name. The fragment below is invented to show the shape. It is not AoE4 data, and the key names are placeholders:

```xml
<Root>
  <RGDNode Key="ExampleUnit" Type="List">
    <RGDNode Key="Health" Type="Float" Value="120" />
    <RGDNode Key="Armor" Type="Int" Value="2" />
    <RGDNode Key="Weapon" Type="List">
      <RGDNode Key="Damage" Type="Float" Value="10" />
      <RGDNode Key="Range" Type="Float" Value="5" />
    </RGDNode>
  </RGDNode>
</Root>
```

Points that matter for us:

- The format is generic. Nothing in the reader knows what a unit is. Meaning comes from the key names, which the game interprets.
- The representation is a nested key-value tree, not a fixed struct per unit type. Adding a stat means adding a key, not changing a binary layout.
- The XML and JSON forms are the practical editing surface. The binary RGD is compiled from them with `rgd-encode`.
- **No inheritance or overrides.** I searched the whole tree for inherit, parent, override and bag and found nothing in the data layer (the only "Parent" hits are in the SGA graph code). Any "base unit plus overrides" rule is either in the game engine or in other tooling not in this repo. Unverified which.
- **Hash-based key ids, and a caveat.** When writing, `RGDWriter.cs:25-28` builds each id as `(ulong)((long)node.Key.GetHashCode() + int.MaxValue)`. Two problems follow from the code, both observable without running the game:
  1. .NET's `string.GetHashCode()` is randomised per process on .NET Core, so the same XML can produce different ids on different runs. The KEYS chunk still maps names to those ids, so the library can read its own output back.
  2. Name collisions are not detected. Two names with the same hash silently share a dictionary slot (`RGDWriter.cs:30`, `RGDWriter.cs:140`).
  Whether the game checks ids against its own hash function is unverified. The repo does not show how Relic computes ids.
- **Doc-comment mismatch.** `RGDDataType.cs:44` says Boolean is a "32 bit boolean", but reader and writer both use `BinaryReader.ReadBoolean` and `BinaryWriter.Write(bool)`, which are one byte. The two sides agree with each other, so the code is consistent. Whether the game's real format is one byte is unverified.

## 3. Archive and Asset Formats

**SGA archive.** The layout is read in `AOEMods.Essence/SGA/Core/ArchiveReader.cs:86-128` (header) and `:134-182` (entries).

- Header: 8-byte magic (expected `_ARCHIVE`), version, product id, a 64-character UTF-16 "nice name", then offsets and lengths for a header blob and a data blob. The header blob contains TOC, folder, file and string tables at offsets relative to itself. The header also carries a 256-byte signature (`ArchiveReader.cs:100`), which the code reads but does not verify. The source comment says the game validates it by XOR-folding 16-byte blocks.
- TOC entries: alias and name (64 characters each) plus folder and file index ranges and a root folder index. Folders and files are flat arrays with start and end indexes into them, so the tree is reconstructed from index ranges.
- File entries: name offset, hash offset, data offset (64-bit), compressed and uncompressed length, a verification-type byte, a storage-type byte, and a CRC32.
- Storage types: `Store`, `StreamCompress` and `BufferCompress` (Deflate), and `StreamCompressBrotli` and `BufferCompressBrotli` (Brotli). Decompression is in `AOEMods.Essence/SGA/Graph/ArchiveFileNode.cs:58-83`.
- The writer is minimal. `ArchiveWriterHelper.cs:52-58` writes every file as `Store` with verification `None`, hash offset 0 and CRC 0. Nothing in the repo computes a CRC. So packed archives are uncompressed and unverified, which the game may or may not accept. Unverified.

**Chunky container.** Most of the other formats are Relic Chunky. Chunks have a 4-byte type (`DATA` or `FOLD`), a 4-byte name, a version, a length, and a path (`ChunkyFileReader.cs:85-95`). Folders nest by path. `ReadChunkHeadersImpl` (`:51-70`) stops at any type that is not DATA or FOLD.

**RRTex textures.** The reader (`Chunky/RRTex/RRTexReader.cs`) navigates TSET, TXTR and DXTC folders to find TMAN (metadata) and TDAT (data) chunks. TMAN holds width, height, a compression code, mip count, and per-mip compressed and uncompressed sizes (`RRTexDataTman.cs:17-20`). The codes map to BC formats: 18 BC1 with alpha, 19 BC1, 22 BC3, 28 BC7, and 2 raw R (`RRTexReader.cs:195-200`). Several header fields are still named `Unknown1` to `Unknown4`. Mip data is zlib-compressed (the reader skips a 2-byte zlib header, `:176`). `RRTexType` (`RRTexType.cs:6-27`) tells the decoder how to read channels: NormalMap, Metal, Pattern or Generic.

**RRGeom meshes.** Geometry object (GEOM) folders contain header chunks (GOHD), a vertex buffer (GEOB with positions, normals, UVs) and an index buffer (GEOB again, as faces). They are grouped in levels of detail (LOD) under MESH folders (`Chunky/RRGeom/RRGeomReader.cs:17-63`). Some fields are unnamed.

**RRMaterial.** Materials are a folder of "Pb" (presumably a property block) entries whose PbTe chunk holds a count and then length-prefixed key-value texture paths (`Chunky/RRMaterial/RRMaterialReader.cs:17-60`). So a material is a set of texture slot names pointing at texture files in other archives.

**What this says about a modern RTS pipeline.** Source art and data are converted to compressed runtime formats (BC textures, mesh buffers). Game data is a binary keyed tree compiled from editable text. Assets are packed into archives that reference one another by path. The editor's "export with materials" note (README line 35-37) shows that a model depends on archives opened elsewhere. This is a conventional asset pipeline. Nothing in it is specific to our needs.

## 4. Architecture of the Tool

The solution (`AOEMods.Essence.sln`) has five projects:

- **`AOEMods.Essence`** (net6.0, library, about 2,950 lines in the format folders). Layered: `Chunky/Core` (raw chunk reading and writing), `Chunky/<Format>` (one folder per format, each with a reader and writer), `Chunky/Graph` (a node tree over chunky files), `SGA/Core` and `SGA/Graph` (the same split for archives). `FormatReader` and `FormatWriter` (`Chunky/FormatReader.cs:12-58`) are thin static facades so callers do not pick the reader class themselves.
- **`AOEMods.Essence.CLI`** (net6.0, console). Uses CommandLineParser verbs, one options class per command (`RGDEncodeOptions.cs`), dispatched in `Program.cs:9-19`. Commands live in `Commands.cs` (343 lines), each returning an exit code. Batch mode is a glob (`BatchConvert`, `Commands.cs:58`). One help-text bug: `RGDEncodeOptions.cs:15` says batch mode converts ".json" files, but the code globs `**/*.xml` (`Commands.cs:149-176`).
- **`AOEMods.Essence.Editor`** (net6.0-windows, WPF, about 2,400 lines of C# plus XAML). MVVM with Microsoft.Toolkit.Mvvm `RelayCommand`s (`GameDataViewModel.cs:14-41`). It opens SGA archives, folders and single files; mass-exports and converts; edits and repacks archives; shows RGD as a tree with JSON, XML and RGD export (`GameDataViewModel.cs:43-80`); and includes a hex view for chunky files.
- **Two test projects** (xUnit): `AOEMods.Essence.Test` and `AOEMods.Essence.CLI.Test`.

Design choices worth noting. The library is UI-free and the editor depends on it, not the other way round. The CLI reuses the same library calls, so the two cannot drift apart. Reading is stream-based and lazy: archive nodes wrap a stream and a position, and bytes are read only when asked for (`ArchiveFileNode.cs`). That matters for large archives.

## 5. What Is Reusable for RTS_2027

Honest answer: little of the binary code, and some of the structure.

- **Not reusable:** the Essence binary formats. We will define our own data, and we have no need to read AoE4 files. Porting the BC texture, glTF and RRGeom code would only matter if we wanted to read Relic assets, which our licence and asset rules forbid for art anyway.
- **Reusable as a pattern:** the keyed-tree data model. A unit, weapon or upgrade becomes a named node with typed fields and child lists. This is the same idea as the data-driven "bags" we want, without the id hashing. We should use readable string ids and our own loader, not hashed ids.
- **Reusable as a pattern:** a small CLI for pack, unpack and convert operations. We may need one for map or data packs. It can be a thin wrapper over the library, as here.
- **Reusable as a pattern:** the project layout (library, CLI, tests, one facade per format) and the CI setup. If we choose C#, this layout is a sound starting point. The editor's WPF approach is viable because we are Windows-only, but a map or data editor is a large separate project and should be scoped later.
- **Code style:** XML doc comments on public types, records for immutable file headers, and `static` readers. Consistent and easy to follow.

## 6. Comparison: Four Generations of RTS Data Design

The first two rows use general knowledge, not this repo, and are unverified here. The AoE4 row is taken from this repo's code.

| Game or engine | How unit data is stored | Extension model | Source of this row |
|---|---|---|---|
| C&C (Tiberian Dawn, Red Alert) | Mostly compiled into the executable as C tables, with an INI text file (RULES.INI) for many values | Edit the INI; code changes need recompiling | General knowledge, unverified here; other helpers cover the C&C source |
| AoE2 | Binary `.dat` file of records and effects | Patch effects inside the binary file | General knowledge, unverified here |
| AoE4 (Essence) | Binary RGD keyed tree, compiled from XML or JSON | Key names are the schema, no inheritance in this library | This repo, `Chunky/RGD/*.cs` |
| openage (nyan) | Text-based object definitions with inheritance and patches | Patches and inheritance in the data language | General knowledge, unverified here; another helper covers nyan |

Trend: data moves from compiled code, to binary tables, to text trees with explicit inheritance. The AoE4 approach keeps the tree-of-keys idea but drops the binary-patch model in favour of XML or JSON as the editing surface.

## Implications for RTS_2027

- **Recommendation:** store unit, weapon, tech and upgrade data as human-readable text (JSON or TOML) with string ids. Do not adopt RGD or hashed ids. Hashes block review, merging and diffing, and the RGD writer here shows the collision and per-process randomness problems.
- **Recommendation:** design the data as a keyed tree (named nodes with typed fields and child lists), as in RGD. It maps well to units with nested weapons and to tech trees.
- **Trade-off:** a tree needs a separate, explicit inheritance step for "base unit plus overrides". This library does not supply one, so we would design and test it ourselves. Benefit: we control the rules. Cost: more to build.
- **Trade-off:** a text format is easy to edit and diff but slower to parse than a binary one. For a few thousand records this does not matter, and loading can be cached at startup.
- **Recommendation:** use plain folders for data and maps in development, and add a packed archive (zip is enough) only at release. The SGA format is undocumented outside this code and the game's own use. Unverified whether the game accepts our archives, and we do not need to match it.
- **Recommendation:** if we use C#, copy the project layout and the CLI-over-library pattern. Keep the library free of UI code so the tests and any tool can use it.
- **Trade-off:** WPF is a viable editor stack on Windows, but it ties the editor to Windows and to .NET. Given the Windows-only target, this is acceptable, but a data editor is a separate scope and can wait.
- **Note on licence:** MIT code can be copied into our GPL v3 project with attribution. Check the licences of its dependencies (BCnEncoder, SharpGLTF, WPFHexaEditor) separately before copying anything that uses them.
- **Caution:** the Brotli and Deflate handling and the BC texture decoding here are useful references if we ever read a third-party archive, but they add no value to our own data pipeline.
- **Caution on tests:** the repo's coverage is thin, with one synthetic archive. If we borrow any code, write our own tests for it, do not rely on this suite.

## Open Questions for the Owner

1. **Data language:** which format do we want for unit and tech data? Options: (a) JSON, widely supported, verbose; (b) TOML, readable, weaker for deep nesting; (c) a custom text format with inheritance built in, more work up front. This decides how much of section 2 we reuse.
2. **Inheritance rules:** how deep should unit inheritance go? Options: (a) one level (a base unit with overrides), simple and easy to reason about; (b) a full chain with multiple parents, flexible but harder to debug.
3. **Distribution:** do we need a packed archive for releases, or are plain folders enough? Options: (a) folders only; (b) a zip file; (c) a custom archive, which the SGA reference could inform but is not needed for us.
4. **Tooling scope:** do we want a data or map editor in the first release? Options: (a) none, edit text files by hand; (b) a small CLI for validating and packing data; (c) a full WPF editor, a large separate project.

## Sources

Code in the local clone at `/home/claude/ref/AOEMods.Essence` (read-only, MIT). Paths are relative to that root.

- `README.md:1-72` (formats, CLI, library structure)
- `LICENSE:1-5` (MIT, Copyright 2021 AOE Mods)
- `AOEMods.Essence/Chunky/Core/ChunkyConstants.cs:6-12`, `ChunkyFileReader.cs:51-109`, `ChunkyFileHeader.cs`, `ChunkHeader.cs`
- `AOEMods.Essence/Chunky/RGD/RGDReader.cs:17-148`, `RGDWriter.cs:16-148`, `RGDDataType.cs:33-59`, `RGDNode.cs`, `RGDList.cs`, `KeyValueDataChunk.cs`, `KeysDataChunk.cs`
- `AOEMods.Essence/Chunky/RGD/GameDataXmlUtil.cs:18-126`
- `AOEMods.Essence/SGA/Core/ArchiveReader.cs:86-216`, `ArchiveHeader.cs`, `ArchiveTocEntry.cs`, `ArchiveFileEntry.cs`, `FileStorageType.cs`, `FileVerificationType.cs`
- `AOEMods.Essence/SGA/Graph/ArchiveFileNode.cs:58-83`, `ArchiveWriterHelper.cs:40-66`
- `AOEMods.Essence/Chunky/RRTex/RRTexReader.cs:26-200`, `RRTexDataTman.cs:1-20`, `RRTexType.cs:1-27`
- `AOEMods.Essence/Chunky/RRGeom/RRGeomReader.cs:17-63`
- `AOEMods.Essence/Chunky/RRMaterial/RRMaterialReader.cs:17-60`
- `AOEMods.Essence/Chunky/FormatReader.cs:12-58`
- `AOEMods.Essence.CLI/Program.cs:1-19`, `Commands.cs:18-30`, `:58`, `:149-176`, `RGDEncodeOptions.cs:6-20`
- `AOEMods.Essence.Editor/GameDataViewModel.cs:14-80`
- Projects: `AOEMods.Essence/AOEMods.Essence.csproj`, `AOEMods.Essence.Editor/AOEMods.Essence.Editor.csproj` (net6.0-windows, UseWPF)
- Tests: `AOEMods.Essence.Test/ArchiveReaderTest.cs:15-87`, `ChunkyFileReaderTest.cs:10-40`, `AOEMods.Essence.CLI.Test/TestCommands.cs:9-50`, `AOEMods.Essence.Test/TestFiles/TestArchive.sga` (contains only `hello.txt`)
- CI: `.github/workflows/dotnet.yml`
- Git: latest commit `2bb952f` in the clone's history ("Added support for decoding Brotli compression used in new formats (#7)")

Not read: the editor's XAML and most view code, the `ChunkyFileWriter` body beyond its header writes, and the `Chunky/Graph` and `SGA/Graph` internals beyond what is cited. No external web sources were used. The statements about C&C, AoE2 and openage in section 6 are general knowledge and unverified in this session.
