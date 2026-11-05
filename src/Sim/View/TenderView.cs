// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.View;

/// <summary>M7-06: one of the viewing player's own supply tenders at sea (<see cref="PlayerView.CopyOwnTenders"/>).</summary>
/// <param name="Id">Tender id.</param>
/// <param name="Vehicle">The tender's vehicle entity.</param>
/// <param name="Cell">Cell it is in now.</param>
/// <param name="Payload">Credits aboard.</param>
public readonly record struct TenderView(int Id, EntityId Vehicle, CellCoord Cell, int Payload);
