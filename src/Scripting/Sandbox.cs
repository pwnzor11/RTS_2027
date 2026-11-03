// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;

namespace Rts.Scripting;

/// <summary>
/// Builds the restricted Lua environment (review A-7). Only the hard-sandbox core modules are loaded (no os, io, debug, coroutine,
/// load); then load, loadstring, dofile, require, collectgarbage, string.dump and every non-integer-safe math function are removed,
/// <c>math</c> keeps only abs/min/max/floor/ceil, and <c>pairs</c> is replaced by an iterator in canonical key order (integers
/// ascending, then strings ordinal; any other key type is an error). math.random does not exist: use Random.Next(n).
/// </summary>
internal static class Sandbox
{
    private static readonly string[] RemovedGlobals =
    {
        "load", "loadstring", "loadfile", "dofile", "require", "collectgarbage", "rawget", "rawset", "setmetatable", "getmetatable",
        "os", "io", "debug", "coroutine", "package", "module", "newproxy", "print",
    };

    public static Script Create(MissionHost host, List<string> printLog)
    {
        var script = new Script(CoreModules.Preset_HardSandbox);
        script.Options.DebugPrint = _ => { };
        script.Options.ScriptLoader = new InvalidScriptLoader();
        foreach (string name in RemovedGlobals) script.Globals.Set(name, DynValue.Nil);

        // Only integer-preserving math.
        DynValue math = script.Globals.Get("math");
        var safe = new Table(script);
        if (math.Type == DataType.Table)
            foreach (string f in new[] { "abs", "min", "max", "floor", "ceil" }) safe.Set(f, math.Table.Get(f));
        script.Globals.Set("math", DynValue.NewTable(safe));

        DynValue str = script.Globals.Get("string");
        if (str.Type == DataType.Table) str.Table.Set("dump", DynValue.Nil);

        script.Globals.Set("pairs", DynValue.NewCallback(OrderedPairs, "pairs"));
        script.Globals.Set("print", DynValue.NewCallback((_, args) =>
        {
            if (printLog.Count < 200) printLog.Add(string.Join("\t", Enumerable.Range(0, args.Count).Select(i => args[i].ToPrintString())));
            return DynValue.Nil;
        }, "print"));
        script.Globals.Set("state", DynValue.NewTable(new Table(script)));
        host.RegisterApi(script);
        return script;
    }

    private static DynValue OrderedPairs(ScriptExecutionContext ctx, CallbackArguments args)
    {
        DynValue t = args[0];
        if (t.Type != DataType.Table) throw new ScriptRuntimeException("pairs: table expected, got " + t.Type.ToLuaTypeString());
        if (!LuaStateCodec.TryOrderedKeys(t.Table, out List<DynValue> keys))
            throw new ScriptRuntimeException("pairs: only integer and string keys are allowed (iteration order must be deterministic)");
        int position = 0;
        Table table = t.Table;
        var iterator = DynValue.NewCallback((_, _) =>
        {
            while (position < keys.Count)
            {
                DynValue k = keys[position++];
                DynValue v = table.Get(k);
                if (v.Type != DataType.Nil) return DynValue.NewTuple(k, v);
            }

            return DynValue.Nil;
        }, "pairs_iterator");
        return DynValue.NewTuple(iterator, t, DynValue.Nil);
    }

    /// <summary>A loader that refuses every file access (Preset_HardSandbox has no require, this is belt and braces).</summary>
    private sealed class InvalidScriptLoader : ScriptLoaderBase
    {
        public override object LoadFile(string file, Table globalContext) => throw new ScriptRuntimeException("loading files from scripts is forbidden");

        public override bool ScriptFileExists(string name) => false;
    }
}
