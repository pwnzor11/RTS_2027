// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Llm;
using Rts.Net;
using Rts.Net.Lockstep;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Game.Lan;

/// <summary>
/// One peer's side of a running LAN match (M9-07, M9-08, M9-10), with no graphics so it can be tested headless. Every peer builds the
/// same game from <see cref="LanMatchSetup.Settings"/> (<see cref="SkirmishGame.CreateNetworked"/>); the host drives it with a
/// <see cref="LockstepHost"/> (it runs every AI slot, the takeover AIs of dropped players and the LLM commanders, Q-M9-f), a client
/// with a <see cref="LockstepClient"/>. Local orders leave through <see cref="Rts.Game.Host.SimHost.Outbox"/>, ticks are stepped only by
/// <see cref="Rts.Game.Host.SimHost.StepWithBundle"/> (so each peer records the bundle stream into its replay), and the SimHost never
/// runs an AI itself. The session also keeps the chat list, the waiting, pause and drop state, writes desync reports, runs the host's
/// "Resync from host" (the client replaces its simulation with the host's save right before it steps the paused tick) and, when the host
/// is gone, writes the "Continue against AI" skirmish save (Q-M9-j). Single-threaded: call <see cref="Update"/> from the game loop.
/// </summary>
public sealed class LanSession : IDisposable
{
    /// <summary>Most chat lines kept.</summary>
    public const int MaxChatLines = 50;

    private const int SubHashRing = 64;
    private const int SpectatorTeam = 1000;

    private readonly LanMatchSetup _setup;
    private readonly string _rulesDir;
    private readonly LanSessionOptions _options;
    private readonly LanTransportTap _tap;
    private readonly LockstepHost? _host;
    private readonly LockstepClient? _client;
    private readonly SkirmishGame _game;
    private readonly ReplayRecording? _recording;
    private readonly IReadOnlyList<(string Name, string Text)> _rulesFiles;
    private readonly IReadOnlyList<int> _playerSlots;
    private readonly List<AiDriver> _takeovers = new();
    private readonly ushort[] _nextSequence = new ushort[256];
    private readonly SortedDictionary<int, (string Part, ulong Hash)[]> _subHashes = new();
    private readonly List<LanChatLine> _chat = new();
    private readonly List<CommanderLine> _commanderScratch = new();
    private SimSnapshotSource _source;
    private long _now;
    private long _waitSinceUs = -1;
    private int[]? _lastWaitingPeers;
    private long _waitingReceivedUs;
    private int _waitingBaseMs;
    private int _commanderVersion;
    private bool _reportDue;
    private bool _resyncRequested;
    private int _transferId;
    private LanResync? _pendingResync;
    private bool _disposed;

    /// <summary>Builds the game, the replay recording and the lockstep driver for this peer. The session owns the transport from here.</summary>
    /// <param name="setup">The lobby's result.</param>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="options">Replay path, desync folder and app settings.</param>
    public LanSession(LanMatchSetup setup, string rulesDir, LanSessionOptions options)
    {
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _rulesDir = rulesDir;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _playerSlots = setup.Settings.PlayerSlots;
        LocalPlayer = PlayerOfPeer(setup.LocalPeer);
        _game = SkirmishGame.CreateNetworked(rulesDir, setup.Settings, setup.IsHost ? options.AppSettings : null);
        _rulesFiles = RulesBundle.ReadDirectory(rulesDir);
        if (options.ReplayPath is { } replay) _recording = new ReplayRecording(_game.Sim, _rulesFiles, replay, PreviousReplayPath(replay));
        _tap = new LanTransportTap(setup.Transport);
        _source = MakeSource(_game.Sim);

        if (setup.IsHost)
        {
            var host = new LockstepHost(_tap, setup.CommandDelay, StepTick)
            {
                CollectAi = CollectAi,
                HashAt = HashAt,
                TeamOf = TeamOfPeer,
            };
            foreach ((int peer, int slot) in setup.PeerSlots)
            {
                int player = IndexOfSlot(slot);
                if (player >= 0) host.SetSlot(peer, (byte)player);
            }
            foreach (int peer in setup.Spectators) host.SetSpectator(peer);
            // the match's peers come from the setup, not from who is connected now: a peer that left since the Start is dropped and
            // its slot taken over instead of being left dead (M9-14)
            foreach (int peer in setup.PeerSlots.Keys.Concat(setup.Spectators).Order()) host.AddPeer(peer);
            host.PeerDropped += OnHostDrop;
            host.Desynced += (_, _) => _reportDue = true;
            host.ChatReceived += AddChat;
            _host = host;
        }
        else
        {
            var client = new LockstepClient(_tap, setup.CommandDelay, StepTick, HashOf);
            client.PeerDropped += OnClientDrop;
            client.Desynced += (_, _) => _reportDue = true;
            client.ChatReceived += AddChat;
            _client = client;
        }
        _source.Host.Outbox = Submit;
    }

    /// <summary>The match setup.</summary>
    public LanMatchSetup Setup => _setup;

    /// <summary>The match settings.</summary>
    public SkirmishSettings Settings => _setup.Settings;

    /// <summary>True on the host.</summary>
    public bool IsHost => _setup.IsHost;

    /// <summary>This side's peer id.</summary>
    public int LocalPeer => _setup.LocalPeer;

    /// <summary>This peer's player, or -1 for a spectator.</summary>
    public int LocalPlayer { get; }

    /// <summary>True for a spectator: no slot, omniscient view, no orders.</summary>
    public bool IsSpectator => LocalPlayer < 0;

    /// <summary>The game as it was built (map, rules, the host's AI driver and commanders).</summary>
    public SkirmishGame Game => _game;

    /// <summary>The source the screen draws; replaced by a resync (<see cref="SourceReplaced"/>).</summary>
    public SimSnapshotSource Source => _source;

    /// <summary>The session clock: the last time passed to <see cref="Update"/>, in microseconds.</summary>
    public long Clock => _now;

    /// <summary>The next tick this peer steps.</summary>
    public int Tick => _source.Tick;

    /// <summary>Raised after a resync replaced <see cref="Source"/>; the screen rebuilds itself around the new one.</summary>
    public event Action? SourceReplaced;

    /// <summary>Raised after every stepped tick with its bundle (tests and diagnostics).</summary>
    public event Action<int, IReadOnlyList<Command>>? Stepped;

    /// <summary>The host's lockstep driver (null on a client).</summary>
    public LockstepHost? Host => _host;

    /// <summary>The client's lockstep driver (null on the host).</summary>
    public LockstepClient? Client => _client;

    /// <summary>The chat list, oldest first (player lines and system notices).</summary>
    public IReadOnlyList<LanChatLine> Chat => _chat;

    /// <summary>True from a pause until the resume countdown ends.</summary>
    public bool Paused => _host?.Paused ?? _client!.Paused;

    /// <summary>The peer that paused last (-1 before any pause).</summary>
    public int PausedBy => _host?.PausedBy ?? _client!.PausedBy;

    /// <summary>Milliseconds left of the resume countdown, 0 when none runs.</summary>
    public int ResumeCountdownMs => _host?.ResumeCountdownMs ?? _client!.ResumeCountdownMs;

    /// <summary>Peers the match is waiting for (empty when it is not waiting).</summary>
    public IReadOnlyList<int> WaitingFor => _host is not null ? _host.WaitingFor : _client!.Waiting.Peers;

    /// <summary>How long the match has been waiting, in milliseconds.</summary>
    public int WaitedMs { get; private set; }

    /// <summary>True when the waiting overlay shows (waiting for longer than <see cref="NetProtocol.WaitOverlayMs"/>).</summary>
    public bool ShowWaiting => WaitingFor.Count > 0 && WaitedMs >= NetProtocol.WaitOverlayMs;

    /// <summary>The open desync's tick, or null when in sync (or after Continue / a resync).</summary>
    public int? DesyncTick { get; private set; }

    /// <summary>The folder of the last desync report, or null.</summary>
    public string? DesyncReportFolder { get; private set; }

    /// <summary>True on the host from Resync from host until the save is sent; on a client while it waits for or holds the save.</summary>
    public bool Resyncing => _resyncRequested || _pendingResync is not null;

    /// <summary>Resyncs completed on this peer.</summary>
    public int ResyncCount { get; private set; }

    /// <summary>True on a client once the host left or was lost (Q-M9-j).</summary>
    public bool HostGone => _client?.HostLost ?? false;

    /// <summary>Tests only: replaces the host's capture of the resync save (null in the game).</summary>
    public Func<byte[]>? ResyncSaveOverride { get; set; }

    /// <summary>The host's state hashes compared with clients' so far (host only, else 0).</summary>
    public int HashesCompared => _host?.HashesCompared ?? 0;

    /// <summary>Client hashes that differed from the host's (host only, else 0).</summary>
    public int HashMismatches => _host?.HashMismatches ?? 0;

    /// <summary>The display name of a peer.</summary>
    public string PeerName(int peer) =>
        _setup.PeerNames.TryGetValue(peer, out string? name) && name.Length > 0 ? name : LanText.UnknownPeer.Replace("{peer}", peer.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>The player a peer plays, or -1 for a spectator or an unknown peer.</summary>
    public int PlayerOfPeer(int peer) => _setup.PeerSlots.TryGetValue(peer, out int slot) ? IndexOfSlot(slot) : -1;

    /// <summary>True when the host may drop <paramref name="peer"/> now (it has held the game up for 10 s).</summary>
    public bool CanDrop(int peer) => _host is not null && _host.CanDrop(peer);

    /// <summary>Host only: drops a lagging peer; an AI takes its slot.</summary>
    public bool Drop(int peer) => _host is not null && _host.Drop(peer, "dropped by the host");

    /// <summary>Pauses, or starts the resume countdown when paused (P). No resume while a desync is open.</summary>
    public void TogglePause()
    {
        if (!Paused || ResumeCountdownMs > 0) RequestPause();
        else RequestResume();
    }

    /// <summary>Asks for a pause (anyone may, Q-M9-h).</summary>
    public void RequestPause()
    {
        if (_host is not null) _host.RequestPause();
        else if (!HostGone) _client!.RequestPause();
    }

    /// <summary>Asks for the resume countdown. Ignored while a desync is open (the host decides).</summary>
    public void RequestResume()
    {
        if (DesyncTick is not null || Resyncing) return;
        if (_host is not null) _host.RequestResume();
        else if (!HostGone) _client!.RequestResume();
    }

    /// <summary>Sends a chat line to everyone or to the sender's team. It shows when the host relays it back.</summary>
    public void SendChat(bool team, string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        if (text.Length > LockstepHost.MaxChatChars) text = text[..LockstepHost.MaxChatChars];
        byte audience = team ? (byte)1 : (byte)0;
        if (_host is not null) _host.SendChat(audience, text);
        else if (!HostGone) _client!.SendChat(audience, text);
    }

    /// <summary>Host: answers an open desync by sending its save of the paused tick to every client (they load it, then play resumes).</summary>
    public void ResyncFromHost()
    {
        if (_host is null || DesyncTick is null) return;
        _resyncRequested = true;
    }

    /// <summary>Host: answers an open desync with Continue (keep playing; a later mismatch is reported again).</summary>
    public void ContinueAfterDesync()
    {
        if (_host is null || DesyncTick is null) return;
        DesyncTick = null;
        _tap.BlockResume = false;
        _host.ContinueAfterDesync();
        _host.RequestResume();
    }

    /// <summary>Pumps the network and steps every tick that is due.</summary>
    /// <param name="nowMicroseconds">A monotonic clock (the game time).</param>
    public void Update(long nowMicroseconds)
    {
        if (_disposed) return;
        _now = nowMicroseconds;
        if (_host is not null) _host.Update(nowMicroseconds);
        else _client!.Update(nowMicroseconds);
        DrainTransfers();
        if (_client is not null && _pendingResync is { Complete: true } r && _client.Tick == r.Tick) ApplyResync();
        if (_reportDue)
        {
            _reportDue = false;
            OnDesync();
        }
        if (_client is not null && DesyncTick is not null && _client.Desync is null) DesyncTick = null;   // the host chose Continue and resumed
        if (_resyncRequested && _host is { Paused: true } h && h.Tick == h.SealedThrough + 1) SendResync(h);
        UpdateWaiting();
        RelayCommanderLines();
    }

    /// <summary>The settings a "Continue against AI" restart uses: the local player's slot stays Human, every other Human slot becomes an AI.</summary>
    public static SkirmishSettings SinglePlayerSettings(SkirmishSettings settings, int localPlayer)
    {
        ArgumentNullException.ThrowIfNull(settings);
        IReadOnlyList<int> order = settings.PlayerSlots;
        int keep = localPlayer >= 0 && localPlayer < order.Count ? order[localPlayer] : -1;
        return settings with
        {
            Slots = settings.Slots.Select((s, i) => s.Kind == SlotKind.Human && i != keep
                ? s with { Kind = SlotKind.Ai, AiTier = SkirmishSettings.DefaultAiTier } : s).ToArray(),
        };
    }

    /// <summary>
    /// Client, host gone (Q-M9-j, design 20 §9): writes an ordinary single-player skirmish save of the last stepped tick into
    /// <paramref name="catalog"/>, where every player but this one is played by an AI (the former humans, the host included, at the
    /// default tier; host-run AIs start with empty memory, which lived on the host). Never a LAN save. Returns its path.
    /// </summary>
    /// <exception cref="InvalidOperationException">This peer is a spectator.</exception>
    /// <exception cref="IOException">The save could not be written.</exception>
    public string WriteContinueSave(SaveCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (IsSpectator) throw new InvalidOperationException("A spectator has no side to continue with.");
        var ai = new List<SavedAi>();
        for (int p = 0; p < _playerSlots.Count; p++)
        {
            if (p == LocalPlayer) continue;
            SkirmishSlot slot = Settings.Slots[_playerSlots[p]];
            int tier = slot.Kind == SlotKind.Ai ? slot.AiTier : SkirmishSettings.DefaultAiTier;
            ai.Add(new SavedAi((byte)p, _game.AiId, tier, slot.Faction, Settings.SimSeed));
        }
        GameSaveHeader template = _game.SaveTemplate(Settings) with { LocalPlayer = (byte)LocalPlayer, Ai = ai };
        _source.Host.RestoreQueue(_source.Host.Pending.ToArray(), Sequences());
        return GameSaver.Save(catalog, template, _source.Host, _rulesDir, LanText.ContinueSlot);
    }

    /// <summary>Finishes the replay (and saves a generated or user map beside it). Safe to call more than once.</summary>
    public bool SaveReplay()
    {
        if (_recording is null) return false;
        bool ok = _recording.Finish();
        if (ok)
        {
            try { _game.SaveMapBeside(_recording.FilePath); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"error: map not saved beside the replay: {e.Message}");
            }
        }
        return ok;
    }

    /// <summary>Leaves the match politely (Bye), finishes the replay, closes the transport and cancels LLM calls. Safe to call twice.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_host is not null) _tap.Broadcast(new NetMessage.Bye(LanText.HostClosed));
            else if (!HostGone) _client!.Leave(LanText.ClientClosed);
            _tap.Poll(_now);   // flush the Bye before the socket closes
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException
            or System.Net.Sockets.SocketException)
        {
            // leaving is best effort; the others time us out
        }
        SaveReplay();
        _tap.Dispose();
        _game.Dispose();
    }

    private static string PreviousReplayPath(string replay)
    {
        string name = Path.GetFileNameWithoutExtension(replay);
        string prev = name.Replace("_last", "_prev", StringComparison.Ordinal);
        return prev == name ? replay + ".prev" : Path.Combine(Path.GetDirectoryName(replay) ?? "", prev + Path.GetExtension(replay));
    }

    private int IndexOfSlot(int slot)
    {
        for (int i = 0; i < _playerSlots.Count; i++) if (_playerSlots[i] == slot) return i;
        return -1;
    }

    private int TeamOfPeer(int peer)
    {
        int player = PlayerOfPeer(peer);
        return player < 0 ? SpectatorTeam : _game.Sim.Setup.TeamOf((byte)player);
    }

    private SimSnapshotSource MakeSource(Simulation sim)
    {
        // the replay writer is bound to the simulation it started with: after a resync the session records the bundles itself (no hashes)
        ReplayRecording? recording = ReferenceEquals(sim, _game.Sim) ? _recording : null;
        var source = new SimSnapshotSource(sim, _game.Map, recording) { Local = (byte)Math.Max(0, LocalPlayer) };
        source.SaveTemplate = null;   // a LAN game is never saved (Q-M9-i)
        source.Host.Spectator = IsSpectator;
        source.Host.SetViewer(IsSpectator ? -1 : LocalPlayer);
        return source;
    }

    private void Submit(Command command)
    {
        if (_host is not null) _host.Submit(command);
        else _client!.Submit(command);
    }

    private void StepTick(int tick, IReadOnlyList<Command> bundle)
    {
        DrainTransfers();
        if (_client is not null && _pendingResync is { Complete: true } r && r.Tick == tick) ApplyResync();
        if (_source.Tick != tick)
            throw new InvalidOperationException($"LAN lockstep out of step: the sim is at tick {_source.Tick} but bundle {tick} came to run.");
        if (!ReferenceEquals(_source.Sim, _game.Sim))
            foreach (Command c in bundle) _recording?.RecordCommand(c);   // resynced: the source records nothing itself
        _source.Host.StepWithBundle(bundle);
        foreach (Command c in bundle)
            if (c.Sequence + 1 > _nextSequence[c.Player]) _nextSequence[c.Player] = (ushort)(c.Sequence + 1);
        if (_host is not null && _source.Sim.Tick % SimConstants.HashIntervalTicks == 0) KeepSubHashes(_source.Sim.Tick);
        Stepped?.Invoke(tick, bundle);
    }

    private (ulong Value, ulong[] SubHashes) HashOf(int tick)
    {
        (string Part, ulong Hash)[] subs = KeepSubHashes(tick, out ulong full);
        var values = new ulong[subs.Length];
        for (int i = 0; i < subs.Length; i++) values[i] = subs[i].Hash;
        return (full, values);
    }

    private (string Part, ulong Hash)[] KeepSubHashes(int tick) => KeepSubHashes(tick, out _);

    private (string Part, ulong Hash)[] KeepSubHashes(int tick, out ulong full)
    {
        (string Part, ulong Hash)[] subs = _source.Sim.ComputeHashes(out full).ToArray();
        _subHashes[tick] = subs;
        while (_subHashes.Count > SubHashRing) _subHashes.Remove(_subHashes.Keys.First());
        return subs;
    }

    private ulong? HashAt(int tick)
    {
        foreach ((int Tick, ulong Hash) h in _source.Sim.RecentHashes) if (h.Tick == tick) return h.Hash;
        return null;
    }

    private void CollectAi(int tick, List<Command> list)
    {
        _game.Ai.Collect(_source.Sim, list);
        foreach (AiDriver ai in _takeovers) ai.Collect(_source.Sim, list);
    }

    private ushort[] Sequences()
    {
        int n = _nextSequence.Length;
        while (n > 0 && _nextSequence[n - 1] == 0) n--;
        return _nextSequence[..n];
    }

    private void OnHostDrop(LockstepDrop drop)
    {
        if (drop.Player >= 0)
        {
            // Q-M9-d: a fresh driver (AiDriver.Add must precede its first Collect) takes the slot from the drop tick, at the skirmish default tier
            DifficultyProfile profile = LoadProfile(SkirmishSettings.DefaultAiTier);
            if (AiRegistry.TryCreate(_game.AiId, profile, out IAiPlayer? ai))
            {
                var driver = new AiDriver(Settings.SimSeed);
                driver.Add((byte)drop.Player, ai, profile);
                _takeovers.Add(driver);
            }
        }
        AddNotice((drop.Player >= 0 ? LanText.PeerLeft : LanText.SpectatorLeft).Replace("{name}", PeerName(drop.Peer)));
    }

    private void OnClientDrop(NetMessage.PeerDropped drop)
    {
        if (drop.Peer == LocalPeer) AddNotice(LanText.YouWereDropped);
        else AddNotice((PlayerOfPeer(drop.Peer) >= 0 ? LanText.PeerLeft : LanText.SpectatorLeft).Replace("{name}", PeerName(drop.Peer)));
    }

    private DifficultyProfile LoadProfile(int tier)
    {
        try
        {
            IReadOnlyList<DifficultyProfile> all = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(_rulesDir));
            return all[Math.Clamp(tier, 1, all.Count) - 1];
        }
        catch (RulesException)
        {
            return DifficultyProfile.Default;
        }
    }

    // PENDING Q-M9-g-replay: chat is "kept in the replay" (Q-M9-g) but the replay format has no chat block and may not change in this
    // package; chat lines are shown only.
    private void AddChat(NetMessage.Chat chat)
    {
        AddLine(new LanChatLine(chat.FromPeer, PlayerOfPeer(chat.FromPeer), PeerName(chat.FromPeer), chat.Text, chat.Audience == 1, _now));
    }

    private void AddNotice(string text) => AddLine(new LanChatLine(-1, -1, "", text, false, _now));

    private void AddLine(LanChatLine line)
    {
        _chat.Add(line);
        if (_chat.Count > MaxChatLines) _chat.RemoveRange(0, _chat.Count - MaxChatLines);
    }

    // The host relays its LLM commanders' lines as chat (design 20 §4.6: chat messages, not sim commands); they show on every peer.
    private void RelayCommanderLines()
    {
        if (_host is null) return;
        CommanderChatLog log = _game.Chat;
        int version = log.Version;
        if (version == _commanderVersion) return;
        int added = version - _commanderVersion;
        _commanderVersion = version;
        _commanderScratch.Clear();
        log.CopyTo(_commanderScratch);
        for (int i = Math.Max(0, _commanderScratch.Count - added); i < _commanderScratch.Count; i++)
        {
            CommanderLine line = _commanderScratch[i];
            if (line.FromPlayer) continue;
            _host.SendChat(0, line.System ? line.Text : $"{line.Speaker}: {line.Text}");
        }
    }

    private void UpdateWaiting()
    {
        if (_host is not null)
        {
            if (_host.WaitingFor.Count == 0) _waitSinceUs = -1;
            else if (_waitSinceUs < 0) _waitSinceUs = _now;
            WaitedMs = _waitSinceUs < 0 ? 0 : (int)Math.Min(int.MaxValue, (_now - _waitSinceUs) / 1000);
            return;
        }
        (int[] peers, int ms) = _client!.Waiting;
        if (!ReferenceEquals(peers, _lastWaitingPeers))
        {
            _lastWaitingPeers = peers;
            _waitingReceivedUs = _now;
            _waitingBaseMs = ms;
        }
        WaitedMs = peers.Length == 0 ? 0 : (int)Math.Min(int.MaxValue, _waitingBaseMs + (_now - _waitingReceivedUs) / 1000);
    }

    // ---- desync (M9-10) ----

    private void OnDesync()
    {
        int tick;
        IReadOnlyList<ulong>? other = null;
        string otherName = "";
        if (_host is not null)
        {
            if (_host.Desync is not { } d) return;
            tick = d.Tick;
            other = d.SubHashes;
            otherName = d.Peers.Length > 0 ? PeerName(d.Peers[0]) : "client";
            _tap.BlockResume = true;   // nobody resumes until the host chose
        }
        else
        {
            if (_client!.Desync is not { } d) return;
            tick = d.Tick;
        }
        DesyncTick = tick;
        IReadOnlyList<(string Part, ulong Hash)>? own = _subHashes.TryGetValue(tick, out var subs) ? subs : null;
        string text = LanDesyncReport.SubHashText(tick, own, other, otherName);
        DesyncReportFolder = LanDesyncReport.Write(_options.DesyncRoot, DateTime.Now, _recording?.FilePath, TryCaptureSave(), text);
    }

    private byte[]? TryCaptureSave()
    {
        try { return CaptureSave(); }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine($"error: desync save not made: {e.Message}");
            return null;
        }
    }

    // The state at the current tick in the ordinary save format (the resync payload). No AI slots: the host keeps running its own.
    private byte[] CaptureSave()
    {
        if (ResyncSaveOverride is { } over) return over();
        GameSaveHeader template = _game.SaveTemplate(Settings) with { LocalPlayer = (byte)Math.Max(0, LocalPlayer), Ai = Array.Empty<SavedAi>() };
        _source.Host.RestoreQueue(_source.Host.Pending.ToArray(), Sequences());
        using var stream = new MemoryStream();
        GameSaveFile.Write(stream, GameSaver.Capture(template, _source.Host), _source.Sim, _rulesFiles);
        return stream.ToArray();
    }

    // Host, paused and drained (every sealed bundle stepped, so every client stops on this same tick): send the save, then resume.
    private void SendResync(LockstepHost host)
    {
        _resyncRequested = false;
        int tick = host.Tick;
        byte[] save = CaptureSave();
        int id = ++_transferId;
        _tap.Broadcast(new NetMessage.ResyncBegin(tick, id));
        foreach (NetMessage.FileChunk chunk in LanResync.Split(save, id)) _tap.Broadcast(chunk);
        _tap.DropHashesThrough = tick;   // hashes a lagging client took on its old state up to this tick are void
        DesyncTick = null;
        _tap.BlockResume = false;
        host.ContinueAfterDesync();
        host.RequestResume();
        ResyncCount++;
        AddNotice(LanText.Resynced);
    }

    private void DrainTransfers()
    {
        while (_tap.Transfers.TryDequeue(out NetMessage? message))
        {
            switch (message)
            {
                case NetMessage.ResyncBegin begin:
                    _pendingResync = new LanResync(begin.Tick, begin.TransferId);
                    _tap.DropHashesThrough = begin.Tick;
                    break;
                case NetMessage.FileChunk chunk:
                    _pendingResync?.Add(chunk);
                    break;
            }
        }
    }

    // Client: replaces the simulation with the host's save, right before stepping the save's tick. The lockstep driver is already at that
    // tick (every peer drained the same sealed bundles while paused), so its tick, held bundles and the host's sequences stay as they are.
    private void ApplyResync()
    {
        LanResync r = _pendingResync!;
        _pendingResync = null;
        Simulation sim;
        GameSaveHeader header;
        try
        {
            using var stream = new MemoryStream(r.Assemble());
            header = GameSaveFile.ReadHeader(stream);
            if (header.Tick != r.Tick) throw new InvalidDataException($"the save is for tick {header.Tick}, not {r.Tick}");
            sim = Simulation.Load(stream, SkirmishGame.MakeSetup(_game.Rules, Settings, _game.Map, _game.MapMeta));
        }
        catch (Exception e) when (e is InvalidDataException or SaveLoadException or EndOfStreamException or IOException
            or InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine($"error: resync failed: {e.Message}");
            return;
        }
        int viewer = _source.Viewer;
        _source = MakeSource(sim);
        _source.Host.Outbox = Submit;
        _source.Host.SetViewer(viewer);
        _source.Host.RestoreQueue(header.Pending, header.Sequences);
        Array.Clear(_nextSequence);
        for (int i = 0; i < header.Sequences.Count && i < _nextSequence.Length; i++) _nextSequence[i] = header.Sequences[i];
        DesyncTick = null;
        ResyncCount++;
        AddNotice(LanText.Resynced);
        SourceReplaced?.Invoke();
    }
}
