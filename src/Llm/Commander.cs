// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.Llm;

/// <summary>
/// M5-05: one LLM commander (one per LLM slot). A directive source for <see cref="AiDriver"/> and the chat channel of the panel.
/// All timing is in ticks (20 per second): the first decision is requested at the first poll at or after tick 600, then one every 600
/// ticks, never more than one request in flight. The network call runs on the thread pool; only its result crosses threads (under a lock).
/// A reply older than 600 ticks is dropped. Failure, a missing provider or the call cap switch the commander offline: the slot plays its
/// plain tier and one system chat line says so. Typed player chat goes into the next prompt and never changes the directive by itself.
/// </summary>
public sealed class Commander : IDirectiveSource, ICommanderChannel, IDisposable
{
    /// <summary>Ticks between decisions (30 s of game time, D-103); also the age after which a reply is stale.</summary>
    public const int CadenceTicks = 600;

    /// <summary>Longest kept player chat text.</summary>
    public const int MaxHearChars = 200;

    /// <summary>Most player chat lines kept for the next prompt.</summary>
    public const int MaxHeard = 3;

    private readonly IChatProvider? _provider;
    private readonly CommanderPersona _persona;
    private readonly string _faction;
    private readonly int _callCap;
    private readonly bool _chatOn;
    private readonly CommanderChatLog _chat;
    private readonly SummaryHistory _history = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private readonly List<string> _heard = new();
    private readonly List<OrderDraft> _orders = new();   // S24 (M5-12): game thread only, taken by TakeOrders

    // game thread only
    private int _nextCallTick = CadenceTicks;
    private int _requestTick;
    private int _calls;
    private bool _capNoticePosted;
    private bool _offlineNoticePosted;
    private bool _startNoticeChecked;
    private bool _disposed;

    // guarded by _gate
    private bool _inFlight;
    private ChatResult? _done;
    private volatile bool _online;

    /// <summary>Creates the commander of slot <paramref name="player"/>.</summary>
    /// <param name="player">The sim slot.</param>
    /// <param name="provider">The chat backend, or null = offline from the start.</param>
    /// <param name="persona">Voice and display name.</param>
    /// <param name="faction">The faction's name for the prompt.</param>
    /// <param name="callCap">Most calls per game; 0 = no cap.</param>
    /// <param name="chatOn">Whether the commander's say-lines go to the chat.</param>
    /// <param name="chat">The shared chat log.</param>
    public Commander(byte player, IChatProvider? provider, CommanderPersona persona, string faction, int callCap, bool chatOn, CommanderChatLog chat)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(chat);
        Player = player;
        _provider = provider;
        _persona = persona;
        _faction = faction ?? "";
        _callCap = Math.Max(0, callCap);
        _chatOn = chatOn;
        _chat = chat;
        _online = provider is not null;
    }

    /// <inheritdoc />
    public byte Player { get; }

    /// <inheritdoc />
    public string Speaker => _persona.DisplayName;

    /// <inheritdoc />
    public bool Online => _online;

    /// <summary>Calls started so far (for the cap and tests).</summary>
    public int CallsMade => _calls;

    /// <summary>The request in flight or the last one started; tests await it. Null before the first call.</summary>
    internal Task? LastCall { get; private set; }

    /// <inheritdoc />
    public void Hear(int tick, string text)
    {
        string t = (text ?? "").Trim().ReplaceLineEndings(" ");
        if (t.Length == 0) return;
        if (t.Length > MaxHearChars) t = t[..MaxHearChars];
        lock (_gate)
        {
            _heard.Add(t);
            while (_heard.Count > MaxHeard) _heard.RemoveAt(0);
        }
        _chat.Add(new CommanderLine(tick, Player, "You", t, FromPlayer: true));
    }

    /// <inheritdoc />
    public bool Poll(PlayerView view, out int packedArg, out CellCoord focusCell)
    {
        packedArg = 0;
        focusCell = Directive.NoCell;
        int tick = view.Tick;
        if (_disposed) return false;

        if (!_startNoticeChecked)
        {
            _startNoticeChecked = true;
            if (_provider is null) PostOffline(tick);
        }

        bool issued = false;
        ChatResult? result = null;
        lock (_gate) { result = _done; _done = null; }
        if (result is not null) issued = Handle(result, view, out packedArg, out focusCell);

        if (_provider is not null && tick >= _nextCallTick) TryStart(view);
        return issued;
    }

    private bool Handle(ChatResult result, PlayerView view, out int packedArg, out CellCoord focusCell)
    {
        packedArg = 0;
        focusCell = Directive.NoCell;
        int tick = view.Tick;
        if (!result.Ok)
        {
            _online = false;
            PostOffline(tick);
            return false;
        }
        _online = true;
        _offlineNoticePosted = false;
        if (tick - _requestTick > CadenceTicks) return false;   // stale

        CommanderReply reply = ReplyParser.Parse(result.Text);
        if (reply.Say is { } say && _chatOn)
            _chat.Add(new CommanderLine(tick, Player, Speaker, say));
        _orders.AddRange(reply.OrderList);
        if (reply.Directive is not { } d) return false;

        _history.RecordDirective(d, tick);
        packedArg = DirectiveCodec.PackStrong(DirectiveCodec.Pack(d.Posture, d.Focus, d.TargetPlayer, d.Mix, d.Expand), d.StrongMix);
        focusCell = d.FocusX < 0 || d.FocusY < 0 ? Directive.NoCell : new CellCoord(d.FocusX, d.FocusY);
        return true;
    }

    /// <summary>S24 (M5-12): the standing orders parsed since the last call, oldest first, then cleared. Game thread only. The caller
    /// turns them into commands with <see cref="DirectiveCommands.AddOrderCommands"/> (the driver emits only the directive).</summary>
    public IReadOnlyList<OrderDraft> TakeOrders()
    {
        OrderDraft[] taken = _orders.ToArray();
        _orders.Clear();
        return taken;
    }

    private void TryStart(PlayerView view)
    {
        lock (_gate) { if (_inFlight) return; }
        if (_callCap > 0 && _calls >= _callCap)
        {
            _online = false;
            if (!_capNoticePosted)
            {
                _capNoticePosted = true;
                _chat.Add(new CommanderLine(view.Tick, Player, Speaker, Speaker + CapText, System: true));
            }
            return;
        }

        _history.Observe(view);
        string chat;
        lock (_gate)
        {
            chat = string.Join(" / ", _heard);
            _heard.Clear();
        }
        string summary = BattlefieldSummary.Build(view, _history, chat.Length == 0 ? null : chat);
        var request = new ChatRequest(CommanderPrompt.System(_persona, _faction),
            new[] { new ChatMessage(ChatRole.User, CommanderPrompt.User(summary)) }, CommanderPrompt.MaxTokens);

        _calls++;
        _requestTick = view.Tick;
        _nextCallTick = view.Tick + CadenceTicks;
        lock (_gate) _inFlight = true;
        IChatProvider provider = _provider!;
        CancellationToken ct = _cts.Token;
        LastCall = Task.Run(async () =>
        {
            ChatResult r;
            try { r = await provider.CompleteAsync(request, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { lock (_gate) _inFlight = false; return; }
            catch (Exception) { r = new ChatResult(false, "", "request failed", 0, 0); }
            lock (_gate) { _done = r; _inFlight = false; }
        });
    }

    private void PostOffline(int tick)
    {
        if (_offlineNoticePosted) return;
        _offlineNoticePosted = true;
        _chat.Add(new CommanderLine(tick, Player, Speaker, Speaker + OfflineText, System: true));
    }

    /// <summary>Chat notice appended to the speaker's name when the commander cannot call the model.</summary>
    public const string OfflineText = " is offline — playing the standard AI.";

    /// <summary>Chat notice appended to the speaker's name when the call cap is reached.</summary>
    public const string CapText = " has reached the call limit — playing the standard AI.";

    /// <summary>Cancels an in-flight call.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
