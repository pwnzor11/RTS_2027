// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Render;

/// <summary>One resource cell the survey overlay marks: its place, field kind, raw density and the 1..3 shade drawn.</summary>
public readonly record struct RichnessCell(CellCoord Cell, OverlayKind Kind, int Density, int Shade);

/// <summary>A selected Survey Hover: where it is and how far it sees, in cells.</summary>
public readonly record struct SurveySource(CellCoord Cell, int SightCells);

/// <summary>
/// S44 (M-14, design 07): while a friendly Survey Hover is selected, the ash and pitchblende fields inside its sight show their richness
/// as three shades (thin, fair, rich) plus one to three pips. Display only: it reads the cell density the player's own sight already shows
/// and never marks a cell the viewer cannot see now. The model (<see cref="Plan"/>) is pure; <see cref="Draw"/> only paints it.
/// </summary>
public static class SurveyOverlay
{
    /// <summary>The rules id of the survey vehicle.</summary>
    public const string SurveyId = "exp_survey_hover";

    private static readonly Color AshTint = new(0xF2, 0xB8, 0x3A), PitchTint = new(0x5F, 0xE0, 0xC8), PipColour = new(0xFF, 0xFF, 0xFF);
    private static readonly byte[] ShadeAlpha = { 0, 70, 120, 170 };
    private static readonly List<RichnessCell> Scratch = new();

    /// <summary>Whether a vehicle type is the Survey Hover.</summary>
    public static bool IsSurvey(VehicleType type) => type.Id == SurveyId;

    /// <summary>The shade of a density: 1 for the lowest third of 1..<see cref="EconomyState.MaxDensity"/>, 2 the middle, 3 the richest; 0 for none.</summary>
    public static int ShadeOf(int density)
    {
        if (density <= 0) return 0;
        int third = (EconomyState.MaxDensity + 2) / 3;
        return Math.Min(3, (density - 1) / third + 1);
    }

    /// <summary>
    /// Fills <paramref name="dest"/> (cleared first, row-major order) with the resource cells within the sight circle of any source that the
    /// viewer sees now (<paramref name="visible"/>). Cells with no density are skipped.
    /// </summary>
    public static void Plan(MapState map, IReadOnlyList<SurveySource> sources, Func<CellCoord, bool> visible, List<RichnessCell> dest)
    {
        dest.Clear();
        if (sources.Count == 0) return;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var c = new CellCoord(x, y);
                OverlayKind kind = map.OverlayAt(c);
                if (!OverlayKinds.IsResource(kind)) continue;
                int density = map.DensityAt(c);
                if (density <= 0 || !InSight(sources, c) || !visible(c)) continue;
                dest.Add(new RichnessCell(c, kind, density, ShadeOf(density)));
            }
        }
    }

    private static bool InSight(IReadOnlyList<SurveySource> sources, CellCoord c)
    {
        foreach (SurveySource s in sources)
        {
            int dx = c.X - s.Cell.X, dy = c.Y - s.Cell.Y;
            if (dx * dx + dy * dy <= s.SightCells * s.SightCells) return true;
        }
        return false;
    }

    /// <summary>The selected, living Survey Hovers of the frame as sources (empty without a sim or a selected survey).</summary>
    public static List<SurveySource> Sources(SceneFrame frame)
    {
        var list = new List<SurveySource>();
        if (frame.Sim is not { } sim) return list;
        SimRules rules = sim.Setup.Rules;
        foreach (UnitKey key in frame.Selection.Units)
        {
            if (key.Kind != ActorKind.Vehicle || !sim.Units.Slots.IsAlive(key.Id)) continue;
            VehicleType type = rules.Vehicles[sim.Units.TypeIndex[key.Id.Index]];
            if (IsSurvey(type)) list.Add(new SurveySource(sim.Units.Position[key.Id.Index].ToCell(), type.Sight));
        }
        return list;
    }

    /// <summary>Draws the richness shades over the fields in sight of the selected Survey Hovers.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera)
    {
        if (frame.Sim is not { } sim) return;
        List<SurveySource> sources = Sources(frame);
        if (sources.Count == 0) return;
        int viewer = frame.Snapshot.Viewer;
        PlayerView view = sim.ViewFor((byte)Math.Max(0, viewer));
        Plan(sim.Map, sources, c => viewer < 0 || view.IsVisible(c), Scratch);
        foreach (RichnessCell cell in Scratch)
        {
            Rectangle r = camera.ScreenRect(cell.Cell.X * Camera.CellPx, cell.Cell.Y * Camera.CellPx,
                (cell.Cell.X + 1) * Camera.CellPx, (cell.Cell.Y + 1) * Camera.CellPx);
            Shapes.Fill(batch, pixel, r, Shapes.Alpha(cell.Kind == OverlayKind.Ashfall ? AshTint : PitchTint, ShadeAlpha[cell.Shade]));
            int pip = Math.Max(2, r.Width / 12);
            for (int k = 0; k < cell.Shade; k++)
                Shapes.Fill(batch, pixel, new Rectangle(r.X + pip * (1 + 2 * k), r.Bottom - pip * 2, pip, pip), PipColour);
        }
    }
}
