// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Lan;

/// <summary>Where a <see cref="LanSession"/> writes and what it may use (M9-07). The game passes the defaults; tests pass a temp folder.</summary>
/// <param name="AppSettings">The app settings, for the host's LLM commanders (Q-M9-f); ignored on clients. Null = no commanders.</param>
/// <param name="ReplayPath">Where the match replay is recorded (the previous one is kept beside it as <c>.prev</c>); null = not recorded.</param>
/// <param name="DesyncRoot">The desync report root (<see cref="LanDesyncReport.DefaultRoot"/> in the game).</param>
public sealed record LanSessionOptions(Settings? AppSettings, string? ReplayPath, string DesyncRoot)
{
    /// <summary>Where a LAN match records its replay: the replays folder, named so the replay list shows it as a LAN game.</summary>
    public const string DefaultReplayPath = "replays/lan_last.rpl";

    /// <summary>The options the game uses.</summary>
    public static LanSessionOptions ForGame(Settings appSettings) => new(appSettings, DefaultReplayPath, LanDesyncReport.DefaultRoot);
}
