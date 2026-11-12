// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Net.Lockstep;

/// <summary>
/// The host's command stamper (M9-03, design 20 §4.2 host-sequenced turn bundles). A command submitted at host tick t gets
/// <c>ExecTick = t + Delay</c> (never a tick already sealed) and the next Sequence of its player from host-owned counters, whatever
/// the sender wrote in those fields. <see cref="Seal"/> hands out one tick's commands in a fixed order (player, then sequence), so
/// every peer receives the same list. A peer may only submit for the player of its slot (<see cref="SetSlot"/>); anything else is
/// dropped and counted in <see cref="Rejected"/>. The host's own AIs use <see cref="SubmitTrusted"/>.
/// </summary>
public sealed class TurnScheduler
{
    private readonly List<Command> _pending = new();
    private readonly ushort[] _sequence = new ushort[256];   // Command.Player is a byte
    private readonly Dictionary<int, byte> _slotOf = new();

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

    /// <summary>Commands refused because the sender had no slot or wrote another player's id.</summary>
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
        SubmitTrusted(command, hostTick);
        return true;
    }

    /// <summary>Stamps and queues a command without a slot check (the host's AIs, which may play any slot). Returns it as stamped.</summary>
    public Command SubmitTrusted(Command command, int hostTick)
    {
        int exec = Math.Max(hostTick + Delay, SealedThrough + 1);
        Command stamped = command with { ExecTick = exec, Sequence = _sequence[command.Player]++ };
        _pending.Add(stamped);
        return stamped;
    }

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
