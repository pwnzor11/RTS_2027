// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Update;

/// <summary>A newer release found on GitHub.</summary>
/// <param name="Version">The release version without the leading "v", for example "0.2.0".</param>
/// <param name="Url">The release page address.</param>
public sealed record UpdateInfo(string Version, string Url);
