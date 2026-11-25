// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Game.Lan;
using Rts.Sim.Core;

namespace Rts.Game.Tests.A43Qol;

/// <summary>A43-QOL-09: the ping message and the board that shows it.</summary>
public sealed class PingTests
{
    [Fact]
    public void A_ping_round_trips_through_its_text()
    {
        string text = LanPing.Encode(new CellCoord(37, 112));
        Assert.True(LanPing.IsPing(text));
        Assert.True(LanPing.TryDecode(text, out CellCoord cell));
        Assert.Equal(new CellCoord(37, 112), cell);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("1,2")]
    [InlineData("\u0001")]
    [InlineData("\u00013,")]
    [InlineData("\u0001,4")]
    [InlineData("\u0001-3,4")]
    [InlineData("\u0001a,b")]
    [InlineData("\u000199999,2")]
    [InlineData("")]
    public void Ordinary_or_malformed_text_does_not_decode(string text)
    {
        Assert.False(LanPing.TryDecode(text, out _));
    }

    [Fact]
    public void Ordinary_chat_is_never_a_ping_and_the_chat_box_cannot_type_the_prefix()
    {
        Assert.False(LanPing.IsPing("attack 3,4"));
        var input = new LanChatInput();
        input.Open();
        input.Type(LanPing.Prefix);
        input.Type('x');
        Assert.Equal("x", input.Text);
    }

    [Fact]
    public void A_ping_text_stays_inside_the_chat_length_limit()
    {
        Assert.True(LanPing.Encode(new CellCoord(4096, 4096)).Length <= LanChatInput.MaxChars);
    }

    [Fact]
    public void One_ping_per_sender_lasts_thirty_seconds()
    {
        var board = new PingBoard();
        board.Add(new CellCoord(1, 1), 0, 0);
        board.Add(new CellCoord(2, 2), 1, 1_000);
        board.Add(new CellCoord(3, 3), 0, 5_000);   // replaces player 0's first ping
        Assert.Equal(2, board.Pings.Count);
        Assert.Contains(board.Pings, p => p.Player == 0 && p.Cell == new CellCoord(3, 3));
        board.Prune(PingBoard.LifeMs + 999);
        Assert.Equal(2, board.Pings.Count);
        board.Prune(PingBoard.LifeMs + 1_000);
        Assert.Single(board.Pings);
        board.Prune(PingBoard.LifeMs + 5_000);
        Assert.Empty(board.Pings);
    }
}
