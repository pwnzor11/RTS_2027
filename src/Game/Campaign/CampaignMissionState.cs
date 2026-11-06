// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Campaign;

/// <summary>Where a campaign mission stands for a profile (plan 3.4): done, playable next, or waiting on a requirement.</summary>
public enum CampaignMissionState
{
    /// <summary>Won at least once (can be replayed).</summary>
    Completed,

    /// <summary>Not won yet and every requirement is completed.</summary>
    Next,

    /// <summary>A required mission is not completed yet.</summary>
    Locked,
}
