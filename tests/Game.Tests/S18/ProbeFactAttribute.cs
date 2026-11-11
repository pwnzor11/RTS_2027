// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Xunit;

namespace Rts.Game.Tests.S18;

/// <summary>
/// S18 TESTS-REPO-05: a probe or proof that only runs when an environment variable is set. Without it the test is reported as
/// SKIPPED with the reason, instead of passing without checking anything.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ProbeFactAttribute : FactAttribute
{
    public ProbeFactAttribute(string environmentVariable)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(environmentVariable)))
            Skip = $"probe: set {environmentVariable} to run it (it does not assert anything in the normal suite)";
    }
}
