// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Input;
using Rts.Game.Lan;
using Rts.Game.Lan.Lobby;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Game.Ui;
using Rts.Net;
using Rts.Net.Discovery;
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>
/// The LAN lobby (M9-05, M9-06). Host view: every slot (open, closed, AI tier, faction, colour, team, options), kick, the map with a preview,
/// the command delay, chat, and Start once every player is ready. Guest view: the lobby as the host set it, with the guest's own faction,
/// colour and team, Ready, chat and Leave. The host answers LAN probes while the lobby is open. Start replaces this screen with the match
/// from <see cref="LanMatch.CreateScreen"/>.
/// </summary>
public sealed class LobbyScreen : IScreen
{
    private const ulong MaxSeed = 999_999_999;

    private readonly GameServices _s;
    private readonly LobbyHost? _host;
    private readonly LobbyClient? _client;
    private readonly DiscoveryResponder? _responder;
    private readonly int _gamePort;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly string[] _tiers;
    private readonly IReadOnlyList<MapEntry> _maps;
    private readonly MapPreviewView _previewView = new();
    private MenuPanel _panel;
    private Point _mouse;
    private int _mapIndex;
    private Biome _biome = Biome.RustBelt;
    private MapSize _size = MapSize.Small;
    private ulong _seed = 1;
    private string _chat = "";
    private string _status = "";
    private int _shownRevision = -1;
    private int _seedRow = -1, _chatRow = -1;
    private bool _handedOver;
    private MapChoice? _previewFor;
    private MapPreviewImage? _preview;

    /// <summary>Opens the host view over a lobby that is already listening. The screen owns <paramref name="responder"/> (may be null).</summary>
    public LobbyScreen(GameServices services, LobbyHost host, DiscoveryResponder? responder, int gamePort)
    {
        _s = services;
        _host = host;
        _responder = responder;
        _gamePort = gamePort;
        _tiers = LobbyText.TierNames(services.RulesDir);
        _maps = MapPicker.List(services.RulesDir);
        _panel = BuildPanel();
    }

    /// <summary>Opens the guest view over a connecting client.</summary>
    public LobbyScreen(GameServices services, LobbyClient client)
    {
        _s = services;
        _client = client;
        _tiers = LobbyText.TierNames(services.RulesDir);
        _maps = Array.Empty<MapEntry>();
        _panel = BuildPanel();
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
    }

    /// <inheritdoc />
    public void Exit()
    {
        _previewView.Dispose();
        _responder?.Dispose();
        if (_handedOver) return;   // the match owns the transport now
        _client?.Leave();
        _client?.Transport.Dispose();
        _host?.Transport.Dispose();
    }

    private long NowMicros => _clock.ElapsedTicks * 1_000_000L / Stopwatch.Frequency;

    private LobbyData? Data => _host?.Data ?? _client?.Data;

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        long now = NowMicros;
        if (_host is not null)
        {
            _host.Poll(now);
            if (_responder is not null) { _responder.Info = _host.Announcement(_gamePort); _responder.Poll(); }
        }
        else if (_client is not null)
        {
            _client.Poll(now);
            if (!CheckClient()) return;
        }

        int revision = _host?.Revision ?? _client!.Revision;
        if (revision != _shownRevision) { _shownRevision = revision; Rebuild(); }

        if (input.ConsumePressed(Keys.Escape)) { Leave(); return; }
        if (_panel.Selected == _seedRow) TypeSeed(input);
        else if (_panel.Selected == _chatRow) TypeChat(input);
        _panel.Update(input, _s.Viewport, _s.UiScale);
    }

    // false when the screen has left the stack
    private bool CheckClient()
    {
        switch (_client!.State)
        {
            case LobbyClientState.Rejected:
                _s.Screens.Replace(new NoticeScreen(_s, "Cannot join", _client.Reason));
                return false;
            case LobbyClientState.Disconnected when _client.Reason.Length > 0:
                _s.Screens.Replace(new NoticeScreen(_s, "Left the lobby", _client.Reason));
                return false;
            case LobbyClientState.Disconnected:
                _s.Screens.Pop();
                return false;
            case LobbyClientState.Started:
                BeginMatch(_client.Setup!);
                return false;
        }

        return true;
    }

    private void BeginMatch(LanMatchSetup setup)
    {
        try
        {
            IScreen game = LanMatch.CreateScreen(_s, setup);
            _handedOver = true;
            _s.Screens.Replace(game);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException)
        {
            // A bad map or rules file on this machine: stay in the lobby and say why instead of crashing.
            _status = "Could not start the match: " + ex.Message;
            Rebuild();
        }
    }

    private void Leave()
    {
        if (_client is not null) _client.Leave();
        _s.Screens.Pop();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
        DrawPreview(sb);
    }

    private MapPreviewImage? Preview
    {
        get
        {
            MapChoice? choice = CurrentChoice();
            if (!Equals(choice, _previewFor))
            {
                _previewFor = choice;
                _preview = choice is null ? null : MapPreviewCache.For(_s.RulesDir, choice);
            }

            return _preview;
        }
    }

    // The map as this machine would play it (null while a custom map is still downloading); a generated map shows the player count of the open slots.
    private MapChoice? CurrentChoice()
    {
        if (Data is not { } d) return null;
        MapChoice? choice = d.Map.Resolve(MapPicker.UserMapsDir, LanBrowserScreen.DownloadedMapsDir);
        if (choice?.Generated is { } g)
            choice = choice with { Generated = g with { Players = Math.Clamp(d.Slots.Count(x => x.Slot.Kind != SlotKind.Closed), SkirmishSettings.MinPlayers, SkirmishSettings.MaxSlots) } };
        return choice;
    }

    private void DrawPreview(SpriteBatch sb)
    {
        Rectangle panel = _panel.PanelRect;
        UiUnit scale = _s.UiScale;
        int side = 48 * scale;
        int gap = 3 * scale;
        int x;
        if (_s.Viewport.Width - panel.Right - gap >= side) x = panel.Right + gap;
        else if (panel.X - gap >= side) x = panel.X - gap - side;
        else return;
        _previewView.Draw(sb, _s.Pixel, new Rectangle(x, panel.Y, side, side), Preview, scale);
    }

    private void Rebuild() => _panel = BuildPanel();

    // ---- rows ----

    private MenuPanel BuildPanel()
    {
        var rows = new List<MenuItem>();
        _seedRow = -1;
        _chatRow = -1;
        LobbyData? d = Data;
        if (d is null)
        {
            rows.Add(new(() => "Connecting to the host ...") { Info = true });
            rows.Add(new(() => "Leave", Leave));
            return Panel(rows);
        }

        if (_host is not null) AddHostRows(rows, d);
        else AddGuestRows(rows, d);

        rows.Add(new(() => LobbyText.DelayLabel(d.CommandDelay), null, _host is null ? null : (Action<int>)StepDelay,
            _host is null ? Array.Empty<MenuButton>() : new[] { new MenuButton("<", () => StepDelay(-1)), new MenuButton(">", () => StepDelay(1)) }));
        rows.Add(new(() => LobbyText.SpectatorLine(d.Spectators)) { Info = true });
        IReadOnlyList<string> lines = _host?.Lines ?? _client!.Lines;
        foreach (string line in lines.Skip(Math.Max(0, lines.Count - 3)))
        {
            string l = line;
            rows.Add(new(() => SkirmishSetupScreen.Shorten(l, 60)) { Info = true });
        }

        _chatRow = rows.Count;
        rows.Add(new(() => $"Chat: {_chat}_", SendChat) { WidthTexts = new[] { "Chat: " + new string('W', 40) } });
        rows.Add(new(() => _status.Length > 0 ? _status : StatusText(d)) { Info = true });
        if (_host is not null) rows.Add(new(() => "Start", StartGame));
        else if (d.SlotOf(_client!.LocalPeer) is var me && me >= 0)
            rows.Add(new(() => d.Slots[me].Ready ? "Ready: yes (Enter to cancel)" : "Ready: no (Enter to get ready)", () => _client.SetReady(!d.Slots[me].Ready)));
        rows.Add(new(() => "Leave", Leave));
        return Panel(rows);
    }

    private MenuPanel Panel(List<MenuItem> rows)
    {
        var panel = new MenuPanel(_host is not null ? "LAN Lobby (host)" : "LAN Lobby", rows);
        if (_panel is not null) panel.Selected = Math.Min(_panel.Selected, rows.Count - 1);
        return panel;
    }

    private string StatusText(LobbyData d)
    {
        if (_host is not null)
            return _host.CanStart(out string? why) ? "Everyone is ready. Start when you like." : SkirmishSetupScreen.Shorten(why ?? "", 60);
        if (!_client!.HasMap) return $"Downloading the map from the host ... {_client.MapPercent}%";
        return d.SlotOf(_client.LocalPeer) < 0 ? "You are watching this game." : "Choose your side, then press Ready.";
    }

    private void AddHostRows(List<MenuItem> rows, LobbyData d)
    {
        if (_mapIndex == 0 && !d.Map.IsCustom && d.Map.File is null) _mapIndex = Math.Max(0, _maps.ToList().FindIndex(m => m.IsGenerated));
        rows.Add(new(() => $"Map: {LobbyText.MapLabel(_host!.Data.Map)}", null, StepMap, new MenuButton("<", () => StepMap(-1)), new MenuButton(">", () => StepMap(1)))
        { WidthTexts = _maps.Select(m => $"Map: {m.Label} xxxxxxxx").ToArray() });
        if (d.Map.Generated is not null)
        {
            rows.Add(new(() => $"Biome: {MapPicker.BiomeName(_biome)}", null, d2 => { _biome = (Biome)(((int)_biome + d2 + 4) % 4); ApplyGenerated(); },
                new MenuButton("<", () => { _biome = (Biome)(((int)_biome + 3) % 4); ApplyGenerated(); }), new MenuButton(">", () => { _biome = (Biome)(((int)_biome + 1) % 4); ApplyGenerated(); })));
            rows.Add(new(() => $"Size: {MapPicker.SizeName(_size)}", null, d2 => { _size = (MapSize)(((int)_size + d2 + 3) % 3); ApplyGenerated(); },
                new MenuButton("<", () => { _size = (MapSize)(((int)_size + 2) % 3); ApplyGenerated(); }), new MenuButton(">", () => { _size = (MapSize)(((int)_size + 1) % 3); ApplyGenerated(); })));
            _seedRow = rows.Count;
            rows.Add(new(() => $"Seed: {_seed}", null, d2 => { _seed = d2 < 0 ? (_seed > 0 ? _seed - 1 : 0) : Math.Min(MaxSeed, _seed + 1); ApplyGenerated(); },
                new MenuButton("Reroll", () => { _seed = (ulong)Random.Shared.NextInt64(1, (long)MaxSeed + 1); ApplyGenerated(); }))
            { WidthTexts = new[] { $"Seed: {MaxSeed}" } });
        }

        for (int i = 0; i < d.Slots.Count; i++)
        {
            int slot = i;
            var buttons = new List<MenuButton>();
            if (slot > 0) buttons.Add(new MenuButton("Kind", () => StepKind(slot)));
            buttons.Add(new MenuButton("Fac", () => _host!.UpdateSlot(slot, s => s with { Faction = SkirmishSetupScreen.NextFaction(s.Faction, 1) })));
            buttons.Add(new MenuButton("Tier", () => _host!.UpdateSlot(slot, s => s.Kind == SlotKind.Ai ? s with { AiTier = s.AiTier % _tiers.Length + 1 } : s)));
            buttons.Add(new MenuButton("Team", () => _host!.UpdateSlot(slot, s => s with { Team = (s.Team + 1) % SkirmishSettings.MaxSlots })));
            buttons.Add(new MenuButton("Col", () => _host!.CycleColour(slot)));
            if (d.Slots[slot].IsTaken && d.Slots[slot].Peer != 0) buttons.Add(new MenuButton("Kick", () => _host!.Kick(d.Slots[slot].Peer)));
            rows.Add(new(() => LobbyText.SlotLine(_host!.Data.Slots[slot], slot, _tiers, isLocal: slot == 0), null, null, buttons.ToArray())
            {
                WidthTexts = new[] { "4 " + new string('W', 12) + " (host) Overseas Expedition T4 not ready" },
                Swatch = () => _host!.Data.Slots[slot].Slot.Kind == SlotKind.Closed ? null : PlayerColours.Choice(_host.Data.Slots[slot].Slot.Colour),
            });
        }

        for (int i = 0; i < d.Slots.Count; i++)
        {
            int slot = i;
            if (d.Slots[slot].Slot.Kind == SlotKind.Closed) continue;
            rows.Add(new(() => LobbyText.OptionsLine(_host!.Data.Slots[slot], slot), null, null,
                new MenuButton(UiText.Get(UiText.OptHandicapButton), () => _host!.UpdateSlot(slot, s => s with { Handicap = SlotOptions.NextHandicap(s.Handicap) })),
                new MenuButton(UiText.Get(UiText.OptCreditsButton), () => _host!.UpdateSlot(slot, s => s with { CreditsPreset = SlotOptions.NextCredits(s.CreditsPreset) })),
                new MenuButton(UiText.Get(UiText.OptStartButton), () => _host!.UpdateSlot(slot, s => s with { SmallArmy = !s.SmallArmy }))));
        }
    }

    private void AddGuestRows(List<MenuItem> rows, LobbyData d)
    {
        rows.Add(new(() => $"Map: {LobbyText.MapLabel(d.Map)}" + (_client!.HasMap ? "" : $" (downloading {_client.MapPercent}%)")) { Info = true });
        int me = d.SlotOf(_client!.LocalPeer);
        for (int i = 0; i < d.Slots.Count; i++)
        {
            int slot = i;
            var buttons = new List<MenuButton>();
            if (slot == me)
            {
                buttons.Add(new MenuButton("Fac", () => Ask(slot, s => s with { Faction = SkirmishSetupScreen.NextFaction(s.Faction, 1) })));
                buttons.Add(new MenuButton("Team", () => Ask(slot, s => s with { Team = (s.Team + 1) % SkirmishSettings.MaxSlots })));
                buttons.Add(new MenuButton("Col", () => Ask(slot, s => s with { Colour = (s.Colour + 1) % PlayerColours.Count })));
            }
            else if (d.Slots[slot].IsOpen && me >= 0) buttons.Add(new MenuButton("Move", () => _client.Request(new SlotChange(d.Slots[me].Slot.Faction, d.Slots[me].Slot.Colour, d.Slots[me].Slot.Team, slot))));
            rows.Add(new(() => LobbyText.SlotLine(d.Slots[slot], slot, _tiers, isLocal: slot == me), null, null, buttons.ToArray())
            {
                WidthTexts = new[] { "4 " + new string('W', 12) + " (host) Overseas Expedition T4 not ready" },
                Swatch = () => d.Slots[slot].Slot.Kind == SlotKind.Closed ? null : PlayerColours.Choice(d.Slots[slot].Slot.Colour),
            });
        }
    }

    // A guest asks for a change of their own slot; the host decides and sends the lobby back.
    private void Ask(int slot, Func<SkirmishSlot, SkirmishSlot> change)
    {
        SkirmishSlot s = change(_client!.Data!.Slots[slot].Slot);
        _client.Request(new SlotChange(s.Faction, s.Colour, s.Team));
    }

    // ---- host actions ----

    private void StepKind(int slot)
    {
        SlotKind now = _host!.Data.Slots[slot].Slot.Kind;
        _host.SetSlotKind(slot, now switch { SlotKind.Human => SlotKind.Ai, SlotKind.Ai => SlotKind.Closed, _ => SlotKind.Human });
    }

    private void StepDelay(int delta)
    {
        IReadOnlyList<int> choices = NetProtocol.DelayChoices;
        int at = Math.Max(0, choices.ToList().IndexOf(_host!.Data.CommandDelay));
        _host.SetDelay(choices[((at + delta) % choices.Count + choices.Count) % choices.Count]);
    }

    private void StepMap(int delta)
    {
        _mapIndex = ((_mapIndex + delta) % _maps.Count + _maps.Count) % _maps.Count;
        if (_maps[_mapIndex].IsGenerated) ApplyGenerated();
        else _host!.SetMap(LobbyMap.FromEntry(_maps[_mapIndex], null));
        Rebuild();
    }

    private void ApplyGenerated()
    {
        int players = Math.Clamp(_host!.Data.Slots.Count(x => x.Slot.Kind != SlotKind.Closed), SkirmishSettings.MinPlayers, SkirmishSettings.MaxSlots);
        _host.SetMap(LobbyMap.FromGenerated(new MapGenParams(_biome, _size, players, _seed)));
    }

    private void StartGame()
    {
        if (!_host!.CanStart(out string? why)) { _status = SkirmishSetupScreen.Shorten(why ?? "", 60); Rebuild(); return; }
        MapEntry? entry = _mapIndex < _maps.Count ? _maps[_mapIndex] : null;
        int open = _host.Data.Slots.Count(s => s.Slot.Kind != SlotKind.Closed && !s.IsOpen);
        if (entry is { IsGenerated: false } && entry.File == _host.Data.Map.File && open > entry.MaxPlayers)
        {
            _status = $"This map has room for {entry.MaxPlayers} players.";
            Rebuild();
            return;
        }

        LanMatchSetup? setup = _host.Start(out why);
        if (setup is null) { _status = SkirmishSetupScreen.Shorten(why ?? "", 60); Rebuild(); return; }
        BeginMatch(setup);
    }

    // ---- typing ----

    private void TypeSeed(InputFrame input)
    {
        bool typed = false;
        foreach (char ch in input.ConsumeText())
        {
            if (ch < '0' || ch > '9') continue;
            ulong next = _seed * 10 + (ulong)(ch - '0');
            if (next <= MaxSeed) { _seed = next; typed = true; }
        }

        if (input.ConsumePressed(Keys.Back)) { _seed /= 10; typed = true; }
        if (typed) ApplyGenerated();
    }

    private void TypeChat(InputFrame input)
    {
        foreach (char ch in input.ConsumeText())
            if (ch >= ' ' && ch != 127 && _chat.Length < NetCodec.MaxChatChars) _chat += ch;
        if (input.ConsumePressed(Keys.Back) && _chat.Length > 0) _chat = _chat[..^1];
    }

    private void SendChat()
    {
        string text = _chat.Trim();
        _chat = "";
        if (text.Length == 0) return;
        if (_host is not null) _host.Say(0, text);
        else _client!.Say(text);
    }
}
