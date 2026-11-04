// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;

namespace Rts.Scripting;

/// <summary>
/// Caps the Lua instructions one script callback may run (M3-12), so a runaway loop raises a script error instead of hanging
/// the simulation. Attached to a mission's script as a debugger, which MoonSharp polls before every instruction; it only counts
/// and never pauses, so the count is deterministic (replays and saves see the same error on the same tick).
/// </summary>
internal sealed class InstructionBudget : IDebugger
{
    private long _left;

    /// <summary>Instructions allowed per callback.</summary>
    public int Limit { get; }

    /// <summary>Creates a budget with <paramref name="limit"/> instructions per callback.</summary>
    public InstructionBudget(int limit) => Limit = limit;

    /// <summary>Starts a new callback with the full budget.</summary>
    public void Reset() => _left = Limit;

    /// <summary>Spends one instruction; throws a script error when the budget is used up.</summary>
    private void Spend()
    {
        if (--_left < 0)
            throw new ScriptRuntimeException("instruction budget exceeded (" + Limit + " instructions per callback)");
    }

    /// <inheritdoc />
    public DebuggerCaps GetDebuggerCaps() => DebuggerCaps.CanDebugByteCode;

    /// <inheritdoc />
    public void SetDebugService(DebugService debugService) { }

    /// <inheritdoc />
    public void SetSourceCode(SourceCode sourceCode) { }

    /// <inheritdoc />
    public void SetByteCode(string[] byteCode) { }

    /// <inheritdoc />
    public bool IsPauseRequested()
    {
        Spend();
        return false;
    }

    /// <inheritdoc />
    public bool SignalRuntimeException(ScriptRuntimeException ex) => false;

    /// <inheritdoc />
    public DebuggerAction GetAction(int ip, SourceRef sourceref) => new() { Action = DebuggerAction.ActionType.Run };

    /// <inheritdoc />
    public void SignalExecutionEnded() { }

    /// <inheritdoc />
    public void Update(WatchType watchType, IEnumerable<WatchItem> items) { }

    /// <inheritdoc />
    public List<DynamicExpression> GetWatchItems() => new();

    /// <inheritdoc />
    public void RefreshBreakpoints(IEnumerable<SourceRef> refs) { }
}
