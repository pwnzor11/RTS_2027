// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting;

namespace Rts.Game.Ui.Objectives;

/// <summary>
/// What the objective HUD shows and when (M11-05, M11-30), with no graphics: the rows of the collapsed bar, the highlighted objective that the
/// map guidance follows, the banner queue and minimap flash timers, which objective just arrived (for the camera cue), the O toggle and Tab
/// cycling. Fed the visible objective list each frame; time comes in as seconds so it is testable.
/// </summary>
public sealed class ObjectiveGuidanceModel
{
    /// <summary>How long a NEW OBJECTIVE banner stays up, in seconds (M11-30: was 3).</summary>
    public const float BannerSeconds = 6f;

    /// <summary>How long an OBJECTIVE COMPLETE or OBJECTIVE FAILED banner stays up, in seconds (M11-30).</summary>
    public const float ResultBannerSeconds = 4f;

    /// <summary>How long the minimap flash lasts, in seconds (M11-30: was 2).</summary>
    public const float FlashSeconds = 4f;

    /// <summary>The most objective rows the collapsed bar shows (M11-30).</summary>
    public const int MaxBarRows = 4;

    private readonly List<(string Id, ObjectiveStatus Status)> _known = new();
    private readonly Queue<(string Text, float Seconds)> _banners = new();
    private bool _observed;
    private string _selected = "";

    /// <summary>True when the full list and the briefing are shown (the O toggle).</summary>
    public bool Expanded { get; private set; }

    /// <summary>Banner text on screen ("NEW OBJECTIVE: ..." and the like), or empty when none is up.</summary>
    public string Banner { get; private set; } = "";

    /// <summary>Seconds left on the banner on screen.</summary>
    public float BannerLeft { get; private set; }

    /// <summary>Banners queued behind the one on screen (they show in order, one after another).</summary>
    public int BannersWaiting => _banners.Count;

    /// <summary>Seconds left on the minimap flash.</summary>
    public float FlashLeft { get; private set; }

    /// <summary>Id of an objective that arrived since the last <see cref="TakeArrival"/>, or empty.</summary>
    private string _arrival = "";

    /// <summary>Flips the full list and briefing on or off.</summary>
    public void ToggleExpanded() => Expanded = !Expanded;

    /// <summary>
    /// Looks at this frame's visible objectives: new ones and status changes queue a banner and start the flash; the clocks run down by
    /// <paramref name="dt"/>. <paramref name="announceFirst"/> says whether objectives present at the first look count as new (true at
    /// mission start, false when a save was loaded). <paramref name="label"/> turns objective text into display text.
    /// </summary>
    public void Observe(IReadOnlyList<MissionObjective> visible, float dt, bool announceFirst, Func<string, string> label)
    {
        BannerLeft = Math.Max(0f, BannerLeft - dt);
        FlashLeft = Math.Max(0f, FlashLeft - dt);
        NextBanner();
        foreach (MissionObjective o in visible)
        {
            int at = _known.FindIndex(k => k.Id == o.Id);
            if (at < 0)
            {
                _known.Add((o.Id, o.Status));
                if (_observed || announceFirst) Raise("NEW OBJECTIVE: " + label(o.Text), BannerSeconds, o.Id);
            }
            else if (_known[at].Status != o.Status)
            {
                _known[at] = (o.Id, o.Status);
                if (o.Status == ObjectiveStatus.Completed) Raise("OBJECTIVE COMPLETE: " + label(o.Text), ResultBannerSeconds, "");
                else if (o.Status == ObjectiveStatus.Failed) Raise("OBJECTIVE FAILED: " + label(o.Text), ResultBannerSeconds, "");
            }
        }

        _observed = true;
    }

    /// <summary>Shows the next queued banner once the one on screen has run out, and clears the banner when none is left.</summary>
    private void NextBanner()
    {
        while (BannerLeft <= 0f && _banners.Count > 0)
        {
            (string text, float seconds) = _banners.Dequeue();
            Banner = text;
            BannerLeft = seconds;
        }

        if (BannerLeft <= 0f) Banner = "";
    }

    private void Raise(string text, float seconds, string arrivalId)
    {
        FlashLeft = FlashSeconds;
        if (BannerLeft > 0f) _banners.Enqueue((text, seconds));   // one banner at a time; the others wait their turn
        else { Banner = text; BannerLeft = seconds; }
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

    /// <summary>
    /// The active objectives in bar order: primary (required) ones first, then optional ones, each group in list order. Tab and the
    /// highlight move through this order.
    /// </summary>
    public static List<MissionObjective> Ordered(IReadOnlyList<MissionObjective> visible) =>
        visible.Where(static o => o.Status == ObjectiveStatus.Active && o.Primary)
            .Concat(visible.Where(static o => o.Status == ObjectiveStatus.Active && !o.Primary))
            .ToList();

    /// <summary>The highlighted objective: the Tab selection while it is still active, else the first in bar order; null when none is active.</summary>
    public MissionObjective? Current(IReadOnlyList<MissionObjective> visible)
    {
        List<MissionObjective> active = Ordered(visible);
        return active.Find(o => o.Id == _selected) ?? (active.Count > 0 ? active[0] : null);
    }

    /// <summary>
    /// The objective the on-map guidance points at (diamond, edge arrow, minimap ping): always the highlighted one (M11-30). It may have no
    /// map location; the callers then draw nothing and the bar says "no map marker", so the guidance never points at a different objective.
    /// </summary>
    public MissionObjective? Guided(IReadOnlyList<MissionObjective> visible) => Current(visible);

    /// <summary>Tab: the next active objective in bar order becomes the highlight (wrapping). Returns it, or null when none is active.</summary>
    public MissionObjective? Cycle(IReadOnlyList<MissionObjective> visible)
    {
        List<MissionObjective> active = Ordered(visible);
        if (active.Count == 0) return null;
        MissionObjective? cur = Current(visible);
        int at = cur is null ? -1 : active.FindIndex(o => o.Id == cur.Id);
        MissionObjective next = active[(at + 1) % active.Count];
        _selected = next.Id;
        return next;
    }

    /// <summary>
    /// The objective rows of the collapsed bar (M11-30): every active objective, primary first, at most <see cref="MaxBarRows"/> rows. The
    /// highlighted row starts with "> " and the others with two spaces; optional rows say "(optional)"; the highlighted row says "(no map
    /// marker)" when it has no location. With more than four active, three rows are shown around the highlight and a last row counts the
    /// rest. With none active the single row says so.
    /// </summary>
    public List<string> BarRows(IReadOnlyList<MissionObjective> visible, Func<string, string> label, Func<MissionObjective, bool> hasCell)
    {
        List<MissionObjective> active = Ordered(visible);
        if (active.Count == 0) return new List<string> { visible.Count == 0 ? "No objectives" : "All objectives resolved" };
        string curId = Current(visible)?.Id ?? "";
        int shown = active.Count <= MaxBarRows ? active.Count : MaxBarRows - 1;
        int curAt = Math.Max(0, active.FindIndex(o => o.Id == curId));
        int first = Math.Clamp(curAt - 1, 0, active.Count - shown);
        var rows = new List<string>();
        for (int i = first; i < first + shown; i++)
        {
            MissionObjective o = active[i];
            bool hi = o.Id == curId;
            rows.Add((hi ? "> " : "  ") + (o.Primary ? "" : "(optional) ") + label(o.Text) + (hi && !hasCell(o) ? " (no map marker)" : ""));
        }

        if (shown < active.Count) rows.Add("  +" + (active.Count - shown) + " more active (Tab to cycle)");
        return rows;
    }
}
