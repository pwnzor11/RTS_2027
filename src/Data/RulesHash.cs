using System.IO.Hashing;
using System.Text;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// Hash over the parsed rules, not over the file text. Layout (all integers little-endian): version byte (2),
/// TickRate, DefaultMapWidth, DefaultMapHeight; vehicle count then per vehicle in list (ordinal id) order: id
/// (length-prefixed UTF-8), Locomotion, Speed, TurnRate, MaxHealth, Armor, Sight, PrimaryWeapon, HasTurret,
/// TurretTurnRate, Crusher, Cost, prerequisites (count + ids); counts of infantry, buildings, weapons,
/// warheads, effects; then the terrain table (speed percent for every land x locomotion, buildable per land).
/// Hashed with XxHash64. Whitespace, comments and key order in the TOML do not affect it.
/// </summary>
public static class RulesHash
{
    /// <summary>Canonical layout version. Bump only with a DECISIONS entry.</summary>
    public const byte FormatVersion = 2;

    /// <summary>Computes the rules hash. The loader sorts every list by id before calling this.</summary>
    public static ulong Compute(GameRules game, SimRules sim)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(FormatVersion);
            w.Write(game.TickRate);
            w.Write(game.DefaultMapWidth);
            w.Write(game.DefaultMapHeight);

            w.Write(sim.Vehicles.Count);
            foreach (VehicleType v in sim.Vehicles)
            {
                WriteString(w, v.Id);
                w.Write((byte)v.Locomotion);
                w.Write(v.Speed);
                w.Write(v.TurnRate);
                w.Write(v.MaxHealth);
                w.Write((byte)v.Armor);
                w.Write(v.Sight);
                w.Write(v.PrimaryWeapon);
                w.Write(v.HasTurret);
                w.Write(v.TurretTurnRate);
                w.Write(v.Crusher);
                w.Write(v.Cost);
                w.Write(v.Prerequisites.Count);
                foreach (string p in v.Prerequisites) WriteString(w, p);
            }

            w.Write(sim.Infantry.Count);
            w.Write(sim.Buildings.Count);
            w.Write(sim.Weapons.Count);
            w.Write(sim.Warheads.Count);
            w.Write(sim.Effects.Count);

            for (int land = 0; land < RuleCounts.LandTypes; land++)
            {
                for (int loco = 0; loco < RuleCounts.Locomotions; loco++)
                    w.Write(sim.Terrain.SpeedPercent((LandType)land, (Locomotion)loco));
                w.Write(sim.Terrain.Buildable((LandType)land));
            }
        }
        return XxHash64.HashToUInt64(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    private static void WriteString(BinaryWriter w, string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s);
        w.Write(b.Length);
        w.Write(b);
    }
}
