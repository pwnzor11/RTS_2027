// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// The small dust cloud where a destructible prop fell (M12-16, D-264): a few puffs of the theater's dust colour at the cell of every
/// <see cref="FxKind.PropDestroyed"/> event. Once per sim tick however many frames show a snapshot (it remembers the tick it last saw, like
/// <see cref="DustEmitter"/>), silent after a restart or a jump back. View only: nothing here is sim state.
/// </summary>
public sealed class PropDust
{
    /// <summary>Puffs per destroyed prop. ASSUMED (S31): small, the dust of one crushed tree.</summary>
    public const int PuffsPerProp = 3;

    /// <summary>Ticks a prop's dust lasts. ASSUMED (S31): a little longer than a vehicle's trail puff.</summary>
    public const int Life = 28;

    private int _tick = int.MinValue;

    /// <summary>Adds the puffs for the prop events of <paramref name="snapshot"/> (once per tick).</summary>
    /// <param name="snapshot">The snapshot about to be drawn.</param>
    /// <param name="field">Where the puffs go.</param>
    /// <param name="dust">Dust colour 0xRRGGBB (the theater's mood dust).</param>
    public void Observe(SimSnapshot snapshot, PuffField field, uint dust)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(field);
        if (snapshot.Tick == _tick) return;
        bool silent = snapshot.Tick < _tick || _tick == int.MinValue;
        _tick = snapshot.Tick;
        if (silent || snapshot.FxEvents is not { Count: > 0 } events) return;
        foreach (FxEvent e in events)
        {
            if (e.Kind != FxKind.PropDestroyed) continue;
            var centre = Camera.WorldFromLepton(e.Pos);
            for (int i = 0; i < PuffsPerProp; i++)
            {
                uint h = DecorHash.Mix(0x5052_4F50UL, e.Pos.X, e.Pos.Y, (uint)(snapshot.Tick * 4 + i));
                float dx = ((int)(h & 31) - 15.5f) * 0.6f, dy = ((int)((h >> 5) & 15) - 7.5f) * 0.5f;
                field.Add(new Puff(centre.X + dx, centre.Y + dy + 4, snapshot.Tick, Life, dust, 170, 0.8f, 10));
            }
        }
    }
}
