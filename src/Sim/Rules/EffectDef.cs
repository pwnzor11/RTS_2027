// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Effect entry (design 01 §6). Op: enable|set|add|mul; Target: a type id or "class:&lt;name&gt;".
/// Parsed and validated in M1; applied by the tech system later (M2/M3). Value is an integer (mul is per-mille).</summary>
public sealed record EffectDef(string Id, string Op, string Target, string Attr, int Value);
