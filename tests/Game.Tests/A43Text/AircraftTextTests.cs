// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 ROSTER-AD-01 / -02: Authority aircraft are not fast (speed 8), and the Bomber is not the most expensive aircraft (cost 600, as the Interceptor).</summary>
public sealed class AircraftTextTests
{
    private static ArchiveEntry Entry(string id) => ArchiveTestData.Catalog().Find(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void Interceptor_text_does_not_call_it_fast()
    {
        Assert.DoesNotContain("It is fast", Entry("auth_interceptor").Body);
    }

    [Fact]
    public void Dirigible_text_does_not_call_it_slow_when_it_is_the_fastest_directorate_air_unit()
    {
        Assert.DoesNotContain("slow, fragile", Entry("dir_dirigible").Body);
    }

    [Fact]
    public void Bomber_text_does_not_claim_the_most_expensive_aircraft()
    {
        Assert.DoesNotContain("most expensive", Entry("auth_bomber").Body);
    }
}
