// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Directives;

/// <summary>
/// The "directive" state part (M5-01, docs/design/09_m5_plan.md §2): the latest <see cref="Directive"/> per player slot. Appended after
/// "stats". Written by <see cref="CommandKind.Directive"/> only; the sim never acts on it, the AI reads it through the player view.
/// Layout: version, player count, then every field of every slot (all slots, so a stray value in an unset slot still hashes).
/// </summary>
public sealed class DirectiveState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>1 once the slot received a directive.</summary>
    public readonly byte[] Set;
    public readonly byte[] Posture;
    public readonly byte[] Focus;
    public readonly byte[] Target;
    public readonly byte[] Mix;
    public readonly byte[] Expand;
    public readonly int[] FocusX;
    public readonly int[] FocusY;
    /// <summary>Tick the slot's directive was applied.</summary>
    public readonly int[] AppliedTick;

    /// <summary>Creates the part with every slot at <see cref="Directive.None"/>.</summary>
    public DirectiveState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int n = setup.PlayerCount;
        Set = new byte[n]; Posture = new byte[n]; Focus = new byte[n]; Target = new byte[n]; Mix = new byte[n]; Expand = new byte[n];
        FocusX = new int[n]; FocusY = new int[n]; AppliedTick = new int[n];
        for (int p = 0; p < n; p++) Reset(p);
    }

    /// <inheritdoc />
    public string Name => "directive";

    /// <summary>The directive of <paramref name="player"/> (<see cref="Directive.None"/> before the first one).</summary>
    public Directive Of(byte player) => player >= Set.Length
        ? Directive.None
        : new Directive(Set[player] != 0, Posture[player], Focus[player], Target[player], Mix[player], Expand[player] != 0,
            new CellCoord(FocusX[player], FocusY[player]), AppliedTick[player]);

    /// <summary>Stores an already clamped directive.</summary>
    internal void Store(byte player, byte posture, byte focus, byte target, byte mix, bool expand, CellCoord focusCell, int tick)
    {
        Set[player] = 1; Posture[player] = posture; Focus[player] = focus; Target[player] = target; Mix[player] = mix;
        Expand[player] = expand ? (byte)1 : (byte)0; FocusX[player] = focusCell.X; FocusY[player] = focusCell.Y; AppliedTick[player] = tick;
    }

    private void Reset(int p)
    {
        Directive d = Directive.None;
        Set[p] = 0; Posture[p] = d.Posture; Focus[p] = d.Focus; Target[p] = d.TargetPlayer; Mix[p] = d.Mix; Expand[p] = 0;
        FocusX[p] = d.FocusCell.X; FocusY[p] = d.FocusCell.Y; AppliedTick[p] = 0;
    }

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(Set.Length);
        for (int p = 0; p < Set.Length; p++)
        {
            w.Write(Set[p]); w.Write(Posture[p]); w.Write(Focus[p]); w.Write(Target[p]); w.Write(Mix[p]); w.Write(Expand[p]);
            w.Write(FocusX[p]); w.Write(FocusY[p]); w.Write(AppliedTick[p]);
        }
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("directive version mismatch.");
        if (r.ReadInt32() != Set.Length) throw new InvalidDataException("directive player count mismatch.");
        for (int p = 0; p < Set.Length; p++)
        {
            Set[p] = r.ReadByte(); Posture[p] = r.ReadByte(); Focus[p] = r.ReadByte(); Target[p] = r.ReadByte(); Mix[p] = r.ReadByte();
            Expand[p] = r.ReadByte(); FocusX[p] = r.ReadInt32(); FocusY[p] = r.ReadInt32(); AppliedTick[p] = r.ReadInt32();
        }
    }
}
