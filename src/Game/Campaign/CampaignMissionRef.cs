// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Campaign;

/// <summary>S18 MIS-02: which campaign mission a running (or restored) mission game is: the campaign faction, the mission id and the tier it was started at.</summary>
/// <param name="Faction">Campaign faction id.</param>
/// <param name="MissionId">Mission id.</param>
/// <param name="Tier">Difficulty tier 1..5 the mission was started at.</param>
public sealed record CampaignMissionRef(string Faction, string MissionId, int Tier);
