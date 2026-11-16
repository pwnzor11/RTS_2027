Stamped data maps (M12-14). The three campaign maps are the pre-rework files in tools/maps/base/ plus a plan in tools/maps/plans/.
Rebuild (output is byte-identical every time):
  dotnet run --project src/Tools -- map stamp --base tools/maps/base/<id>.map.toml --plan tools/maps/plans/<id>.stamps.toml --out data/maps/<id>.map.toml
Plan keys: symmetry, [[keep]] rect, [[stamp]] id x y rot mirror, [[patch]] land rect, [[prop]] overlay x y density. See src/Tools/MapStampCommand.cs.
