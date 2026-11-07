// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting;

namespace Rts.Game.Ui.Objectives;

/// <summary>
/// What the objective HUD shows and when (M11-05), with no graphics: which objective is current, the one-line text, the "NEW OBJECTIVE"
/// banner and minimap flash timers, which objective just arrived (for the camera cue), the O toggle and Tab cycling. Fed the visible objective
/// list each frame; time comes in as seconds so it is testable.
/// </summary>
public sealed class ObjectiveGuidanceModel
{
    /// <summary>How long the banner stays up, in seconds.</summary>
    public const float BannerSeconds = 3f;

    /// <summary>How long the minimap flash lasts, in seconds.</summary>
    public const float FlashSeconds = 2f;

    private readonly List<(string Id, ObjectiveStatus Status)> _known = new();
    private bool _observed;
    private string _selected = "";

    /// <summary>True when the full list and the briefing are shown (the O toggle).</summary>
    public bool Expanded { get; private set; }

    /// <summary>Banner text ("NEW OBJECTIVE: ..."), or empty when none is up.</summary>
    public string Banner { get; private set; } = "";

    /// <summary>Seconds left on the banner.</summary>
    public float BannerLeft { get; private set; }

    /// <summary>Seconds left on the minimap flash.</summary>
    public float FlashLeft { get; private set; }

    /// <summary>Id of an objective that arrived since the last <see cref="TakeArrival"/>, or empty.</summary>
    private string _arrival = "";

    /// <summary>Flips the full list and briefing on or off.</summary>
    public void ToggleExpanded() => Expanded = !Expanded;

    /// <summary>
    /// Looks at this frame's visible objectives: new ones and status changes raise the banner and flash; the clocks run down by
    /// <paramref name="dt"/>. <paramref name="announceFirst"/> says whether objectives present at the first look count as new (true at
    /// mission start, false when a save was loaded). <paramref name="label"/> turns objective text into display text.
    /// </summary>
    public void Observe(IReadOnlyList<MissionObjective> visible, float dt, bool announceFirst, Func<string, string> label)
    {
        BannerLeft = Math.Max(0f, BannerLeft - dt);
        FlashLeft = Math.Max(0f, FlashLeft - dt);
        if (BannerLeft <= 0f) Banner = "";
        foreach (MissionObjective o in visible)
        {
            int at = _known.FindIndex(k => k.Id == o.Id);
            if (at < 0)
            {
                _known.Add((o.Id, o.Status));
                if (_observed || announceFirst) Raise("NEW OBJECTIVE: " + label(o.Text), o.Id);
            }
            else if (_known[at].Status != o.Status)
            {
                _known[at] = (o.Id, o.Status);
                if (o.Status == ObjectiveStatus.Completed) Raise("OBJECTIVE COMPLETE: " + label(o.Text), "");
                else if (o.Status == ObjectiveStatus.Failed) Raise("OBJECTIVE FAILED: " + label(o.Text), "");
            }
        }

        _observed = true;
    }

    private void Raise(string text, string arrivalId)
    {
        Banner = text; BannerLeft = BannerSeconds; FlashLeft = FlashSeconds;
        if (arrivalId.Length > 0) _arrival = arrivalId;
    }

    /// <summary>The id of the objective that has just arrived (once), or null. The screen pans to its target when the setting is on.</summary>
    public string? TakeArrival()
    {
        if (_arrival.Length == 0) return null;
        string id = _arrival;
        _arrival = "";
        return id;
    }

    /// <summary>The active objectives in list order.</summary>
    public static List<MissionObjective> Active(IReadOnlyList<MissionObjective> visible) =>
        visible.Where(static o => o.Status == ObjectiveStatus.Active).ToList();

    /// <summary>The current objective: the Tab selection while it is still active, else the first active one; null when none are active.</summary>
    public MissionObjective? Current(IReadOnlyList<MissionObjective> visible)
    {
        List<MissionObjective> active = Active(visible);
        return active.Find(o => o.Id == _selected) ?? (active.Count > 0 ? active[0] : null);
    }

    /// <summary>
    /// The objective the on-map guidance points at: the current one when it has a target cell (<paramref name="hasCell"/>), else the first
    /// active one that has. Null when no active objective can be pointed at.
    /// </summary>
    public MissionObjective? Guided(IReadOnlyList<MissionObjective> visible, Func<MissionObjective, bool> hasCell)
    {
        MissionObjective? cur = Current(visible);
        if (cur is not null && hasCell(cur)) return cur;
        return Active(visible).Find(o => hasCell(o));
    }

    /// <summary>Tab: makes the next active objective that has a target cell the selection (wrapping). Returns it, or null when none has one.</summary>
    public MissionObjective? Cycle(IReadOnlyList<MissionObjective> visible, Func<MissionObjective, bool> hasCell)
    {
        List<MissionObjective> withTarget = Active(visible).Where(o => hasCell(o)).ToList();
        if (withTarget.Count == 0) return null;
        MissionObjective? cur = Guided(visible, hasCell);
        int at = cur is null ? -1 : withTarget.FindIndex(o => o.Id == cur.Id);
        MissionObjective next = withTarget[(at + 1) % withTarget.Count];
        _selected = next.Id;
        return next;
    }

    /// <summary>"Objective 1 of 3: text" for the current objective (position among the active ones), or a done line when none is active.</summary>
    public string OneLine(IReadOnlyList<MissionObjective> visible, Func<string, string> label)
    {
        List<MissionObjective> active = Active(visible);
        MissionObjective? cur = Current(visible);
        if (cur is null) return visible.Count == 0 ? "No objectives" : "All objectives resolved";
        return "Objective " + (active.FindIndex(o => o.Id == cur.Id) + 1) + " of " + active.Count + ": " + (cur.Primary ? "" : "(optional) ") + label(cur.Text);
    }
}
