// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Screens.Editor;

/// <summary>One line of the validation panel.</summary>
/// <param name="Severity">Error or warning.</param>
/// <param name="Code">The validator's issue code.</param>
/// <param name="Text">Display text, e.g. "E start_space (12, 40): no clear 5x5".</param>
/// <param name="Cell">Where to jump to when the row is clicked, or null for a map-wide issue.</param>
public sealed record ValidationRow(MapIssueSeverity Severity, string Code, string Text, CellCoord? Cell);

/// <summary>The validator's issues arranged for the editor panel: errors first, then warnings, each group in validator order.</summary>
public sealed class ValidationReport
{
    /// <summary>Runs <paramref name="validator"/> (default <see cref="MapValidator.Validate"/>) on the document's current state.</summary>
    public static ValidationReport Run(EditorDocument document, TerrainTable? terrain = null,
        Func<MapData, MapMeta?, TerrainTable?, IReadOnlyList<MapIssue>>? validator = null)
    {
        validator ??= MapValidator.Validate;
        return From(validator(document.ToMapData(), document.ToMeta(), terrain));
    }

    /// <summary>Arranges already-computed issues (used by tests with a fake validator result).</summary>
    public static ValidationReport From(IReadOnlyList<MapIssue> issues)
    {
        var rows = new List<ValidationRow>();
        foreach (MapIssueSeverity sev in new[] { MapIssueSeverity.Error, MapIssueSeverity.Warning })
            foreach (MapIssue i in issues.Where(i => i.Severity == sev))
            {
                string at = i.Cell is { } c ? $" ({c.X}, {c.Y})" : "";
                rows.Add(new ValidationRow(sev, i.Code, $"{(sev == MapIssueSeverity.Error ? "E" : "W")} {i.Code}{at}: {i.Message}", i.Cell));
            }
        return new ValidationReport(rows);
    }

    private ValidationReport(IReadOnlyList<ValidationRow> rows)
    {
        Rows = rows;
        ErrorCount = rows.Count(r => r.Severity == MapIssueSeverity.Error);
        WarningCount = rows.Count - ErrorCount;
    }

    /// <summary>The rows, errors first.</summary>
    public IReadOnlyList<ValidationRow> Rows { get; }

    /// <summary>Number of errors.</summary>
    public int ErrorCount { get; }

    /// <summary>Number of warnings.</summary>
    public int WarningCount { get; }

    /// <summary>Saving is allowed with warnings but not with errors (design 08 §5).</summary>
    public bool CanSave => ErrorCount == 0;

    /// <summary>One-line summary for the panel header.</summary>
    public string Summary => Rows.Count == 0 ? "No issues" : $"{ErrorCount} error(s), {WarningCount} warning(s)";
}
