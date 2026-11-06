// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>What a registered Lua function does today (M8-02).</summary>
public enum ApiStatus
{
    /// <summary>Complete since the pilot (M3); must not change.</summary>
    Exists,

    /// <summary>Complete as part of the M8-02 contracts.</summary>
    Implemented,

    /// <summary>Validates its arguments, then does nothing.</summary>
    NoOp,

    /// <summary>Validates its arguments, then raises "not implemented (package)".</summary>
    Throws,
}

/// <summary>One row of the Lua API table: the same rows as docs/missions/api.md, checked against the registered functions by tests.</summary>
/// <param name="Name">Table.Function, e.g. "Mission.Timer".</param>
/// <param name="Status">What it does today.</param>
/// <param name="Package">Package that fills or owns it (M8-03..06), or "pilot".</param>
public sealed record ApiEntry(string Name, ApiStatus Status, string Package);

/// <summary>The full mission Lua API (plan 3.1 and 4). The tests fail if a function is registered but not listed here, or listed but not registered.</summary>
public static class MissionApiContract
{
    /// <summary>Every function, pilot ones first.</summary>
    public static IReadOnlyList<ApiEntry> Entries { get; } = Build();

    private static List<ApiEntry> Build()
    {
        var l = new List<ApiEntry>();
        void Add(ApiStatus st, string pkg, params string[] names) { foreach (string n in names) l.Add(new ApiEntry(n, st, pkg)); }

        Add(ApiStatus.Exists, "pilot",
            "Trigger.AfterDelay", "Trigger.Every", "Trigger.OnEnter", "Trigger.OnDestroyed", "Trigger.OnAllDestroyed",
            "Actor.Create", "Actor.Order", "Actor.Alive", "Actor.Count", "Actor.CountInZone", "Actor.Cell",
            "Reinforce.Edge", "Objective.Add", "Objective.Complete", "Objective.Fail",
            "Tech.Grant", "Tech.Revoke", "Tech.Has", "Media.Message", "Player.Cash", "Player.AddCash",
            "Map.Zone", "Map.Center", "Map.Width", "Map.Height", "Mission.Win", "Mission.Lose", "Mission.Tick", "Random.Next");
        Add(ApiStatus.Implemented, "M8-03",
            "Trigger.OnLeave", "Trigger.OnHold", "Trigger.OnSeen", "Trigger.OnIdle",
            "Objective.AddHidden", "Objective.Reveal", "Objective.Status",
            "Mission.Timer", "Mission.TimerCancel", "Mission.TimerLeft", "Mission.AllPrimaryDone", "Mission.AnyPrimaryFailed", "Mission.Difficulty",
            "Tech.GrantMany", "Tech.List");
        Add(ApiStatus.Implemented, "M8-04", "Reinforce.Zone", "Reinforce.Transport", "Reinforce.Delayed", "Reinforce.Naval", "Reinforce.Rig", "Actor.CreateGroup");
        Add(ApiStatus.Implemented, "M8-05", "Dialogue.Say", "Dialogue.Clear", "Camera.Focus", "Media.MessageKey");
        Add(ApiStatus.Implemented, "M8-06", "AI.Attack", "AI.Defend", "AI.SetTier", "AI.Wave", "AI.Clear");
        return l;
    }
}
