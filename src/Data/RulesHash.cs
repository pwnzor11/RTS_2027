using System.IO.Hashing;
using System.Text;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// Hash over the parsed rules, not over the file text. All integers are little-endian; strings are a 4-byte length
/// then UTF-8 bytes; booleans are one byte. Layout, in order:
/// <list type="number">
/// <item>Version byte (3), then TickRate, DefaultMapWidth, DefaultMapHeight.</item>
/// <item>Weapons: count, then per weapon in id order: id, Damage, Range, MinRange, RateOfFire, ProjectileSpeed, Warhead index.</item>
/// <item>Warheads: count, then per warhead in id order: id, versus count, versus values, Splash, HitsAllies.</item>
/// <item>Vehicles: count, then per vehicle in id order: id, Locomotion, Speed, TurnRate, MaxHealth, Armor, Sight,
/// PrimaryWeapon, HasTurret, TurretTurnRate, Crusher, Cost, prerequisites (count, ids).</item>
/// <item>Infantry: count, then per type in id order: id, Speed, MaxHealth, Armor, Sight, PrimaryWeapon, Cost,
/// prerequisites (count, ids).</item>
/// <item>Buildings: count, then per building in id order: id, FootprintW, FootprintH, MaxHealth, Armor, Sight,
/// PrimaryWeapon, Cost, Power, prerequisites (count, ids), provides (count, names).</item>
/// <item>Effects: count, then per effect in id order: id, Op, Target, Attr, Value.</item>
/// <item>Terrain: speed percent for every land (LandType order) and locomotion (Locomotion order), then the buildable
/// flag for every land.</item>
/// </list>
/// Hashed with XxHash64. Whitespace, comments and key order in the TOML do not affect it. The loader sorts each
/// list by id before this runs.
/// </summary>
public static class RulesHash
{
    /// <summary>Canonical layout version. Bump only with a DECISIONS entry.</summary>
    public const byte FormatVersion = 3;

    /// <summary>Computes the rules hash over the game constants and every sim type table.</summary>
    public static ulong Compute(GameRules game, SimRules sim)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(FormatVersion);
            w.Write(game.TickRate);
            w.Write(game.DefaultMapWidth);
            w.Write(game.DefaultMapHeight);

            w.Write(sim.Weapons.Count);
            foreach (WeaponType x in sim.Weapons)
            {
                WriteString(w, x.Id);
                w.Write(x.Damage);
                w.Write(x.Range);
                w.Write(x.MinRange);
                w.Write(x.RateOfFire);
                w.Write(x.ProjectileSpeed);
                w.Write(x.Warhead);
            }

            w.Write(sim.Warheads.Count);
            foreach (WarheadType x in sim.Warheads)
            {
                WriteString(w, x.Id);
                w.Write(x.VersusPercent.Count);
                foreach (int v in x.VersusPercent) w.Write(v);
                w.Write(x.Splash);
                w.Write(x.HitsAllies);
            }

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
                WriteStrings(w, v.Prerequisites);
            }

            w.Write(sim.Infantry.Count);
            foreach (InfantryType x in sim.Infantry)
            {
                WriteString(w, x.Id);
                w.Write(x.Speed);
                w.Write(x.MaxHealth);
                w.Write((byte)x.Armor);
                w.Write(x.Sight);
                w.Write(x.PrimaryWeapon);
                w.Write(x.Cost);
                WriteStrings(w, x.Prerequisites);
            }

            w.Write(sim.Buildings.Count);
            foreach (BuildingType x in sim.Buildings)
            {
                WriteString(w, x.Id);
                w.Write(x.FootprintW);
                w.Write(x.FootprintH);
                w.Write(x.MaxHealth);
                w.Write((byte)x.Armor);
                w.Write(x.Sight);
                w.Write(x.PrimaryWeapon);
                w.Write(x.Cost);
                w.Write(x.Power);
                WriteStrings(w, x.Prerequisites);
                WriteStrings(w, x.Provides);
            }

            w.Write(sim.Effects.Count);
            foreach (EffectDef x in sim.Effects)
            {
                WriteString(w, x.Id);
                WriteString(w, x.Op);
                WriteString(w, x.Target);
                WriteString(w, x.Attr);
                w.Write(x.Value);
            }

            for (int land = 0; land < RuleCounts.LandTypes; land++)
            {
                for (int loco = 0; loco < RuleCounts.Locomotions; loco++)
                {
                    w.Write(sim.Terrain.SpeedPercent((LandType)land, (Locomotion)loco));
                }
            }
            for (int land = 0; land < RuleCounts.LandTypes; land++)
            {
                w.Write(sim.Terrain.Buildable((LandType)land));
            }
        }
        return XxHash64.HashToUInt64(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    private static void WriteStrings(BinaryWriter w, IReadOnlyList<string> values)
    {
        w.Write(values.Count);
        foreach (string s in values) WriteString(w, s);
    }

    private static void WriteString(BinaryWriter w, string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s);
        w.Write(b.Length);
        w.Write(b);
    }
}
