using Rts.Sim.Core;

namespace Rts.Sim.Serialization;

/// <summary>
/// The identity of a game, written at the start of every replay and save (M1.5, review A-2/A-3/A-6): build string,
/// seed, player count, teams, store limits, AllowDebug, rules hash, map hash and name, and the rules source files
/// (name, text) the game was started with, stored deflate-compressed. Lets a save or replay be understood without
/// the caller supplying the rules, and lets a different build refuse it with a clear message.
/// </summary>
public sealed record SetupHeader(
    string Build, ulong Seed, int PlayerCount, IReadOnlyList<byte>? Teams, SimLimits Limits, bool AllowDebug,
    ulong RulesHash, ulong MapHash, int MapWidth, int MapHeight, string MapName,
    IReadOnlyList<(string Name, string Text)> RulesFiles)
{
    private const int MaxLimit = 1 << 20;

    /// <summary>Header for a running game.</summary>
    public static SetupHeader From(Simulation sim, string build, IReadOnlyList<(string Name, string Text)> rulesFiles)
    {
        SimSetup s = sim.Setup;
        return new SetupHeader(build, s.Seed, s.PlayerCount, s.Teams, s.Limits, s.AllowDebug, s.RulesHash, sim.MapHash,
            s.Map.Width, s.Map.Height, s.Map.Name, rulesFiles);
    }

    /// <summary>Writes the header fields in their fixed order.</summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Build);
        w.Write(Seed);
        w.Write(PlayerCount);
        if (Teams is null) w.Write((byte)0);
        else
        {
            w.Write((byte)1);
            w.Write(Teams.Count);
            for (int i = 0; i < Teams.Count; i++) w.Write(Teams[i]);
        }
        w.Write(Limits.Vehicles); w.Write(Limits.Infantry); w.Write(Limits.Buildings); w.Write(Limits.Projectiles);
        w.Write(AllowDebug);
        w.Write(RulesHash);
        w.Write(MapHash);
        w.Write(MapWidth); w.Write(MapHeight);
        w.Write(MapName);
        RulesBlob.Write(w, RulesFiles);
    }

    /// <summary>Reads what <see cref="Write"/> wrote. Throws InvalidDataException on a malformed or truncated header.</summary>
    public static SetupHeader Read(BinaryReader r)
    {
        try
        {
            string build = r.ReadString();
            ulong seed = r.ReadUInt64();
            int players = r.ReadInt32();
            if (players < 1 || players > byte.MaxValue) throw new InvalidDataException($"Bad player count {players}.");
            byte[]? teams = null;
            byte hasTeams = r.ReadByte();
            if (hasTeams == 1)
            {
                int n = r.ReadInt32();
                if (n != players) throw new InvalidDataException("Teams count does not match the player count.");
                teams = r.ReadBytes(n);
                if (teams.Length != n) throw new InvalidDataException("Truncated header.");
            }
            else if (hasTeams != 0) throw new InvalidDataException($"Bad teams flag {hasTeams}.");
            var limits = new SimLimits(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            if (limits.Vehicles < 2 || limits.Infantry < 2 || limits.Buildings < 2 || limits.Projectiles < 2
                || limits.Vehicles > MaxLimit || limits.Infantry > MaxLimit || limits.Buildings > MaxLimit || limits.Projectiles > MaxLimit)
                throw new InvalidDataException("Bad store limits.");
            bool allowDebug = r.ReadBoolean();
            ulong rulesHash = r.ReadUInt64(), mapHash = r.ReadUInt64();
            int w = r.ReadInt32(), h = r.ReadInt32();
            string mapName = r.ReadString();
            var files = RulesBlob.Read(r);
            return new SetupHeader(build, seed, players, teams, limits, allowDebug, rulesHash, mapHash, w, h, mapName, files);
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Truncated header.");
        }
    }

    /// <summary>
    /// Throws InvalidDataException naming the first way <paramref name="setup"/> (with map hash
    /// <paramref name="mapHash"/>) differs from this header: rules hash, map hash, seed (when
    /// <paramref name="checkSeed"/>), map size, player count, teams, limits or AllowDebug.
    /// </summary>
    public void CheckMatches(SimSetup setup, ulong mapHash, string what, bool checkSeed)
    {
        if (RulesHash != setup.RulesHash)
            throw new InvalidDataException($"Rules hash mismatch: {what} has 0x{RulesHash:x16}, setup has 0x{setup.RulesHash:x16}.");
        if (MapHash != mapHash)
            throw new InvalidDataException($"Map hash mismatch: {what} has 0x{MapHash:x16}, setup map is 0x{mapHash:x16}.");
        if (checkSeed && Seed != setup.Seed)
            throw new InvalidDataException($"{what} seed {Seed} does not match setup seed {setup.Seed}.");
        if (MapWidth != setup.Map.Width || MapHeight != setup.Map.Height)
            throw new InvalidDataException($"{what} map size {MapWidth}x{MapHeight} does not match setup {setup.Map.Width}x{setup.Map.Height}.");
        if (PlayerCount != setup.PlayerCount)
            throw new InvalidDataException($"{what} player count {PlayerCount} does not match setup {setup.PlayerCount}.");
        if (!TeamsEqual(Teams, setup.Teams))
            throw new InvalidDataException($"{what} teams do not match the setup.");
        if (Limits != setup.Limits)
            throw new InvalidDataException($"{what} store limits {Limits} do not match setup {setup.Limits}.");
        if (AllowDebug != setup.AllowDebug)
            throw new InvalidDataException($"{what} AllowDebug={AllowDebug} does not match setup AllowDebug={setup.AllowDebug}.");
    }

    /// <summary>
    /// A setup for this header from rules and a map the caller loaded (for example from <see cref="RulesFiles"/>):
    /// seed, player count, teams, limits and AllowDebug come from the header. Throws InvalidDataException when the
    /// rules hash or map hash differ.
    /// </summary>
    public SimSetup ToSetup(Map.MapData map, Rules.SimRules rules, ulong rulesHash)
    {
        if (rulesHash != RulesHash)
            throw new InvalidDataException($"Rules hash 0x{rulesHash:x16} does not match the recorded 0x{RulesHash:x16}.");
        if (Map.MapHasher.Compute(map) != MapHash)
            throw new InvalidDataException($"Map hash 0x{Map.MapHasher.Compute(map):x16} does not match the recorded 0x{MapHash:x16}.");
        return new SimSetup(Seed, map, PlayerCount, rules, rulesHash) { Teams = Teams, Limits = Limits, AllowDebug = AllowDebug };
    }

    private static bool TeamsEqual(IReadOnlyList<byte>? a, IReadOnlyList<byte>? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
