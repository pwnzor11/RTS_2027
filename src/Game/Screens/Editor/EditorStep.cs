// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Screens.Editor;

/// <summary>One cell's before and after in an <see cref="EditorStep"/>.</summary>
public readonly record struct CellEdit(int Index, LandType OldLand, LandType NewLand, byte OldHeight, byte NewHeight,
    OverlayKind OldOverlay, OverlayKind NewOverlay, byte OldDensity, byte NewDensity, byte OldDecor = 0, byte NewDecor = 0);

/// <summary>One undoable step: a whole stroke or click. Starts and features are stored as full before/after lists (they are tiny).</summary>
public sealed class EditorStep
{
    /// <summary>Creates a step.</summary>
    public EditorStep(string label, IReadOnlyList<CellEdit> cells, IReadOnlyList<CellCoord>? startsBefore, IReadOnlyList<CellCoord>? startsAfter,
        IReadOnlyList<MapFeature>? featuresBefore, IReadOnlyList<MapFeature>? featuresAfter)
    {
        Label = label; Cells = cells; StartsBefore = startsBefore; StartsAfter = startsAfter;
        FeaturesBefore = featuresBefore; FeaturesAfter = featuresAfter;
    }

    /// <summary>Short name for the status line ("Paint land", "Place start").</summary>
    public string Label { get; }

    /// <summary>Cell changes in the order they were made.</summary>
    public IReadOnlyList<CellEdit> Cells { get; }

    /// <summary>Starts before / after, or null when the step did not touch starts.</summary>
    public IReadOnlyList<CellCoord>? StartsBefore { get; }

    /// <summary>See <see cref="StartsBefore"/>.</summary>
    public IReadOnlyList<CellCoord>? StartsAfter { get; }

    /// <summary>Features before / after, or null when the step did not touch features.</summary>
    public IReadOnlyList<MapFeature>? FeaturesBefore { get; }

    /// <summary>See <see cref="FeaturesBefore"/>.</summary>
    public IReadOnlyList<MapFeature>? FeaturesAfter { get; }

    /// <summary>True when the step changes nothing.</summary>
    public bool IsEmpty => Cells.Count == 0 && StartsAfter is null && FeaturesAfter is null;
}
