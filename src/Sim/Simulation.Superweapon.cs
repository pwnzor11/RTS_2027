// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Superweapon (design 05 §4: the Sunrise Device, D-077). OWNER: M3-13. State: Effects.BuildingCharge (ticks charged per building slot).
//
// RULES
//  * A live building whose type has Superweapon != "" and SuperweaponChargeTicks > 0, owned by an undefeated player, gains one charge
//    tick per tick up to SuperweaponChargeTicks. Charge starts at 0 when the building appears.
//  * POWER (ASSUMED M3-13): at full power it charges at full rate; in power band 1 and 2 at 1000/LowPowerSlowdownPermille[band-1]
//    of the rate (the production slowdown reused; spread over ticks with an integer accumulator derived from Tick, no extra state);
//    in band 3 (output below 50 % of use) the charge pauses.
//  * Special (Subject = the building, Cell = target) fires when Charge == Total, the target is on the map and, if
//    SuperweaponEntranceRange > 0, an own live finished entrance building (IsTunnelNode) lies within that many cells of the target
//    (Chebyshev, nearest footprint cell). The blast is launched from the network: the entrance is a requirement, not a path.
//  * EFFECT: radius BlastRadius cells (Chebyshev); every non-friendly surface actor and building takes BlastDamage through the
//    warhead "<superweapon>_blast" (else "dir_meltdown_blast", else a flat 100 % table), half beyond BlastRadius/2; underground
//    actors are spared. Every cell in the radius gets +RadiationLevel radiation (cap 255) and Blackout for BlackoutTicks. Values come
//    from the warhead when it sets Contaminate / BlackoutTicks, else the constants below. The charge then resets to 0.
//  * FOG (ASSUMED M3-13): nobody is told that a superweapon is charging; the flash (FxKind.Superweapon) reaches viewers who can see the
//    target cell. The owner sees charge and readiness in PlayerHud.Superweapons.
public sealed partial class Simulation
{
    /// <summary>Blast radius in cells. ASSUMED (M3-13).</summary>
    internal const int SuperweaponBlastRadius = 5;
    /// <summary>Full damage at the centre half of the blast. ASSUMED (M3-13).</summary>
    internal const int SuperweaponDamage = 700;
    /// <summary>Radiation added to every cell in the blast when the warhead sets none. ASSUMED (M3-13).</summary>
    internal const int SuperweaponRadiation = 200;
    /// <summary>Blackout ticks laid on every cell in the blast when the warhead sets none. ASSUMED (M3-13).</summary>
    internal const int SuperweaponBlackoutTicks = 600;

    /// <summary>Special: Subject = own superweapon building, Cell = target.</summary>
    private void ApplySpecial(Command c)
    {
        if (!CanFireSuperweapon(c.Player, c.Subject, c.Cell)) return;
        int b = c.Subject.Index;
        BuildingType bt = BuildingTypeAt(b);
        LeptonCoord impact = CellCentre(c.Cell);
        WarheadType wh = SuperweaponWarhead(c.Player, bt);
        if (wh.StormTicks > 0 && wh.StormRadius > 0)
        {   // M6-07: a ground-effect storm instead of the blast damage path
            StartStorm(c.Player, c.Cell, wh);
            _fx.Add(new FxEvent(FxKind.Superweapon, impact, wh.StormRadius));
            _effects.BuildingCharge[b] = 0;
            return;
        }
        int radius = SuperweaponBlastRadius;
        int contaminate = wh.Contaminate > 0 ? wh.Contaminate : SuperweaponRadiation;
        int blackout = wh.BlackoutTicks > 0 ? wh.BlackoutTicks : SuperweaponBlackoutTicks;

        for (int y = c.Cell.Y - radius; y <= c.Cell.Y + radius; y++)
            for (int x = c.Cell.X - radius; x <= c.Cell.X + radius; x++)
            {
                var cell = new CellCoord(x, y);
                if (!_ground.Contains(cell)) continue;
                int idx = _ground.IndexOf(cell);
                _ground.Radiation[idx] = (byte)Math.Min(255, _ground.Radiation[idx] + contaminate);
                if (blackout > _ground.Blackout[idx]) _ground.Blackout[idx] = blackout;
            }

        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            for (int i = 1; i < slots.Capacity; i++)
            {
                if (!slots.IsIndexAlive(i) || HealthAt(kind, i) <= 0) continue;
                if (IsFriendly(c.Player, PlayerAt(kind, i))) continue;
                if (kind != ActorKind.Building && IsUnderground(kind, i)) continue;
                int d = kind == ActorKind.Building ? FootprintDistance(i, c.Cell) : LeptonMath.ChebyshevCells(c.Cell, PositionAt(kind, i).ToCell());
                if (d > radius) continue;
                Hurt(kind, i, SuperweaponDamage, wh, d > radius / 2, c.Player);
            }
        }
        ApplyPendingDamage();   // at once, as OnDeathWeapon does: the buffer is empty between ticks and must not carry into a save

        _fx.Add(new FxEvent(FxKind.Detonation, impact, radius));
        _fx.Add(new FxEvent(FxKind.Superweapon, impact, radius));
        _effects.BuildingCharge[b] = 0;
    }

    /// <summary>
    /// True if <paramref name="player"/> could fire the superweapon of <paramref name="building"/> at <paramref name="target"/> now:
    /// own, live, charged, on the map and (when the type needs one) with an own entrance within range. The Special command and
    /// the targeting cursor use the same check.
    /// </summary>
    public bool CanFireSuperweapon(byte player, EntityId building, CellCoord target)
    {
        if (player >= _players.Count || _players.Defeated[player] || building.IsNone || !_buildings.Slots.IsAlive(building)) return false;
        int b = building.Index;
        if (_buildings.Player[b] != player || _buildings.Health[b] <= 0) return false;
        BuildingType bt = BuildingTypeAt(b);
        if (!IsSuperweapon(bt) || _effects.BuildingCharge[b] < bt.SuperweaponChargeTicks) return false;
        if (!_ground.Contains(target)) return false;
        return bt.SuperweaponEntranceRange <= 0 || HasEntranceInRange(player, target, bt.SuperweaponEntranceRange);
    }

    /// <summary>True if an own live finished entrance building lies within <paramref name="range"/> cells of <paramref name="target"/>.</summary>
    public bool HasEntranceInRange(byte player, CellCoord target, int range)
    {
        for (int i = 1; i < _buildings.Capacity; i++)
            if (IsTunnelNode(i, player) && FootprintDistance(i, target) <= range) return true;
        return false;
    }

    /// <summary>Per tick (last in RunEffects): charge superweapon buildings.</summary>
    private void RunSuperweaponCharge()
    {
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i)) continue;
            BuildingType bt = BuildingTypeAt(i);
            if (!IsSuperweapon(bt) || _buildings.Health[i] <= 0 || _players.Defeated[_buildings.Player[i]]) continue;
            int rate = ChargeRatePermille(_buildings.Player[i]);
            // Integer accumulator from the tick number: advances on exactly rate/1000 of the ticks, no stored remainder.
            if ((long)(Tick + 1) * rate / 1000 == (long)Tick * rate / 1000) continue;
            if (_effects.BuildingCharge[i] < bt.SuperweaponChargeTicks) _effects.BuildingCharge[i]++;
        }
    }

    private static bool IsSuperweapon(BuildingType bt) => bt.Superweapon.Length > 0 && bt.SuperweaponChargeTicks > 0;

    /// <summary>Charge speed of a player's superweapons in per mille (1000 = full, 0 = paused); see the rules at the top.</summary>
    private int ChargeRatePermille(byte player)
    {
        int band = PowerBand(player);
        if (band == 0) return 1000;
        if (band >= 3) return 0;
        IReadOnlyList<int> slow = Setup.Rules.Economy.LowPowerSlowdownPermille;
        int s = band <= slow.Count ? slow[band - 1] : 1000;
        return s <= 1000 ? 1000 : 1_000_000 / s;
    }

    private WarheadType SuperweaponWarhead(byte owner, BuildingType bt)
    {
        IReadOnlyList<WarheadType> all = RulesOf(owner).Warheads;
        foreach (string id in new[] { bt.Superweapon + "_blast", "dir_meltdown_blast" })
            for (int w = 0; w < all.Count; w++) if (all[w].Id == id) return all[w];
        return new WarheadType("superweapon", new[] { 100, 100, 100, 100, 100 }, SuperweaponBlastRadius, false);
    }

    private int FootprintDistance(int building, CellCoord cell)
    {
        BuildingType bt = BuildingTypeAt(building);
        CellCoord tl = _buildings.Cell[building];
        int dx = Math.Max(Math.Max(tl.X - cell.X, cell.X - (tl.X + bt.FootprintW - 1)), 0);
        int dy = Math.Max(Math.Max(tl.Y - cell.Y, cell.Y - (tl.Y + bt.FootprintH - 1)), 0);
        return Math.Max(dx, dy);
    }

    private static LeptonCoord CellCentre(CellCoord c) =>
        new((c.X << SimConstants.LeptonShift) + SimConstants.LeptonsPerCell / 2, (c.Y << SimConstants.LeptonShift) + SimConstants.LeptonsPerCell / 2);

    private IReadOnlyList<SuperweaponView> SuperweaponViews(byte player)
    {
        List<SuperweaponView>? list = null;
        int rate = ChargeRatePermille(player);
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i) || _buildings.Player[i] != player || _buildings.Health[i] <= 0) continue;
            BuildingType bt = BuildingTypeAt(i);
            if (!IsSuperweapon(bt)) continue;
            int charge = Math.Min(_effects.BuildingCharge[i], bt.SuperweaponChargeTicks);
            bool ready = charge >= bt.SuperweaponChargeTicks;
            (list ??= new List<SuperweaponView>()).Add(new SuperweaponView(_buildings.Slots.IdOf(i), _buildings.TypeIndex[i], bt.Superweapon,
                charge, bt.SuperweaponChargeTicks, ready, !ready && rate == 0, bt.SuperweaponEntranceRange)
                { BlastRadius = SuperweaponBlastRadius, Owner = player });
        }
        return list ?? (IReadOnlyList<SuperweaponView>)Array.Empty<SuperweaponView>();
    }
}
