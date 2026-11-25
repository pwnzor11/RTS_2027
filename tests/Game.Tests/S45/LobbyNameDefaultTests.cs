// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Lan.Lobby;
using Xunit;

namespace Rts.Game.Tests.S45;

/// <summary>S45 N-04: with no PlayerName set, the lobby and discovery name is "Player", never the Windows user name.</summary>
public sealed class LobbyNameDefaultTests
{
    [Fact]
    public void EmptySetting_ResolvesToPlayer_NotTheOsUserName()
    {
        Assert.Equal(LobbyNames.Fallback, LobbyNames.Resolve(null));
        Assert.Equal(LobbyNames.Fallback, LobbyNames.Resolve("   "));
    }
}
