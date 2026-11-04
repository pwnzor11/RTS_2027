// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>How bad a <see cref="MapIssue"/> is (M4): a map with errors cannot be saved from the editor or played. Append only.</summary>
public enum MapIssueSeverity : byte { Warning = 0, Error = 1 }
