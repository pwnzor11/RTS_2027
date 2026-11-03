using System.Reflection;
using Rts.Sim.Core;
using Rts.Sim.Vision;

namespace Rts.Sim.Tests.Determinism;

/// <summary>
/// Hash coverage: every array field of every state part must reach the part's serialised output. For each array
/// with more than one element, element 1 is changed and the part's Write bytes must change; the element is then
/// restored. This catches an array added to a part but not written, which would let two diverged games hash equal.
/// </summary>
public class HashCoverageTests
{
    /// <summary>Array fields that are deliberately not serialised. Key is "PartType.field"; value is the reason.</summary>
    private static readonly Dictionary<string, string> NotSerialised = new(StringComparer.Ordinal)
    {
        ["PathService._g"] = "A* scratch, rewritten on every solve (PathService: 'Not saved: scratch and cache.')",
        ["PathService._parent"] = "A* scratch, rewritten on every solve (PathService: 'Not saved: scratch and cache.')",
        ["PathService._seen"] = "A* stamp scratch, meaningful only within one solve (PathService: 'Not saved: scratch and cache.')",
        ["PathService._closed"] = "A* stamp scratch, meaningful only within one solve (PathService: 'Not saved: scratch and cache.')",
        ["VisionState._ghosts"] = "ghost data is written only where _ghostPresent is set; an absent slot is not state. "
            + "GhostHealth_IsSerialisedWhenPresent covers it with a present ghost.",
    };

    private enum Outcome { Mutated, Unsupported, Skipped }

    [Fact]
    public void GhostHealth_IsSerialisedWhenPresent()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        var ghost = new BuildingGhost(1, 0, new CellCoord(5, 5), 2, 2, 100);
        sim.Vision.RecordBuildingSeen(0, 1, ghost);
        byte[] before = WriteBytes(sim.Vision);
        sim.Vision.RecordBuildingSeen(0, 1, ghost with { Health = 99 });
        Assert.False(before.AsSpan().SequenceEqual(WriteBytes(sim.Vision)));
    }

    [Fact]
    public void EveryArrayFieldOfEveryPartIsSerialised()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        for (int i = 0; i < 20; i++) sim.Step(Array.Empty<Command>());

        var unsupported = new List<string>();
        var unchanged = new List<string>();
        int checkedArrays = 0;
        foreach (ISimStatePart part in sim.StateParts)
        {
            foreach (FieldInfo field in ArrayFields(part.GetType()))
            {
                string key = $"{part.GetType().Name}.{field.Name}";
                if (NotSerialised.ContainsKey(key)) continue;
                var array = (Array?)field.GetValue(part);
                if (array is null || array.Length <= 1) continue;

                byte[] before = WriteBytes(part);
                Outcome outcome = Mutate(array, 1, out Action? restore, out string? elementType);
                if (outcome == Outcome.Unsupported)
                {
                    unsupported.Add($"{key} (element {elementType})");
                    continue;
                }
                if (outcome == Outcome.Skipped) continue;

                byte[] after = WriteBytes(part);
                restore!();
                Assert.Equal(before, WriteBytes(part));
                checkedArrays++;
                if (before.AsSpan().SequenceEqual(after)) unchanged.Add(key);
            }
        }

        Assert.True(checkedArrays >= 45, $"only {checkedArrays} arrays were checked; the reflection walk found too few");
        Assert.Empty(unsupported);
        Assert.Empty(unchanged);
    }

    /// <summary>Fields of array type declared on the type and its base classes (instance fields only).</summary>
    private static IEnumerable<FieldInfo> ArrayFields(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type? t = type; t is not null && t != typeof(object); t = t.BaseType)
            foreach (FieldInfo f in t.GetFields(flags))
                if (f.FieldType.IsArray) yield return f;
    }

    /// <summary>Changes element <paramref name="index"/> of <paramref name="array"/> (for a jagged array, the same index
    /// of the inner array at index 1). Returns a restore action for the changed element.</summary>
    private static Outcome Mutate(Array array, int index, out Action? restore, out string? elementType)
    {
        restore = null;
        Type element = array.GetType().GetElementType()!;
        if (element.IsArray)
        {
            var inner = (Array?)array.GetValue(index);
            if (inner is null || inner.Length <= 1)
            {
                elementType = null;
                return Outcome.Skipped;
            }
            return Mutate(inner, 1, out restore, out elementType);
        }

        elementType = element.FullName;
        object? old = array.GetValue(index);
        object? next = Next(old);
        if (next is null) return Outcome.Unsupported;
        array.SetValue(next, index);
        restore = () => array.SetValue(old, index);
        return Outcome.Mutated;
    }

    /// <summary>A different value of the same type, or null when the type is not supported by this test.</summary>
    private static object? Next(object? value) => value switch
    {
        bool b => !b,
        byte v => unchecked((byte)(v + 1)),
        sbyte v => unchecked((sbyte)(v + 1)),
        short v => unchecked((short)(v + 1)),
        ushort v => unchecked((ushort)(v + 1)),
        int v => unchecked(v + 1),
        uint v => unchecked(v + 1),
        long v => unchecked(v + 1),
        ulong v => unchecked(v + 1),
        Enum e => NextEnum(e),
        LeptonCoord c => new LeptonCoord(c.X + 1, c.Y),
        CellCoord c => new CellCoord(c.X + 1, c.Y),
        EntityId id => new EntityId(unchecked((ushort)(id.Index + 1)), id.Generation),
        BuildingGhost g => g with { Health = g.Health + 1 },
        _ => null,
    };

    private static object? NextEnum(Enum value)
    {
        Array values = Enum.GetValues(value.GetType());
        if (values.Length < 2) return null;
        int at = Array.IndexOf(values, value);
        return values.GetValue((at + 1) % values.Length);
    }

    private static byte[] WriteBytes(ISimStatePart part)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) part.Write(w);
        return ms.ToArray();
    }
}
