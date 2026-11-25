// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System;
using System.Collections.Generic;
using System.Linq;
using Rts.Game.Render;

namespace Rts.Game.Tests;

/// <summary>S43 art pass: round decals (blob, soft) have noise-broken outlines, not circles; the shape is deterministic per decal.</summary>
public sealed class DecorShapeTests
{
    [Fact]
    public void RoundDecalOutlinesAreNoiseBrokenAndDeterministic()
    {
        const int r = 10;
        const ulong seed = 0x9E3779B97F4A7C15UL;
        foreach (DecalShape shape in new[] { DecalShape.Blob, DecalShape.Soft })
        {
            double bestRatio = 0;
            for (int cell = 0; cell < 4; cell++)
            {
                var radii = new List<double>();
                for (int a = 0; a < 32; a++)
                {
                    double ang = a * Math.PI / 16.0;
                    double rim = 0;
                    for (double t = 0.5; t <= 20; t += 0.5)
                    {
                        int x = (int)Math.Round(t * Math.Cos(ang)), y = (int)Math.Round(t * Math.Sin(ang));
                        if (DecorBake.RoundCoverage(shape, x, y, r, seed, cell, 3 + cell) > 0) rim = t;
                    }

                    radii.Add(rim);
                    // Same inputs, same coverage: the outline is a pure function of the decal.
                    int probe = DecorBake.RoundCoverage(shape, 3, -2, r, seed, cell, 3 + cell);
                    Assert.Equal(probe, DecorBake.RoundCoverage(shape, 3, -2, r, seed, cell, 3 + cell));
                }

                Assert.True(radii.Min() > 0, "every ray should reach a rim");
                bestRatio = Math.Max(bestRatio, radii.Max() / radii.Min());
            }

            Assert.True(bestRatio >= 1.25, $"{shape} rim should vary by at least 25 % around its outline (best ratio {bestRatio:F2})");
        }
    }
}
