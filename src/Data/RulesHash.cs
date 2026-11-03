using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
using Rts.Sim.Core;

namespace Rts.Data;

/// <summary>
/// Hash over the parsed rules, not over the file text. Layout (all integers little-endian):
/// version byte (1), TickRate, DefaultMapWidth, DefaultMapHeight, unit count, then per unit in
/// ordinal id order: UTF-8 byte length, UTF-8 id bytes, Speed, MaxHealth. Hashed with XxHash64.
/// Whitespace, comments and key order in the TOML do not affect it.
/// </summary>
public static class RulesHash
{
    /// <summary>Canonical layout version. Bump only with a DECISIONS entry.</summary>
    public const byte FormatVersion = 1;

    /// <summary>Computes the rules hash. Units are sorted by id internally, so input order does not matter.</summary>
    public static ulong Compute(GameRules game, IReadOnlyList<UnitTypeDef> units)
    {
        var buf = new List<byte> { FormatVersion };
        WriteInt(buf, game.TickRate);
        WriteInt(buf, game.DefaultMapWidth);
        WriteInt(buf, game.DefaultMapHeight);

        var sorted = units.OrderBy(u => u.Id, StringComparer.Ordinal).ToList();
        WriteInt(buf, sorted.Count);
        foreach (var unit in sorted)
        {
            byte[] id = Encoding.UTF8.GetBytes(unit.Id);
            WriteInt(buf, id.Length);
            buf.AddRange(id);
            WriteInt(buf, unit.Speed);
            WriteInt(buf, unit.MaxHealth);
        }

        return XxHash64.HashToUInt64(buf.ToArray());
    }

    private static void WriteInt(List<byte> buf, int value)
    {
        Span<byte> tmp = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(tmp, value);
        buf.AddRange(tmp.ToArray());
    }
}
