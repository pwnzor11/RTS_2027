// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Llm;
using Rts.Llm;

namespace Rts.Game.Tests.Llm;

public sealed class CommanderChatModelTests
{
    private sealed class FakeChannel : ICommanderChannel
    {
        public FakeChannel(byte player, string speaker)
        {
            Player = player;
            Speaker = speaker;
        }

        public byte Player { get; }
        public string Speaker { get; }
        public bool Online => true;
        public List<(int Tick, string Text)> Heard { get; } = new();
        public void Hear(int tick, string text) => Heard.Add((tick, text));
    }

    private static readonly IReadOnlyList<ICommanderChannel> None = Array.Empty<ICommanderChannel>();

    private static void TypeAll(CommanderChatModel model, string text)
    {
        foreach (char c in text) model.Type(c);
    }

    [Fact]
    public void PlainTextGoesToTheOnlyCommander()
    {
        var a = new FakeChannel(1, "Hester");
        var sent = CommanderChatModel.Parse("  hold the ridge  ", new ICommanderChannel[] { a }, out string? notice);
        Assert.Null(notice);
        Assert.Same(a, sent!.Value.Channel);
        Assert.Equal("hold the ridge", sent.Value.Text);
    }

    [Fact]
    public void PlainTextGoesToTheFirstOfSeveralCommanders()
    {
        var a = new FakeChannel(1, "Hester");
        var b = new FakeChannel(2, "Okonkwo");
        var sent = CommanderChatModel.Parse("attack", new ICommanderChannel[] { a, b }, out _);
        Assert.Same(a, sent!.Value.Channel);
    }

    [Theory]
    [InlineData("/to 3 push north", 2, "push north")]
    [InlineData("/TO 2 hold", 1, "hold")]
    [InlineData("/to 1   spaced   out  ", 0, "spaced   out")]
    public void SlashToAddressesTheSlotShownInThePanel(string input, int player, string expectedText)
    {
        var a = new FakeChannel(0, "A");
        var b = new FakeChannel(1, "B");
        var c = new FakeChannel(2, "C");
        var all = new ICommanderChannel[] { a, b, c };
        var sent = CommanderChatModel.Parse(input, all, out string? notice);
        Assert.Null(notice);
        Assert.Same(all[player], sent!.Value.Channel);
        Assert.Equal(expectedText, sent.Value.Text);
    }

    [Theory]
    [InlineData("/to 9 hello")]
    [InlineData("/to 0 hello")]
    [InlineData("/to two hello")]
    [InlineData("/to")]
    public void UnknownSlotIsAComplaintNotASend(string input)
    {
        var a = new FakeChannel(0, "A");
        var sent = CommanderChatModel.Parse(input, new ICommanderChannel[] { a }, out string? notice);
        Assert.Null(sent);
        Assert.NotNull(notice);
    }

    [Fact]
    public void SlashToWithNoTextIsAComplaint()
    {
        var a = new FakeChannel(0, "A");
        Assert.Null(CommanderChatModel.Parse("/to 1   ", new ICommanderChannel[] { a }, out string? notice));
        Assert.Equal(CommanderChatText.EmptyMessage, notice);
    }

    [Fact]
    public void BlankInputSendsNothingAndIsNoComplaint()
    {
        var a = new FakeChannel(0, "A");
        Assert.Null(CommanderChatModel.Parse("   ", new ICommanderChannel[] { a }, out string? notice));
        Assert.Null(notice);
    }

    [Fact]
    public void NoCommandersMeansTheInputDoesNotOpen()
    {
        var model = new CommanderChatModel();
        Assert.False(model.Open(None));
        Assert.False(model.Typing);
        Assert.Null(CommanderChatModel.Parse("hi", None, out string? notice));
        Assert.Equal(CommanderChatText.NoCommander, notice);
    }

    [Fact]
    public void SubmitHearsTheTextAtTheTickAndCloses()
    {
        var a = new FakeChannel(0, "A");
        var b = new FakeChannel(1, "B");
        var all = new ICommanderChannel[] { a, b };
        var model = new CommanderChatModel();
        Assert.True(model.Open(all));
        TypeAll(model, "/to 2 fall back");

        Assert.True(model.Submit(all, 1234));

        Assert.False(model.Typing);
        Assert.Equal("", model.Input);
        Assert.Empty(a.Heard);
        Assert.Equal(new[] { (1234, "fall back") }, b.Heard);
    }

    [Fact]
    public void FailedSubmitKeepsTypingWithANotice()
    {
        var a = new FakeChannel(0, "A");
        var all = new ICommanderChannel[] { a };
        var model = new CommanderChatModel();
        model.Open(all);
        TypeAll(model, "/to 7 hi");

        Assert.False(model.Submit(all, 10));

        Assert.True(model.Typing);
        Assert.NotNull(model.Notice);
        Assert.Empty(a.Heard);
    }

    [Fact]
    public void TypingIsCappedAtTwoHundredCharactersAndDropsControlCharacters()
    {
        var model = new CommanderChatModel();
        model.Open(new ICommanderChannel[] { new FakeChannel(0, "A") });
        TypeAll(model, new string('x', 250));
        model.Type('\r');
        model.Type('\n');
        Assert.Equal(CommanderChatModel.MaxInputChars, model.Input.Length);
    }

    [Fact]
    public void BackspaceAndCancel()
    {
        var model = new CommanderChatModel();
        model.Open(new ICommanderChannel[] { new FakeChannel(0, "A") });
        TypeAll(model, "abc");
        model.Backspace();
        Assert.Equal("ab", model.Input);
        model.Cancel();
        Assert.False(model.Typing);
        Assert.Equal("", model.Input);
    }

    [Fact]
    public void TypingIgnoresKeysWhileClosed()
    {
        var model = new CommanderChatModel();
        model.Type('a');
        Assert.Equal("", model.Input);
    }

    [Fact]
    public void ClosedPanelFadesLinesOlderThanTwentySeconds()
    {
        var log = new CommanderChatLog();
        log.Add(new CommanderLine(0, 1, "A", "old"));
        log.Add(new CommanderLine(300, 1, "A", "recent"));
        var model = new CommanderChatModel();
        model.Refresh(log);

        var shown = new List<CommanderLine>();
        model.VisibleLines(400, shown);
        Assert.Equal(new[] { "old", "recent" }, shown.Select(l => l.Text));

        model.VisibleLines(401, shown);
        Assert.Equal(new[] { "recent" }, shown.Select(l => l.Text));
    }

    [Fact]
    public void TypingShowsTheLastEightLinesWhateverTheirAge()
    {
        var log = new CommanderChatLog();
        for (int i = 0; i < 12; i++) log.Add(new CommanderLine(i, 1, "A", "line " + i));
        var model = new CommanderChatModel();
        model.Refresh(log);
        model.Open(new ICommanderChannel[] { new FakeChannel(1, "A") });

        var shown = new List<CommanderLine>();
        model.VisibleLines(100_000, shown);
        Assert.Equal(CommanderChatModel.TypingLineCount, shown.Count);
        Assert.Equal("line 4", shown[0].Text);
        Assert.Equal("line 11", shown[^1].Text);
    }

    [Fact]
    public void RefreshCopiesOnlyWhenTheLogChanged()
    {
        var log = new CommanderChatLog();
        var model = new CommanderChatModel();
        model.Refresh(log);
        Assert.Empty(model.Lines);

        log.Add(new CommanderLine(1, 0, "A", "one"));
        model.Refresh(log);
        Assert.Single(model.Lines);

        log.Add(new CommanderLine(2, 0, "A", "two"));
        model.Refresh(log);
        Assert.Equal(2, model.Lines.Count);
    }

    [Fact]
    public void WrapBreaksAtSpacesWithinTheWidth()
    {
        // Each character is one pixel wide in this measure.
        IReadOnlyList<string> rows = CommanderChatModel.Wrap("the quick brown fox", 10, s => s.Length);
        Assert.Equal(new[] { "the quick", "brown fox" }, rows);
    }

    [Fact]
    public void WrapCutsAWordWiderThanTheRow()
    {
        IReadOnlyList<string> rows = CommanderChatModel.Wrap("abcdefghij", 4, s => s.Length);
        Assert.Equal(new[] { "abcd", "efgh", "ij" }, rows);
    }

    [Fact]
    public void WrapOfEmptyTextIsOneEmptyRow()
    {
        IReadOnlyList<string> rows = CommanderChatModel.Wrap("", 10, s => s.Length);
        Assert.Single(rows);
        Assert.Equal("", rows[0]);
    }
}
