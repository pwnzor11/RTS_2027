// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>Kinds of trigger (persisted as a byte; append only).</summary>
internal enum TriggerKind : byte { After = 0, Every = 1, Enter = 2, Destroyed = 3, AllDestroyed = 4, Leave = 5, Hold = 6, Seen = 7, Idle = 8 }

/// <summary>One live trigger. Callbacks are stored by global function name so a loaded game can find them again.</summary>
internal sealed class TriggerRecord
{
    public TriggerKind Kind;
    /// <summary>Period (Every) or player (Enter, AllDestroyed).</summary>
    public int A;
    /// <summary>Zone name (Enter) or actor tag (Destroyed).</summary>
    public string Name = "";
    public string Fn = "";
    /// <summary>Tick at which After / Every fires next.</summary>
    public int Next;
    public bool Done;
    /// <summary>Second argument (M8-02, saved in the v2 tail): the hold or idle length in ticks (Hold, Idle).</summary>
    public int B;
    /// <summary>Running counter (M8-02, saved in the v2 tail): ticks held or idle so far.</summary>
    public int Counter;
}
