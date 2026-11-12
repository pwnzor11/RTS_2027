// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Net.Lockstep;

/// <summary>
/// The host's command stamper (M9-03, design 20 §4.2 host-sequenced turn bundles). A command submitted at host tick t gets
/// <c>ExecTick = t + Delay</c> (never a tick already sealed) and the next Sequence of its player from host-owned counters, whatever
/// the sender wrote in those fields. <see cref="Seal"/> hands out one tick's commands in a fixed order (player, then sequence), so
/// every peer receives the same list. A peer may only submit for the player of its slot (<see cref="SetSlot"/>); anything else is
/// dropped and counted in <see cref="Rejected"/>. The host's own AIs use <see cref="SubmitTrusted"/>. A bundle never grows past
/// <see cref="MaxCommandsPerTick"/> or <see cref="MaxCommandsPerPlayerPerTick"/> per player: further commands spill into the next
/// unsealed ticks (deterministic, the host decides), and a player with <see cref="MaxPendingPerPlayer"/> commands waiting is refused (M9-14).
/// </summary>
public sealed class TurnScheduler
{
    /// <summary>Most commands of one player in one tick's bundle; more spill into the next ticks.</summary>
    public const int MaxCommandsPerPlayerPerTick = 64;

    /// <summary>Most commands in one tick's bundle (well under <see cref="NetCodec.MaxCommands"/>); more spill into the next ticks.</summary>
    public const int MaxCommandsPerTick = 1024;

    /// <summary>Most stamped but unsealed commands of one player; more are refused and counted in <see cref="Rejected"/>.</summary>
    public const int MaxPendingPerPlayer = 1024;

    private readonly List<Command> _pending = new();
    private readonly ushort[] _sequence = new ushort[256];   // Command.Player is a byte
    private readonly Dictionary<int, byte> _slotOf = new();
    private readonly int[] _pendingOf = new int[256];
    private readonly Dictionary<int, int> _countAt = new();          // exec tick -> commands stamped for it
    private readonly Dictionary<long, int> _playerCountAt = new();   // (exec tick, player) -> commands stamped for it

    /// <summary>Creates a scheduler for a fixed command delay (ticks, at least 1).</summary>
    public TurnScheduler(int delay)
    {
        if (delay < 1) throw new ArgumentOutOfRangeException(nameof(delay), "Command delay must be at least 1 tick.");
        Delay = delay;
        SealedThrough = delay - 1;   // bundles 0..delay-1 are implicitly empty
    }

    /// <summary>The command delay in ticks.</summary>
    public int Delay { get; }

    /// <summary>The last tick sealed so far (Delay - 1 before the first <see cref="Seal"/>).</summary>
    public int SealedThrough { get; private set; }

    /// <summary>Commands refused because the sender had no slot, wrote another player's id, or had too many commands waiting.</summary>
    public int Rejected { get; private set; }

    /// <summary>Commands stamped but not yet sealed.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Lets <paramref name="peer"/> submit commands for <paramref name="player"/> (one player per peer).</summary>
    public void SetSlot(int peer, byte player) => _slotOf[peer] = player;

    /// <summary>Takes the slot away from a peer (dropped or spectating); its later submissions are rejected.</summary>
    public void ClearSlot(int peer) => _slotOf.Remove(peer);

    /// <summary>The player a peer may command, if any.</summary>
    public bool TryGetSlot(int peer, out byte player) => _slotOf.TryGetValue(peer, out player);

    /// <summary>
    /// Stamps and queues a command from <paramref name="peer"/> received at host tick <paramref name="hostTick"/>. Returns false (and
    /// counts it) when the peer has no slot or the command names another player.
    /// </summary>
    public bool Submit(int peer, Command command, int hostTick)
    {
        if (!_slotOf.TryGetValue(peer, out byte player) || command.Player != player)
        {
            Rejected++;
            return false;
        }
        return TrySubmitTrusted(command, hostTick, out _);
    }

    /// <summary>
    /// Stamps and queues a command without a slot check (the host's AIs, which may play any slot). Returns it as stamped, or
    /// <c>default</c> when the player already has <see cref="MaxPendingPerPlayer"/> commands waiting (counted in <see cref="Rejected"/>).
    /// </summary>
    public Command SubmitTrusted(Command command, int hostTick) => TrySubmitTrusted(command, hostTick, out Command stamped) ? stamped : default;

    /// <summary>As <see cref="SubmitTrusted"/>; false (and counted) when the player has too many commands waiting.</summary>
    public bool TrySubmitTrusted(Command command, int hostTick, out Command stamped)
    {
        byte player = command.Player;
        if (_pendingOf[player] >= MaxPendingPerPlayer)
        {
            Rejected++;
            stamped = default;
            return false;
        }
        int exec = Math.Max(hostTick + Delay, SealedThrough + 1);
        while (CountAt(exec) >= MaxCommandsPerTick || PlayerCountAt(exec, player) >= MaxCommandsPerPlayerPerTick) exec++;
        stamped = command with { ExecTick = exec, Sequence = _sequence[player]++ };
        _pending.Add(stamped);
        _pendingOf[player]++;
        _countAt[exec] = CountAt(exec) + 1;
        _playerCountAt[Key(exec, player)] = PlayerCountAt(exec, player) + 1;
        return true;
    }

    private static long Key(int tick, byte player) => (long)tick << 8 | player;

    private int CountAt(int tick) => _countAt.TryGetValue(tick, out int n) ? n : 0;

    private int PlayerCountAt(int tick, byte player) => _playerCountAt.TryGetValue(Key(tick, player), out int n) ? n : 0;

    /// <summary>
    /// Seals tick <paramref name="execTick"/>, which must be <see cref="SealedThrough"/> + 1, and returns its commands ordered by
    /// player then sequence (often none). Later submissions can no longer land on it.
    /// </summary>
    public Command[] Seal(int execTick)
    {
        if (execTick != SealedThrough + 1)
            throw new InvalidOperationException($"Seal ticks in order: expected {SealedThrough + 1}, got {execTick}.");
        var due = new List<Command>();
        for (int i = 0; i < _pending.Count; i++)
            if (_pending[i].ExecTick == execTick) due.Add(_pending[i]);
        _pending.RemoveAll(c => c.ExecTick == execTick);
        _countAt.Remove(execTick);
        foreach (Command c in due)
        {
            _pendingOf[c.Player]--;
            _playerCountAt.Remove(Key(execTick, c.Player));
        }
        // sequences are unique per player, so (player, sequence) is a total order; insertion sort keeps it allocation-light
        Command[] result = due.ToArray();
        for (int i = 1; i < result.Length; i++)
        {
            Command c = result[i];
            int j = i;
            while (j > 0 && Before(c, result[j - 1])) { result[j] = result[j - 1]; j--; }
            result[j] = c;
        }
        SealedThrough = execTick;
        return result;
    }

    private static bool Before(Command a, Command b) =>
        a.Player != b.Player ? a.Player < b.Player : a.Sequence < b.Sequence;
}
