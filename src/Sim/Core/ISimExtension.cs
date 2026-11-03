// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>
/// The Simulation's one generic extension point (M2-15, D-015/D-061): a mission script host plugs in through
/// <see cref="SimSetup.Extensions"/>. The Sim knows nothing about the scripting engine; the Scripting project implements this.
/// <see cref="OnTick"/> runs inside Step in the trigger phase, in list order, so everything it does must be deterministic and its
/// state must live in <see cref="State"/> (appended after the built-in parts: saved and hashed).
/// </summary>
public interface ISimExtension
{
    /// <summary>Stable short name for diagnostics.</summary>
    string Name { get; }

    /// <summary>Called once at the end of the Simulation constructor (new game and before a Load). Bind to the sim here; do not spawn.</summary>
    void OnSetup(Simulation sim);

    /// <summary>Called once per Step from the trigger phase. <c>sim.Tick</c> is the tick being run (it increments after).</summary>
    void OnTick(Simulation sim);

    /// <summary>The extension's saved, hashed state part.</summary>
    ISimStatePart State { get; }
}
