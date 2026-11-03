// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Thrown when rules data cannot be loaded. <see cref="Problems"/> lists every issue found, not just the first.</summary>
public sealed class RulesException : Exception
{
    /// <summary>Single-problem form.</summary>
    public RulesException(string file, string key, string message)
        : this([new RulesProblem(file, key, message)]) { }

    /// <summary>All problems found while loading.</summary>
    public RulesException(IReadOnlyList<RulesProblem> problems)
        : base(Format(problems))
    {
        Problems = problems;
    }

    /// <summary>Every problem found, in discovery order.</summary>
    public IReadOnlyList<RulesProblem> Problems { get; }

    private static string Format(IReadOnlyList<RulesProblem> problems) =>
        string.Join(Environment.NewLine, problems.Select(p => $"{p.File}: {p.Key}: {p.Message}"));
}
