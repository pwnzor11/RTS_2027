// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Campaign;

/// <summary>One row of the mission-select list.</summary>
/// <param name="Id">Mission id.</param>
/// <param name="State">Completed, next or locked.</param>
/// <param name="Exists">False while the mission folder is still a placeholder (no data/missions/&lt;id&gt;), so it cannot be started.</param>
public sealed record CampaignMissionEntry(string Id, CampaignMissionState State, bool Exists);
