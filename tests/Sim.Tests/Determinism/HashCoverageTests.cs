// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
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
        // Make slot 1 of the projectile store live: only live slots are serialised (Spawn sets every field, Free clears).
        sim.Projectiles.Spawn(new LeptonCoord(300, 300), default, new LeptonCoord(900, 900), 10, 5, 0, 0, 0);

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
                Array target = array;
                if (target.GetType().GetElementType()!.IsArray)       // jagged (e.g. per-kind tables): use inner [1]
                    target = (Array?)array.GetValue(1) ?? array;
                Type elem = target.GetType().GetElementType()!;
                if (IsPlainStruct(elem) && target.Length > 1 && Next(target.GetValue(1)) is null)
                {
                    array = target;
                    // Struct element (e.g. CombatEntry, ActorRef): every field must reach the bytes on its own.
                    foreach (FieldInfo sf in elem.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        object old = array.GetValue(1)!;
                        object boxed = array.GetValue(1)!;
                        object? next = Next(sf.GetValue(boxed));
                        if (next is null) { unsupported.Add($"{key}.{sf.Name} (field {sf.FieldType})"); continue; }
                        sf.SetValue(boxed, next);
                        array.SetValue(boxed, 1);
                        byte[] changed = WriteBytes(part);
                        array.SetValue(old, 1);
                        checkedArrays++;
                        if (before.AsSpan().SequenceEqual(changed)) unchanged.Add($"{key}.{sf.Name}");
                    }
                    Assert.Equal(before, WriteBytes(part));
                    continue;
                }
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

    // ---- every instance field (A-6) ----------------------------------------------------------

    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Instance fields that are deliberately not in the hash, so not in the save either. Key is "DeclaringType.field"
    /// (auto-property backing fields appear as the property name). Every entry says why it is not sim state.
    /// An entry that no longer matches a field fails the test, so the list cannot go stale.
    /// </summary>
    private static readonly Dictionary<string, string> NotHashed = new(StringComparer.Ordinal)
    {
        // Simulation
        ["Simulation._setupLog"] = "construction history for replays (SetupLog), not state: a loaded sim starts with an empty log",
        ["Simulation.LateCommandCount"] = "diagnostic counter of commands that arrived after their tick; not state, resets on load (M1.5-02)",
        ["Simulation._teamMembers"] = "lazy cache derived from Setup.Teams (hashed via the players part); rebuilt identically after load (M1.5-01)",
        ["Simulation._inStep"] = "true only while Step runs; always false when a hash or save is taken",
        ["Simulation._recentHashes"] = "ring of already-computed hashes for diagnostics and snapshots; derived from state, not state",
        ["Simulation._scratch"] = "command sort buffer, rewritten on every ApplyInput",
        ["Simulation.Setup"] = "immutable construction input; its identity is in the save header, the players part and the replay header",
        ["Simulation.MapHash"] = "identity hash of the starting map, computed once at construction; checked by the save header",
        ["Simulation._open"] = "placement flood-fill scratch, rebuilt whenever _openVersion != the map version",
        ["Simulation._openVersion"] = "cache stamp for _open (compared with MapState.Version)",
        ["Simulation._fillStack"] = "placement flood-fill scratch",
        ["Simulation._fillMark"] = "placement flood-fill scratch (stamped by _fillStamp)",
        ["Simulation._ring"] = "placement flood-fill scratch",
        ["Simulation._fillStamp"] = "placement flood-fill stamp counter; only equality with _fillMark matters within one call",
        ["Simulation._pendingMoves"] = "move commands collected by ApplyInput and consumed (then cleared) by RunMissions in the same Step: empty between steps",
        ["Simulation._members"] = "scratch for one move batch, cleared at the start of every batch",
        ["Simulation._hashSink"] = "reused hashing sink (Simulation.Hashing.cs)",
        ["Simulation._hashWriter"] = "reused hashing writer (Simulation.Hashing.cs)",
        // SlotArray (inside every store)
        ["SlotArray._count"] = "number of set bits in _used: derived, and rebuilt by SetAlive when the used bits are read back",
        // PathService
        ["PathService._map"] = "reference to the map part, hashed there",
        ["PathService._terrain"] = "immutable rules table",
        ["PathService._perTick"] = "constructor constant (expansion budget per tick)",
        ["PathService._w"] = "map width, constant for the game",
        ["PathService._h"] = "map height, constant for the game",
        ["PathService._results"] = "is serialised; covered by PathResults_AreSerialised (a dictionary of arrays the walk cannot mutate)",
        ["PathService._cache"] = "path cache: only speeds up lookups, hits cost the same budget as misses (PathService: 'Not saved: scratch and cache')",
        ["PathService._heap"] = "A* open-list scratch, emptied at the start of every solve",
        ["PathService._stamp"] = "A* stamp counter; only equality with _seen/_closed matters within one solve",
        ["PathService.CacheEnabled"] = "a switch for tests and benchmarks; results do not depend on it",
        // MapState
        ["MapState.Data"] = "the immutable starting MapData; the live layers are the arrays above",
        ["MapState.Width"] = "constant for the game (from the map)",
        ["MapState.Height"] = "constant for the game (from the map)",
        // VisionState constants
        ["VisionState._width"] = "constant for the game", ["VisionState._height"] = "constant for the game",
        ["VisionState._cells"] = "width * height, constant", ["VisionState._playerCount"] = "constant for the game",
        ["VisionState._capacity"] = "constant for the game",
    };

    private sealed class WalkReport
    {
        public readonly List<string> Failures = new();
        public readonly HashSet<string> Seen = new(StringComparer.Ordinal);
        public int Checked;
    }

    [Fact]
    public void EveryInstanceFieldOfSimulationAndPartsReachesTheStateOrIsAllowListed()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        var script = ScriptedGame.Script(80);
        for (int i = 0; i < 40; i++) sim.Step(script);
        sim.Projectiles.Spawn(new LeptonCoord(300, 300), default, new LeptonCoord(900, 900), 10, 5, 0, 0, 0);
        sim.Paths.Request(new CellCoord(2, 2), new CellCoord(50, 50), Rts.Sim.Rules.Locomotion.Tracked);   // leave a request queued
        sim.Paths.Request(new CellCoord(3, 3), new CellCoord(51, 51), Rts.Sim.Rules.Locomotion.Tracked);

        var report = new WalkReport();
        Walk(sim, "Simulation", () => BitConverter.GetBytes(sim.ComputeStateHash()), isSim: true, depth: 0, report);
        foreach (ISimStatePart part in sim.StateParts)
            Walk(part, part.GetType().Name, () => WriteBytes(part), isSim: false, depth: 0, report);

        foreach (string key in NotHashed.Keys)
            if (!report.Seen.Contains(key)) report.Failures.Add($"allow-list entry '{key}' matches no field (stale)");
        Assert.Empty(report.Failures);
        Assert.True(report.Checked >= 20, $"only {report.Checked} fields were checked; the walk found too few");
    }

    [Fact]
    public void PathResults_AreSerialised()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        byte[] before = WriteBytes(sim.Paths);
        sim.Paths.Request(new CellCoord(2, 2), new CellCoord(20, 20), Rts.Sim.Rules.Locomotion.Tracked);
        byte[] queued = WriteBytes(sim.Paths);
        Assert.False(before.AsSpan().SequenceEqual(queued));
        sim.Paths.Process();                       // the request leaves the queue and its result waits to be polled
        byte[] withResult = WriteBytes(sim.Paths);
        Assert.False(queued.AsSpan().SequenceEqual(withResult));
    }

    private static string FieldKey(Type declaring, FieldInfo f)
    {
        string name = f.Name;
        if (name.StartsWith('<')) name = name[1..name.IndexOf('>')];
        return $"{declaring.Name}.{name}";
    }

    private static void Walk(object owner, string label, Func<byte[]> measure, bool isSim, int depth, WalkReport report)
    {
        for (Type? t = owner.GetType(); t is not null && t != typeof(object); t = t.BaseType)
            foreach (FieldInfo f in t.GetFields(InstanceFlags))
            {
                string key = FieldKey(t, f);
                report.Seen.Add(key);
                if (NotHashed.ContainsKey(key)) continue;
                Visit(owner, f, $"{label}.{key}", measure, isSim, depth, report);
            }
    }

    private static bool Changes(Func<byte[]> measure, byte[] before)
    {
        try { return !before.AsSpan().SequenceEqual(measure()); }
        catch (Exception e) when (e is IndexOutOfRangeException or NullReferenceException or ArgumentException or InvalidOperationException)
        {
            return true;   // the serialiser tripped over the changed value, so it reads it
        }
    }

    private static void Visit(object owner, FieldInfo f, string path, Func<byte[]> measure, bool isSim, int depth, WalkReport report)
    {
        object? value = f.GetValue(owner);
        Type type = f.FieldType;
        if (value is null) { report.Failures.Add($"{path}: null in the test fixture (cannot check it)"); return; }
        if (isSim && depth == 0 && (value is ISimStatePart || value is ISimStatePart[])) return;   // parts are walked on their own

        byte[] before = measure();
        object? next = Next(value);
        if (next is not null && (type.IsPrimitive || type.IsEnum || IsKnownStruct(type)))
        {
            f.SetValue(owner, next);
            bool changed;
            try { changed = Changes(measure, before); }
            finally { f.SetValue(owner, value); }
            report.Checked++;
            if (!changed) report.Failures.Add($"{path}: changing it does not change the state ({type.Name})");
            return;
        }
        if (value is Array array)
        {
            if (!isSim && depth == 0) return;                      // array fields of parts: EveryArrayFieldOfEveryPartIsSerialised
            if (array.Length == 0) { report.Failures.Add($"{path}: empty array in the fixture"); return; }
            MutateArrayElement(array, path, measure, before, report);
            return;
        }
        if (value is System.Collections.IList list)
        {
            VisitList(list, type, path, measure, before, isSim, depth, report);
            return;
        }
        if (type.IsClass && type.Namespace is { } ns && ns.StartsWith("Rts.Sim", StringComparison.Ordinal) && value is not Delegate)
        {
            Walk(value, path, measure, isSim, depth + 1, report);
            return;
        }
        report.Failures.Add($"{path}: unsupported field type {type} (add it to the walk or to NotHashed with a reason)");
    }

    private static bool IsKnownStruct(Type t) => t == typeof(LeptonCoord) || t == typeof(CellCoord) || t == typeof(EntityId);

    private static void MutateArrayElement(Array array, string path, Func<byte[]> measure, byte[] before, WalkReport report)
    {
        int index = array.Length > 1 ? 1 : 0;
        Type element = array.GetType().GetElementType()!;
        if (element.IsArray)
        {
            var inner = array.GetValue(index) as Array;
            if (inner is null || inner.Length == 0) { report.Failures.Add($"{path}: empty inner array in the fixture"); return; }
            MutateArrayElement(inner, path + "[]", measure, before, report);
            return;
        }
        object? old = array.GetValue(index);
        object? next = Next(old);
        if (next is null) { report.Failures.Add($"{path}: unsupported element type {element}"); return; }
        array.SetValue(next, index);
        bool changed;
        try { changed = Changes(measure, before); }
        finally { array.SetValue(old, index); }
        report.Checked++;
        if (!changed) report.Failures.Add($"{path}: changing an element does not change the state");
    }

    private static void VisitList(System.Collections.IList list, Type type, string path, Func<byte[]> measure, byte[] before,
        bool isSim, int depth, WalkReport report)
    {
        Type element = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
        if (list.Count == 0)
        {
            object? fresh;
            try { fresh = element.IsValueType || element.GetConstructor(Type.EmptyTypes) is not null ? Activator.CreateInstance(element) : null; }
            catch (Exception) { fresh = null; }
            if (fresh is null) { report.Failures.Add($"{path}: empty list in the fixture and its element cannot be constructed"); return; }
            list.Add(fresh);
            bool grew;
            try { grew = Changes(measure, before); }
            finally { list.RemoveAt(list.Count - 1); }
            report.Checked++;
            if (!grew) report.Failures.Add($"{path}: adding an element does not change the state");
            return;
        }
        object? first = list[0];
        if (first is not null && element.IsClass && element.Namespace is { } ns && ns.StartsWith("Rts.Sim", StringComparison.Ordinal))
        {
            Walk(first, path + "[0]", measure, isSim, depth + 1, report);
            return;
        }
        object? next = Next(first);
        if (next is null) { report.Failures.Add($"{path}: unsupported list element type {element}"); return; }
        list[0] = next;
        bool changed;
        try { changed = Changes(measure, before); }
        finally { list[0] = first; }
        report.Checked++;
        if (!changed) report.Failures.Add($"{path}: changing an element does not change the state");
    }

    private static bool IsPlainStruct(Type t) => t.IsValueType && !t.IsPrimitive && !t.IsEnum;

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
        ActorRef r => r with { Id = new EntityId(unchecked((ushort)(r.Id.Index + 1)), r.Id.Generation) },
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
