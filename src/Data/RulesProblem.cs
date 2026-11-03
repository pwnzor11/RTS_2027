// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>One validation problem: the file it was found in, the dotted key, and what is wrong.</summary>
public sealed record RulesProblem(string File, string Key, string Message);
