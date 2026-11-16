// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Map;

namespace Rts.Sim;

// M12-16 (D-264 (3), design 16 D1 option C): destructible blocking props. Health lives in the "props" part (PropState); the rules are
// game.toml [props] (PropRules). Splash damage and crushing arrive in the next steps of the package.
public sealed partial class Simulation
{
    /// <summary>The "props" part: the health of every destructible prop (Tree, Snag, Wreck) on the map (M12-16).</summary>
    public PropState Props => _props;
}
