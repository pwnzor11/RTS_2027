// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// What the Directorate Recruit does beyond the shared loop (design 05 sections 1 and 3). Fog-honest: it reads only its
/// <see cref="PlayerView"/> and the order history it keeps itself.
/// <list type="bullet">
/// <item>Entrances: every production structure and the Smelter Vent is already a network node. Forward entrances are dug by Bores:
/// the first toward the enemy (outside its defences), the second beside the ore field the Scraper works (a cell it stood on while
/// harvesting), then alternating, up to <see cref="MaxForward"/> standing at once.</item>
/// <item>Transit: when a wave launches and a finished forward entrance is clearly closer to the target, the wave stages at that
/// entrance and members that fit it (infantry in a Hatch) are ordered Enter to it; they ride the network and surface there.</item>
/// <item>Auras: an Agitprop (one, once the army has a few units) is kept at the centre of the wave, or at the rally cell.</item>
/// <item>Radiation: rally and staging cells are moved off irradiated cells, and idle non-resistant infantry standing on one are
/// moved to the nearest clean cell.</item>
/// </list>
/// </summary>
internal sealed class DirectorateTactics : IFactionTactics
{
    /// <summary>Forward entrances (Hatch / Bore Head away from the base) wanted at once.</summary>
    public const int MaxForward = 3;   // ASSUMED (M3-10)
    /// <summary>Bores built over a whole game (a destroyed Hatch is dug again only up to this many).</summary>
    public const int MaxBoresBuilt = 5;   // ASSUMED (M3-10)
    /// <summary>Army size before the first Bore is bought (units first, the Bore costs as much as a rocket truck).</summary>
    public const int ArmyBeforeBore = 4;   // ASSUMED (M3-10)
    /// <summary>Credits kept in hand after buying a Bore.</summary>
    public const int BoreCushion = 200;   // ASSUMED (M3-10)
    /// <summary>Ticks between Bore purchases.</summary>
    public const int BoreGapTicks = 1500;   // ASSUMED (M3-10)
    /// <summary>Cells from the yard before an entrance counts as forward.</summary>
    public const int ForwardMinCells = 10;   // ASSUMED (M3-10)
    /// <summary>Cells short of the enemy target at which a Hatch is dug (outside most defence range).</summary>
    public const int DigShortCells = 15;   // ASSUMED (M3-10)
    /// <summary>A forward entrance must be this many cells nearer the target than the yard to be a staging point.</summary>
    public const int ForwardGainCells = 8;   // ASSUMED (M3-10)
    /// <summary>A unit nearer than this to the stage walks instead of using the network.</summary>
    public const int TransitMinCells = 14;   // ASSUMED (M3-10)
    /// <summary>Ticks a harvester must stand still on a Harvest mission before its cell counts as the field.</summary>
    public const int FieldSettleTicks = 160;   // ASSUMED (M3-10)
    /// <summary>Armies this big get an Agitprop.</summary>
    public const int ArmyBeforeAgitprop = 6;   // ASSUMED (M3-10)
    private const int CleanSearchRadius = 6, TransitReorderTicks = 600, SupportSlack = 2;

    private int _bore = -1, _agitprop = -1;
    private int _mapW, _mapH;
    private int _dugOrders, _boresBought, _lastBoreBuy = -BoreGapTicks;
    private int _tick;
    private CellCoord _field;
    private bool _haveField;
    private readonly Dictionary<ulong, (CellCoord Cell, int Since)> _stood = new();
    private readonly Dictionary<ulong, int> _entered = new();
    private readonly HashSet<ulong> _deployOrdered = new();
    private readonly List<TelegraphView> _telegraphs = new();
    private EntityId _fwdId;
    private CellCoord _fwdExit;
    private int _fwdType = -1;

    /// <summary>Deploy orders given to Bores (Hatches dug).</summary>
    public int Digs { get; private set; }

    /// <summary>Enter orders given for network transit.</summary>
    public int Transits { get; private set; }

    /// <summary>Move orders that took infantry off irradiated cells.</summary>
    public int RadiationMoves { get; private set; }

    /// <inheritdoc />
    public void Start(AiContext ctx)
    {
        _mapW = ctx.MapWidth; _mapH = ctx.MapHeight;
    }

    private bool _resolved;

    private void Resolve(SimRules rules)
    {
        if (_resolved) return;
        _resolved = true;
        _bore = -1; _agitprop = -1;
        for (int i = 0; i < rules.Vehicles.Count; i++)
        {
            VehicleType v = rules.Vehicles[i];
            if (v.Faction != Faction.Directorate) continue;
            if (_bore < 0 && v.DeploysInto >= 0 && v.Locomotion == Locomotion.Burrow) _bore = i;
            if (_agitprop < 0 && v.AuraRadius > 0) _agitprop = i;
        }
    }

    /// <inheritdoc />
    public void Think(RecruitKnowledge k, ICommandSink sink, CellCoord goal, CellCoord rally, LeptonCoord? waveCentre)
    {
        Resolve(k.Rules);
        _tick = k.View.Tick;
        TrackField(k);
        DeployBores(k, sink, goal);
        MoveSupport(k, sink, rally, waveCentre);
        ClearIrradiatedInfantry(k, sink);
    }

    // ---- fields ---------------------------------------------------------------------------------

    /// <summary>Remembers the cell a Scraper has been working on (it stood still while on a Harvest mission): the ore field.</summary>
    private void TrackField(RecruitKnowledge k)
    {
        foreach (ViewActor h in k.Harvesters)
        {
            ulong key = Key(h);
            if (h.Mission != MissionKind.Harvest || h.Underground) { _stood.Remove(key); continue; }
            if (!_stood.TryGetValue(key, out var s) || s.Cell != h.Cell) { _stood[key] = (h.Cell, _tick); continue; }
            if (_tick - s.Since < FieldSettleTicks) continue;
            // the field nearest the yard that is still a real trip away would not need an entrance; take any that is far enough
            if (Cheb(h.Cell, k.Yard) >= ForwardMinCells) { _field = h.Cell; _haveField = true; }
        }
    }

    // ---- entrances ------------------------------------------------------------------------------

    /// <summary>Counts forward entrances standing (finished or emerging).</summary>
    private int ForwardCount(RecruitKnowledge k)
    {
        int n = 0;
        foreach (ViewActor b in k.Buildings)
            if (k.Rules.Buildings[b.TypeIndex].Entrance != TunnelSize.None && Cheb(b.Cell, k.Yard) >= ForwardMinCells) n++;
        return n;
    }

    /// <inheritdoc />
    public int WantedVehicle(RecruitKnowledge k)
    {
        Resolve(k.Rules);
        int army = k.Army.Count;
        // an Agitprop for the wave: one at a time
        if (_agitprop >= 0 && army >= ArmyBeforeAgitprop && k.Support.Count == 0 && k.Buildable(ProductionCategory.Vehicle, _agitprop))
            return _agitprop;
        if (_bore < 0 || army < ArmyBeforeBore || k.Harvesters.Count == 0) return -1;
        if (k.Bores.Count > 0 || _boresBought >= MaxBoresBuilt || ForwardCount(k) >= MaxForward) return -1;
        if (_tick - _lastBoreBuy < BoreGapTicks || !k.Buildable(ProductionCategory.Vehicle, _bore)) return -1;
        if (k.Hud.Cash + k.Hud.Stored < k.Rules.Vehicles[_bore].Cost + BoreCushion) return -1;
        _boresBought++;
        _lastBoreBuy = _tick;
        return _bore;
    }

    private void DeployBores(RecruitKnowledge k, ICommandSink sink, CellCoord goal)
    {
        foreach (ViewActor b in k.Bores)
        {
            ulong key = Key(b);
            if (!b.Idle || _deployOrdered.Contains(key)) continue;
            CellCoord site = Site(k, goal, _dugOrders);
            _deployOrdered.Add(key);
            _dugOrders++;
            Digs++;
            sink.Issue(CommandKind.Deploy, b.Id, b.Kind, site);
        }
    }

    /// <summary>Where the n-th entrance goes: even n toward the enemy (the target pulled back <see cref="DigShortCells"/> toward the
    /// yard, shifted sideways for later ones), odd n beside the ore field (or halfway to the target when no field is known).</summary>
    private CellCoord Site(RecruitKnowledge k, CellCoord goal, int n)
    {
        if (n % 2 == 1)
        {
            if (_haveField) return Clamp(new CellCoord(_field.X + Math.Sign(k.Yard.X - _field.X) * 2, _field.Y + Math.Sign(k.Yard.Y - _field.Y) * 2));
        }
        long dx = k.Yard.X - goal.X, dy = k.Yard.Y - goal.Y;
        long len = Math.Max(1, (long)Rts.Sim.Numerics.LeptonMath.ISqrt((ulong)(dx * dx + dy * dy)));
        int back = n % 2 == 1 ? (int)(len / 2) : DigShortCells;
        int side = n / 2 * 4 * (n / 2 % 2 == 0 ? 1 : -1);
        int x = (int)(goal.X + dx * back / len - dy * side / len), y = (int)(goal.Y + dy * back / len + dx * side / len);
        return Clamp(new CellCoord(x, y));
    }

    private CellCoord Clamp(CellCoord c) => new(Math.Clamp(c.X, 2, _mapW - 3), Math.Clamp(c.Y, 2, _mapH - 3));

    // ---- transit --------------------------------------------------------------------------------

    /// <inheritdoc />
    public CellCoord? ForwardStage(RecruitKnowledge k, CellCoord goal)
    {
        _fwdId = EntityId.None; _fwdType = -1;
        k.View.CopyTelegraphs(_telegraphs);
        long yardDist = Dist(k.Yard, goal), best = long.MaxValue;
        long gain = (long)ForwardGainCells;
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType bt = k.Rules.Buildings[b.TypeIndex];
            if (bt.Entrance == TunnelSize.None || Cheb(b.Cell, k.Yard) < ForwardMinCells || Emerging(b)) continue;
            long d = Dist(b.Cell, goal);
            if (d + gain >= yardDist || d >= best) continue;
            best = d; _fwdId = b.Id; _fwdType = b.TypeIndex;
            _fwdExit = new CellCoord(b.Cell.X + bt.Exit.X, b.Cell.Y + bt.Exit.Y);
        }
        return _fwdId.IsNone ? null : _fwdExit;
    }

    private bool Emerging(ViewActor b)
    {
        foreach (TelegraphView t in _telegraphs)
            if (t.Kind == TelegraphKind.Entrance && t.Cell == b.Cell) return true;
        return false;
    }

    /// <inheritdoc />
    public bool TryEnter(RecruitKnowledge k, ICommandSink sink, ViewActor a, CellCoord stage)
    {
        if (_fwdId.IsNone || _fwdType < 0 || a.Underground) return false;
        TunnelSize unit = a.Kind == ActorKind.Infantry ? k.Rules.Infantry[a.TypeIndex].TunnelSize
            : a.Kind == ActorKind.Vehicle ? k.Rules.Vehicles[a.TypeIndex].TunnelSize : TunnelSize.None;
        TunnelSize door = k.Rules.Buildings[_fwdType].Entrance;
        if (unit == TunnelSize.None || unit > door || Cheb(a.Cell, stage) <= TransitMinCells) return false;
        // the destination must still stand (the view lists own buildings only while alive)
        bool standing = false;
        foreach (ViewActor b in k.Buildings) if (b.Id == _fwdId) { standing = true; break; }
        if (!standing) return false;
        ulong key = Key(a);
        if (_entered.TryGetValue(key, out int since) && _tick - since < TransitReorderTicks) return false;
        _entered[key] = _tick;
        Transits++;
        sink.Issue(CommandKind.Enter, a.Id, a.Kind, default, new ActorRef(ActorKind.Building, _fwdId));
        return true;
    }

    // ---- auras ----------------------------------------------------------------------------------

    private void MoveSupport(RecruitKnowledge k, ICommandSink sink, CellCoord rally, LeptonCoord? waveCentre)
    {
        CellCoord to = waveCentre?.ToCell() ?? rally;
        to = SafeCell(k, to);
        foreach (ViewActor s in k.Support)
            if (s.Idle && Cheb(s.Cell, to) > SupportSlack || Cheb(s.Cell, to) > 6)
                sink.Issue(CommandKind.Move, s.Id, s.Kind, to);
    }

    // ---- radiation ------------------------------------------------------------------------------

    /// <inheritdoc />
    public CellCoord SafeCell(RecruitKnowledge k, CellCoord cell)
    {
        int limit = k.Rules.Radiation.DamageThreshold / 2;
        if (!k.View.InMap(cell) || k.View.RadiationAt(cell) < limit) return cell;
        return NearestClean(k, cell, limit) ?? cell;
    }

    private static CellCoord? NearestClean(RecruitKnowledge k, CellCoord from, int limit)
    {
        for (int r = 1; r <= CleanSearchRadius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(from.X + dx, from.Y + dy);
                    if (k.View.InMap(c) && k.View.IsPassable(c, Locomotion.Foot) && k.View.RadiationAt(c) < limit) return c;
                }
        return null;
    }

    /// <summary>Idle infantry that is not radiation resistant and stands on a cell at or above the damage threshold moves to the
    /// nearest clean cell. Resistant units (Directorate infantry resists 60 percent, crawlers are immune) still leave: 40 percent hurts.</summary>
    private void ClearIrradiatedInfantry(RecruitKnowledge k, ICommandSink sink)
    {
        int threshold = k.Rules.Radiation.DamageThreshold;
        foreach (ViewActor a in k.Army)
        {
            if (a.Kind != ActorKind.Infantry || !a.Idle || a.Underground) continue;
            if (k.Rules.Infantry[a.TypeIndex].RadResistPermille >= 1000 || k.View.RadiationAt(a.Cell) < threshold) continue;
            CellCoord? clean = NearestClean(k, a.Cell, threshold / 2);
            if (clean is not CellCoord c) continue;
            RadiationMoves++;
            sink.Issue(CommandKind.Move, a.Id, a.Kind, c);
        }
    }

    private static ulong Key(ViewActor a) => ((ulong)a.Id.Index << 16) | a.Id.Generation | ((ulong)(int)a.Kind << 48);
    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    private static long Dist(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the Bore and field memory, the order memory and the counters.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        w.Write(_dugOrders); w.Write(_boresBought); w.Write(_lastBoreBuy); w.Write(_tick);
        AiSnapshot.WriteCell(w, _field); w.Write(_haveField);
        AiSnapshot.WriteMap(w, _stood, (bw, v) => { AiSnapshot.WriteCell(bw, v.Cell); bw.Write(v.Since); });
        AiSnapshot.WriteMap(w, _entered, (bw, v) => bw.Write(v));
        AiSnapshot.WriteKeys(w, _deployOrdered);
        AiSnapshot.WriteId(w, _fwdId); AiSnapshot.WriteCell(w, _fwdExit); w.Write(_fwdType);
        w.Write(Digs); w.Write(Transits); w.Write(RadiationMoves);
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r)
    {
        _dugOrders = r.ReadInt32(); _boresBought = r.ReadInt32(); _lastBoreBuy = r.ReadInt32(); _tick = r.ReadInt32();
        _field = AiSnapshot.ReadCell(r); _haveField = r.ReadBoolean();
        AiSnapshot.ReadMap(r, _stood, br => (AiSnapshot.ReadCell(br), br.ReadInt32()));
        AiSnapshot.ReadMap(r, _entered, br => br.ReadInt32());
        AiSnapshot.ReadKeys(r, _deployOrdered);
        _fwdId = AiSnapshot.ReadId(r); _fwdExit = AiSnapshot.ReadCell(r); _fwdType = r.ReadInt32();
        Digs = r.ReadInt32(); Transits = r.ReadInt32(); RadiationMoves = r.ReadInt32();
    }
}
