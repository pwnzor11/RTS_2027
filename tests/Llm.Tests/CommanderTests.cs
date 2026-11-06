// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;
using Rts.Sim;
using Rts.Sim.Core;

namespace Llm.Tests;

using Rts.Llm.Tests;

public class CommanderTests
{
    private const string Allin =
        "{\"directive\":{\"posture\":\"allin\",\"focus\":\"army\",\"target\":1,\"mix\":\"vehicles\",\"expand\":true,\"focus_cell\":[20,22]},\"say\":\"Forward.\"}";

    /// <summary>A provider whose calls are completed by hand.</summary>
    private sealed class ManualProvider : IChatProvider
    {
        public readonly List<TaskCompletionSource<ChatResult>> Calls = new();
        public readonly List<ChatRequest> Requests = new();
        public bool Cancelled;
        public string Name => "Manual";

        public Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<ChatResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => { Cancelled = true; tcs.TrySetCanceled(); });
            lock (Calls) { Calls.Add(tcs); Requests.Add(request); }
            return tcs.Task;
        }

        public int Count { get { lock (Calls) return Calls.Count; } }

        /// <summary>Waits (bounded) until at least <paramref name="n"/> calls reached the provider.</summary>
        public async Task WaitForCount(int n)
        {
            for (int i = 0; i < 500 && Count < n; i++) await Task.Delay(2);
        }
    }

    private sealed class Rig
    {
        public readonly Simulation Sim = SummaryRig.Sim();
        public readonly CommanderChatLog Chat = new();
        public readonly List<(int Tick, int Arg, CellCoord Cell)> Issued = new();

        /// <summary>False for hand-completed providers: their call never finishes by itself, so waiting for it would hang.</summary>
        public bool WaitForCalls = true;

        public Rig()
        {
            Sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
            Sim.SpawnInfantry(1, SummaryRig.Infantry("dir_conscript"), new CellCoord(50, 50));
        }

        public Commander Make(IChatProvider? p, int cap = 0, bool chatOn = true) =>
            new(0, p, new CommanderPersona("t", "Authority", "Brig. Test", "You are gruff."), "Authority", cap, chatOn, Chat);

        /// <summary>Steps the sim to <paramref name="tick"/>, polling every tick; waits for fake calls so replies land on the next poll.</summary>
        public async Task RunTo(Commander c, int tick)
        {
            while (Sim.Tick < tick)
            {
                SummaryRig.Run(Sim, 1);
                if (c.Poll(Sim.ViewFor(0), out int arg, out CellCoord cell)) Issued.Add((Sim.Tick, arg, cell));
                if (WaitForCalls && c.LastCall is { } t) await t.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }

        public List<CommanderLine> Lines()
        {
            var l = new List<CommanderLine>();
            Chat.CopyTo(l);
            return l;
        }
    }

    private static FakeChatProvider Fake(params string[] replies)
    {
        var f = new FakeChatProvider();
        foreach (string r in replies) f.EnqueueText(r);
        return f;
    }

    [Fact]
    public async Task NoCallBeforeTick600ThenOnePerCadence()
    {
        var rig = new Rig();
        var fake = Fake(Allin, Allin, Allin);
        using Commander c = rig.Make(fake);
        await rig.RunTo(c, 599);
        Assert.Empty(fake.Requests);
        await rig.RunTo(c, 600);
        Assert.Single(fake.Requests);
        await rig.RunTo(c, 1199);
        Assert.Single(fake.Requests);
        await rig.RunTo(c, 1250);
        Assert.Equal(2, fake.Requests.Count);
        Assert.Equal(2, c.CallsMade);
    }

    [Fact]
    public async Task OneInFlightAndAStaleReplyIsDropped()
    {
        var rig = new Rig();
        var manual = new ManualProvider();
        rig.WaitForCalls = false;
        using Commander c = rig.Make(manual);
        await rig.RunTo(c, 1500);
        await manual.WaitForCount(1);
        Assert.Equal(1, manual.Count);                       // 1200 passed without a second call
        manual.Calls[0].SetResult(new ChatResult(true, Allin, null, 0, 0));
        await c.LastCall!.WaitAsync(TimeSpan.FromSeconds(10));
        await rig.RunTo(c, 1501);
        Assert.Empty(rig.Issued);                            // requested at 600, now 1501: stale
        Assert.Empty(rig.Lines());                           // a stale reply's say is dropped too
        await manual.WaitForCount(2);
        Assert.Equal(2, manual.Count);                       // the next decision started right away
    }

    [Fact]
    public async Task ReplyGivesPackedDirectiveAndCell()
    {
        var rig = new Rig();
        using Commander c = rig.Make(Fake(Allin));
        await rig.RunTo(c, 602);
        var (tick, arg, cell) = Assert.Single(rig.Issued);
        Assert.Equal(601, tick);
        Assert.Equal(DirectiveCodec.Pack(DirectiveCodec.PostureAllIn, DirectiveCodec.FocusArmy, 1, DirectiveCodec.MixVehicles, true), arg);
        Assert.Equal(new CellCoord(20, 22), cell);
        CommanderLine line = Assert.Single(rig.Lines());
        Assert.Equal("Forward.", line.Text);
        Assert.Equal("Brig. Test", line.Speaker);
        Assert.False(line.System);
    }

    [Fact]
    public async Task DirectiveWithoutCellGivesNoCellAndChatOffHidesSay()
    {
        var rig = new Rig();
        using Commander c = rig.Make(Fake("{\"directive\":{\"posture\":\"turtle\"},\"say\":\"Hold.\"}"), chatOn: false);
        await rig.RunTo(c, 602);
        var (_, _, cell) = Assert.Single(rig.Issued);
        Assert.Equal(Directive.NoCell, cell);
        Assert.Empty(rig.Lines());
    }

    [Fact]
    public async Task ParseFailureKeepsTheLastDirective()
    {
        var rig = new Rig();
        using Commander c = rig.Make(Fake("I refuse to answer in JSON."));
        await rig.RunTo(c, 650);
        Assert.Empty(rig.Issued);
        Assert.True(c.Online);
        Assert.Empty(rig.Lines());
    }

    [Fact]
    public async Task LastDirectivesGoIntoTheNextPrompt()
    {
        var rig = new Rig();
        var fake = Fake(Allin, Allin);
        using Commander c = rig.Make(fake);
        await rig.RunTo(c, 1201);
        Assert.Contains("YOUR LAST DIRECTIVES", fake.Requests[1].Messages[0].Text);
        Assert.DoesNotContain("YOUR LAST DIRECTIVES", fake.Requests[0].Messages[0].Text);
        Assert.Equal(CommanderPrompt.MaxTokens, fake.Requests[0].MaxTokens);
    }

    [Fact]
    public async Task FailureGivesOneOfflineLineThenRecoveryThenAnotherLine()
    {
        var rig = new Rig();
        var fake = new FakeChatProvider();
        fake.Enqueue(new ChatResult(false, "", "boom", 0, 0));
        fake.Enqueue(new ChatResult(false, "", "boom", 0, 0));
        fake.EnqueueText(Allin);
        fake.Enqueue(new ChatResult(false, "", "boom", 0, 0));
        using Commander c = rig.Make(fake);

        await rig.RunTo(c, 601);
        Assert.False(c.Online);
        await rig.RunTo(c, 1201);
        Assert.False(c.Online);
        Assert.Single(rig.Lines(), l => l.System);           // not posted again while still offline
        Assert.Contains("offline", rig.Lines()[0].Text);
        Assert.Contains("Brig. Test", rig.Lines()[0].Text);
        Assert.Empty(rig.Issued);                            // still trying at cadence: 3rd call at 1800

        await rig.RunTo(c, 1801);
        Assert.True(c.Online);
        Assert.Single(rig.Issued);

        await rig.RunTo(c, 2401);
        Assert.False(c.Online);
        Assert.Equal(2, rig.Lines().Count(l => l.System));   // posted again only after a success
    }

    [Fact]
    public async Task NoProviderIsOfflineWithOneLineAndNoCalls()
    {
        var rig = new Rig();
        using Commander c = rig.Make(null);
        Assert.False(c.Online);
        await rig.RunTo(c, 2000);
        CommanderLine l = Assert.Single(rig.Lines());
        Assert.True(l.System);
        Assert.Equal(0, c.CallsMade);
    }

    [Fact]
    public async Task CallCapStopsCallsWithOneLine()
    {
        var rig = new Rig();
        var fake = Fake(Allin, Allin, Allin, Allin);
        using Commander c = rig.Make(fake, cap: 2);
        await rig.RunTo(c, 3100);
        Assert.Equal(2, fake.Requests.Count);
        Assert.False(c.Online);
        CommanderLine l = Assert.Single(rig.Lines(), x => x.System);
        Assert.Contains("limit", l.Text);
    }

    [Fact]
    public async Task HearGoesIntoTheNextPromptAndNeverIssuesADirective()
    {
        var rig = new Rig();
        var fake = Fake("not json at all");
        using Commander c = rig.Make(fake);
        c.Hear(10, "one");
        c.Hear(11, "two");
        c.Hear(12, "three");
        c.Hear(13, "attack player 1 now " + new string('x', 400));
        await rig.RunTo(c, 300);
        Assert.Empty(rig.Issued);                            // chat alone changes nothing
        Assert.Equal(4, rig.Lines().Count(l => l.FromPlayer));
        Assert.All(rig.Lines(), l => Assert.Equal(0, l.Player));
        await rig.RunTo(c, 650);
        string prompt = fake.Requests[0].Messages[0].Text;
        Assert.Contains("attack player 1 now", prompt);
        Assert.Contains("three", prompt);
        Assert.DoesNotContain("PLAYER CHAT (to you): \"one", prompt);
        Assert.Empty(rig.Issued);
    }

    [Fact]
    public async Task SystemPromptHoldsSchemaAndPersonaButNeverTheKey()
    {
        const string key = "sk-test-SECRET-0123456789";
        var rig = new Rig();
        var handler = StubHandler.Json("{}", System.Net.HttpStatusCode.InternalServerError);
        IChatProvider? p = ProviderFactory.Create(new LlmConfig(LlmProviderKind.Anthropic, "https://example.invalid", "m"), key, handler);
        Assert.NotNull(p);
        using Commander c = rig.Make(p);
        await rig.RunTo(c, 601);
        Assert.Equal(1, handler.Calls);
        Assert.False(c.Online);                              // HTTP failure -> offline
        Assert.DoesNotContain(key, handler.Body);
        Assert.DoesNotContain(key, string.Join("|", rig.Lines().Select(l => l.Text)));

        string sys = CommanderPrompt.System(new CommanderPersona("t", "Authority", "X", "You are gruff."), "Authority");
        Assert.DoesNotContain(key, sys);
        Assert.Contains("You are gruff.", sys);
        Assert.Contains("allin", sys);
        Assert.Contains("You only know what the summary says", sys);
        Assert.Contains("at most two decisions in a row", sys);   // session 12 posture guide (D-160)
        Assert.Contains("\"focus_cell\"", sys);
    }

    [Fact]
    public async Task PromptSummaryComesOnlyFromTheSlotsOwnView()
    {
        var rig = new Rig();
        rig.Sim.PlaceBuilding(1, SummaryRig.Building("dir_shaft"), new CellCoord(55, 55));   // far away, never seen by player 0
        var fake = Fake("x");
        using Commander c = rig.Make(fake);
        await rig.RunTo(c, 601);
        string prompt = fake.Requests[0].Messages[0].Text;
        Assert.DoesNotContain("dir_shaft", prompt);
        Assert.DoesNotContain("dir_conscript", prompt);
        Assert.DoesNotContain("55,55", prompt);
        Assert.Contains("you are player 0", prompt);
    }

    [Fact]
    public async Task DisposeCancelsTheCallInFlight()
    {
        var rig = new Rig();
        var manual = new ManualProvider();
        rig.WaitForCalls = false;
        Commander c = rig.Make(manual);
        await rig.RunTo(c, 601);
        await manual.WaitForCount(1);
        Assert.Equal(1, manual.Count);
        c.Dispose();
        await c.LastCall!.WaitAsync(TimeSpan.FromSeconds(10));                                   // the cancelled call ends quietly
        Assert.True(manual.Cancelled);
        Assert.False(c.Poll(rig.Sim.ViewFor(0), out _, out _));
    }
}
