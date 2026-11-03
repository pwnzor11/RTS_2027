// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Determinism;

/// <summary>Runs the determinism analyzer over src/Sim and proves each rule fires (and does not over-fire).</summary>
public class DeterminismRulesTests(ITestOutputHelper output)
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RTS_2027.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("RTS_2027.slnx not found above " + AppContext.BaseDirectory);
    }

    private static List<(string Path, string Source)> SimFiles()
    {
        var root = RepoRoot();
        var sep = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(root, "src", "Sim"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetRelativePath(root, f), File.ReadAllText(f)))
            .ToList();
    }

    private static IReadOnlyList<DeterminismRules.Violation> Scan(params (string Path, string Source)[] files) =>
        DeterminismRules.Analyze(files, DeterminismRules.DefaultReferences());

    private static IReadOnlyList<DeterminismRules.Violation> ScanMembers(string members) =>
        Scan(("snippet.cs", "namespace Test;\nusing System.Collections.Concurrent;\nusing System.Collections.Immutable;\nusing System.Diagnostics;\nstatic class Wrapper\n{\n" + members + "\n}\n"));

    private static string Format(IEnumerable<DeterminismRules.Violation> vs) =>
        string.Join(Environment.NewLine, vs.Select(v => $"{v.Path}:{v.Line} {v.Rule} {v.Text}"));

    [Fact]
    public void SimSource_HasNoViolations()
    {
        var sw = Stopwatch.StartNew();
        var files = SimFiles();
        Assert.NotEmpty(files);
        var violations = DeterminismRules.Analyze(files, DeterminismRules.DefaultReferences());
        output.WriteLine($"Scanned {files.Count} files in {sw.ElapsedMilliseconds} ms");
        Assert.True(violations.Count == 0, "Determinism violations in src/Sim:" + Environment.NewLine + Format(violations));
    }

    [Fact]
    public void SimSource_AllowComments_HaveReasons()
    {
        var bad = DeterminismRules.Analyze(SimFiles(), DeterminismRules.DefaultReferences()).Where(v => v.Rule == "DET000");
        Assert.True(!bad.Any(), Format(bad));
    }

    [Theory]
    [InlineData("double d = 1.0;")]
    [InlineData("float f = 1;")]
    [InlineData("var m = System.Math.Sqrt(4);")]
    public void InjectedFloatingPoint_IntoRealSource_FailsScan(string statement)
    {
        var files = SimFiles();
        files.Add(("scratch/Injected.cs", $"namespace Rts.Sim;\nstatic class Injected\n{{\n    static void M()\n    {{\n        {statement}\n    }}\n}}\n"));
        var violations = DeterminismRules.Analyze(files, DeterminismRules.DefaultReferences());
        Assert.Contains(violations, v => v.Path == "scratch/Injected.cs" && v.Rule == "DET001");
    }

    [Fact]
    public void CompilationErrors_AreReported_NotSilentlyPassed()
    {
        var v = ScanMembers("static void M() { var x = DoesNotExist.Call(); }");
        Assert.Contains(v, x => x.Rule == "DET900");
    }

    [Theory]
    [InlineData("static void M(Dictionary<int,int> d) { foreach (var kv in d) { } } // det-allow: DET004 order-independent sum", false)]
    [InlineData("static void M(Dictionary<int,int> d) { foreach (var kv in d) { } } // det-allow: DET004", true)]
    [InlineData("static void M(Dictionary<int,int> d) { foreach (var kv in d) { } } // det-allow: DET001 wrong rule", true)]
    public void AllowComment_NeedsMatchingRuleAndReason(string members, bool expectViolation)
    {
        var v = ScanMembers(members);
        Assert.Equal(expectViolation, v.Count > 0);
    }

    private static void AssertFires(string rule, string members)
    {
        var v = ScanMembers(members);
        Assert.DoesNotContain(v, x => x.Rule == "DET900");
        Assert.Contains(v, x => x.Rule == rule);
    }

    private static void AssertClean(string members)
    {
        var v = ScanMembers(members);
        Assert.True(v.Count == 0, Format(v));
    }

    [Theory]
    [InlineData("static void M() { double d = 1.0; }")]
    [InlineData("static float F;")]
    [InlineData("static decimal M() => 1m;")]
    [InlineData("static int M() { return (int)System.Math.Sqrt(4); }")]
    [InlineData("static void M() { var x = 1.5f; }")]
    [InlineData("static List<double> L = new();")]
    public void Det001_Fires(string members) => AssertFires("DET001", members);

    [Theory]
    [InlineData("static int X = 3;")]
    [InlineData("static int Sum(List<int> list) { int s = 0; foreach (var u in list) s += u; return s; }")]
    [InlineData("struct Double { public int V; } static Double D;")]
    [InlineData("struct DoubleVec { public long X; } static DoubleVec V;")]
    [InlineData("sealed class SimRandom { public uint Next() => 4; } static uint R() => new SimRandom().Next();")]
    public void Det001_NoFalsePositive(string members) => AssertClean(members);

    [Theory]
    [InlineData("static object R = new Random();")]
    [InlineData("static long N() => DateTime.UtcNow.Ticks;")]
    [InlineData("static int T() => Environment.TickCount;")]
    [InlineData("static long T() => Environment.TickCount64;")]
    [InlineData("static void S() { var sw = Stopwatch.StartNew(); }")]
    [InlineData("static object P = TimeProvider.System;")]
    [InlineData("static object O = DateTimeOffset.Now;")]
    public void Det002_Fires(string members) => AssertFires("DET002", members);

    [Theory]
    [InlineData("sealed class SimRandom { public uint Next() => 4; } static uint R() => new SimRandom().Next();")]
    [InlineData("struct DateTime { public int Tick; } static DateTime D;")]
    [InlineData("static int Env() => Environment.ProcessorCount;")]
    public void Det002_NoFalsePositive(string members) => AssertClean(members);

    [Theory]
    [InlineData("static void P() { Parallel.For(0, 1, _ => { }); }")]
    [InlineData("static Task? T() { return null; }")]
    [InlineData("static Task<int>? T() { return null; }")]
    [InlineData("static async void A() { }")]
    [InlineData("static object o = new(); static void L() { lock (o) { } }")]
    [InlineData("static Thread? Th;")]
    [InlineData("static void Q() { ThreadPool.QueueUserWorkItem(_ => { }); }")]
    public void Det003_Fires(string members) => AssertFires("DET003", members);

    [Theory]
    [InlineData("static void Run() { }")]
    [InlineData("class Task { } static Task? T;")]
    [InlineData("static int Lock(int x) => x + 1;")]
    public void Det003_NoFalsePositive(string members) => AssertClean(members);

    [Theory]
    [InlineData("static void M(Dictionary<int,int> d) { foreach (var kv in d) { } }")]
    [InlineData("static void M(HashSet<int> h) { foreach (var x in h) { } }")]
    [InlineData("static void M(Dictionary<int,int> d) { foreach (var k in d.Keys) { } }")]
    [InlineData("static int M(Dictionary<int,int> d) => d.Select(kv => kv.Key).Sum();")]
    [InlineData("static void M(Dictionary<int,int> d) { var e = d.GetEnumerator(); }")]
    [InlineData("static void M(IDictionary<int,int> d) { foreach (var kv in d) { } }")]
    [InlineData("static void M(ISet<int> s) { foreach (var x in s) { } }")]
    [InlineData("static void M(ConcurrentDictionary<int,int> d) { foreach (var kv in d) { } }")]
    [InlineData("static void M(ImmutableDictionary<int,int> d) { foreach (var kv in d) { } }")]
    [InlineData("static void M(ImmutableHashSet<int> s) { foreach (var x in s) { } }")]
    [InlineData("static int M(HashSet<int> h) => (from x in h select x).Count();")]
    public void Det004_Fires(string members) => AssertFires("DET004", members);

    [Theory]
    [InlineData("static int M(List<int> l) { int s = 0; foreach (var x in l) s += x; return s; }")]
    [InlineData("static int M(Dictionary<int,int> d) => d[1];")]
    [InlineData("static bool M(HashSet<int> h) => h.Contains(1) && h.Count > 0;")]
    [InlineData("static void M(SortedDictionary<int,int> d) { foreach (var kv in d) { } }")]
    [InlineData("static int M(int[] a) => a.Select(x => x + 1).Sum();")]
    public void Det004_NoFalsePositive(string members) => AssertClean(members);

    [Theory]
    [InlineData("static int H(string s) => s.GetHashCode();")]
    [InlineData("static int H(object o) => o.GetHashCode();")]
    [InlineData("static Guid G() => Guid.NewGuid();")]
    [InlineData("static Func<Guid> G = Guid.NewGuid;")]
    public void Det005_Fires(string members) => AssertFires("DET005", members);

    [Theory]
    [InlineData("static int H(int x) => x.GetHashCode();")]
    [InlineData("static Guid G() => Guid.Empty;")]
    [InlineData("sealed class P { public override int GetHashCode() => 7; } static int H(P p) => p.GetHashCode();")]
    public void Det005_NoFalsePositive(string members) => AssertClean(members);
}
