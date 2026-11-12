// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Lan;

/// <summary>
/// The user-visible strings of an in-game LAN match (M9-07, M9-08, M9-10), kept together as <c>CommanderChatText</c> keeps the commander
/// chat's. The bitmap font is ASCII, so "..." stands for an ellipsis. Placeholders are filled with <see cref="string.Replace(string, string)"/>.
/// </summary>
public static class LanText
{
    /// <summary>Waiting overlay. {name} = the peer, {time} = m:ss.</summary>
    public const string Waiting = "Waiting for {name}... {time}";

    /// <summary>The host's button on the waiting overlay. {name} = the peer.</summary>
    public const string Drop = "Drop {name}";

    /// <summary>Pause overlay. {name} = who paused.</summary>
    public const string PausedBy = "Paused by {name}";

    /// <summary>The resume button on the pause overlay.</summary>
    public const string Resume = "Resume";

    /// <summary>Resume countdown. {seconds} = whole seconds left.</summary>
    public const string ResumingIn = "Resuming in {seconds}";

    /// <summary>Shown to everyone when a peer leaves or is dropped. {name} = the peer.</summary>
    public const string PeerLeft = "{name} left; an AI takes over";

    /// <summary>Shown when a spectator leaves. {name} = the spectator.</summary>
    public const string SpectatorLeft = "{name} stopped watching";

    /// <summary>Shown on a peer the host dropped.</summary>
    public const string YouWereDropped = "You were dropped from the match. You can keep watching.";

    /// <summary>The host's desync dialog title. {tick} = the tick.</summary>
    public const string DesyncHost = "Out of sync at tick {tick}";

    /// <summary>The desync dialog line naming the report folder. {folder} = the path.</summary>
    public const string DesyncReport = "Report: {folder}";

    /// <summary>A client's desync notice.</summary>
    public const string DesyncClient = "Out of sync: waiting for the host";

    /// <summary>The host's desync choices.</summary>
    public const string ResyncFromHost = "Resync from host";

    /// <summary>The host's desync choices.</summary>
    public const string Continue = "Continue";

    /// <summary>The host's desync choices.</summary>
    public const string EndMatch = "End match";

    /// <summary>Shown on every peer while a resync runs.</summary>
    public const string Resyncing = "Resyncing from the host...";

    /// <summary>Shown after a resync loaded.</summary>
    public const string Resynced = "Back in sync with the host";

    /// <summary>A client's dialog when the host is gone (Q-M9-j).</summary>
    public const string HostLeft = "The host left the game.";

    /// <summary>The host-left choices.</summary>
    public const string ContinueAgainstAi = "Continue against AI";

    /// <summary>The host-left choices.</summary>
    public const string BackToMenu = "Back to menu";

    /// <summary>The chat input prompt to everyone.</summary>
    public const string ToAll = "To all: ";

    /// <summary>The chat input prompt to the team.</summary>
    public const string ToTeam = "To team: ";

    /// <summary>The chat input hint.</summary>
    public const string ChatHint = "Enter send, Tab all/team, Esc cancel";

    /// <summary>Prefix of a team chat line.</summary>
    public const string TeamTag = "[Team] ";

    /// <summary>Name shown for a peer the setup does not name. {peer} = its id.</summary>
    public const string UnknownPeer = "Player {peer}";

    /// <summary>The LAN menu (Esc): it opens over the running match, which never pauses for it.</summary>
    public const string MenuTitle = "LAN match";

    /// <summary>The LAN menu items.</summary>
    public const string MenuResume = "Back to the game";

    /// <summary>The LAN menu items.</summary>
    public const string MenuControls = "Controls";

    /// <summary>The LAN menu items.</summary>
    public const string MenuLeave = "Leave match";

    /// <summary>Save slot name of the "Continue against AI" skirmish save.</summary>
    public const string ContinueSlot = "lan-continue";

    /// <summary>Notice when the continue save could not be written or loaded. {reason} = why.</summary>
    public const string ContinueFailed = "Could not continue against AI: {reason}";

    /// <summary>Bye reason the host sends when it leaves or ends the match.</summary>
    public const string HostClosed = "host left";

    /// <summary>The Bye reason the host sends when its own side of the match could not start.</summary>
    public const string HostStartFailed = "The host could not start the match.";

    /// <summary>Bye reason a client sends when it leaves.</summary>
    public const string ClientClosed = "left";

    /// <summary>Formats a time in milliseconds as m:ss.</summary>
    public static string Clock(int ms)
    {
        int seconds = Math.Max(0, ms) / 1000;
        return $"{seconds / 60}:{seconds % 60:00}";
    }
}
