// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.AI;

/// <summary>
/// M5 contract (docs/design/09_m5_plan.md §2): something outside the sim that sets a slot's strategy, such as the LLM commander.
/// <see cref="AiDriver"/> polls it every tick for its slot, between Steps, before the slot's Think. When it returns true the driver
/// issues a <see cref="CommandKind.Directive"/> for the slot (ExecTick = tick + the slot's command delay, the slot's next Sequence),
/// so the directive enters the recorded command stream like any AI command and replays exactly without the source.
/// Implementations must not block: start slow work (network) elsewhere and return its result on a later poll.
/// </summary>
public interface IDirectiveSource
{
    /// <summary>Called once per tick. <paramref name="view"/> is the slot's fog-honest view (read it now; do not keep it).
    /// Return true with a packed directive (<see cref="DirectiveCodec.Pack"/>) and a focus cell (or <see cref="Directive.NoCell"/>)
    /// to issue one this tick.</summary>
    bool Poll(PlayerView view, out int packedArg, out CellCoord focusCell);
}
