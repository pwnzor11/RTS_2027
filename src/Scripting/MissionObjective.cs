// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>One objective as the UI shows it.</summary>
/// <param name="Id">Script id.</param>
/// <param name="Text">Display text.</param>
/// <param name="Primary">True for a primary objective.</param>
/// <param name="Status">Current status.</param>
/// <param name="Hidden">True until the script reveals it (M8-03); the UI must not list hidden objectives. Always false for schema-1 missions.</param>
public sealed record MissionObjective(string Id, string Text, bool Primary, ObjectiveStatus Status, bool Hidden = false);
