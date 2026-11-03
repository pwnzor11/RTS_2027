// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.View;

namespace Rts.AI;

/// <summary>
/// "skirmisher": the M1 placeholder AI (no economy yet; uses only the <see cref="PlayerView"/>). While its army is
/// at least AttackThreshold strong it attack-moves idle units at the nearest known enemy building, else the last
/// enemy unit it saw (until it is older than ScoutInfoAgeTicks), else the enemy start mirrored from its own. Below the
/// threshold idle units that wandered off guard near the start. With FocusFire it also gives explicit Attack orders
/// to the units that can already hit the weakest visible enemy. Scatter is reserved.
/// </summary>
public sealed class SkirmisherAi : IAiPlayer
{
    private const int GuardRadiusCells = 3;
    private const int HomeRadiusCells = 6;
    private const int ReissueDistanceCells = 2;

    private readonly int _attackThreshold;
    private readonly bool _focusFire;
    private readonly int _infoAgeTicks;

    private readonly List<ViewActor> _own = new();
    private readonly List<ViewActor> _seen = new();
    private readonly List<ViewActor> _buildings = new();

    private AiContext _ctx = null!;
    private CellCoord _enemyStart;
    private bool _haveLastSeen;
    private CellCoord _lastSeenCell;
    private int _lastSeenTick;

    /// <summary>Creates the AI for a tier's profile.</summary>
    public SkirmisherAi(DifficultyProfile profile)
    {
        _attackThreshold = profile.AttackThreshold;
        _focusFire = profile.FocusFire;
        _infoAgeTicks = profile.ScoutInfoAgeTicks;
    }

    /// <inheritdoc />
    public string Id => AiRegistry.SkirmisherId;

    /// <summary>Reserved flag: scatter orders are not issued yet.</summary>
    public bool Scatter { get; init; }

    /// <inheritdoc />
    public void Start(AiContext ctx)
    {
        _ctx = ctx;
        _enemyStart = new CellCoord(ctx.MapWidth - 1 - ctx.StartCell.X, ctx.MapHeight - 1 - ctx.StartCell.Y);
    }

    /// <inheritdoc />
    public void Think(PlayerView view, ICommandSink sink)
    {
        view.CopyOwn(_own, includeBuildings: false);
        view.CopyVisible(_seen);
        view.CopyKnownEnemyBuildings(_buildings);
        if (_own.Count == 0) return;

        long cx = 0, cy = 0;
        foreach (ViewActor a in _own) { cx += a.Position.X; cy += a.Position.Y; }
        var centre = new LeptonCoord((int)(cx / _own.Count), (int)(cy / _own.Count));

        RememberEnemy(view, centre);

        if (_own.Count >= _attackThreshold)
        {
            CellCoord goal = PickGoal(view, centre);
            foreach (ViewActor a in _own)
            {
                if (!a.Idle) continue;
                if (Chebyshev(a.Cell, goal) <= ReissueDistanceCells) continue;
                sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, goal);
            }
        }
        else
        {
            foreach (ViewActor a in _own)
            {
                if (!a.Idle || Chebyshev(a.Cell, _ctx.StartCell) <= HomeRadiusCells) continue;
                sink.Issue(CommandKind.Guard, a.Id, a.Kind, GuardSpot());
            }
        }

        if (_focusFire) FocusFire(view, sink);
    }

    private void RememberEnemy(PlayerView view, LeptonCoord centre)
    {
        long best = long.MaxValue;
        foreach (ViewActor e in _seen)
        {
            if (e.Relation != Relation.Enemy || !e.IsMobile) continue;
            long d = DistSq(e.Position, centre);
            if (d >= best) continue;
            best = d;
            _lastSeenCell = e.Cell;
            _lastSeenTick = view.Tick;
            _haveLastSeen = true;
        }
    }

    private CellCoord PickGoal(PlayerView view, LeptonCoord centre)
    {
        long best = long.MaxValue;
        CellCoord goal = default;
        bool found = false;
        foreach (ViewActor b in _buildings)
        {
            long d = DistSq(b.Position, centre);
            if (d >= best) continue;
            best = d;
            goal = b.Cell;
            found = true;
        }
        if (found) return goal;
        if (_haveLastSeen && view.Tick - _lastSeenTick <= _infoAgeTicks) return _lastSeenCell;
        return _enemyStart;
    }

    private CellCoord GuardSpot()
    {
        int dx = _ctx.Rng.Next(2 * GuardRadiusCells + 1) - GuardRadiusCells;
        int dy = _ctx.Rng.Next(2 * GuardRadiusCells + 1) - GuardRadiusCells;
        int x = Math.Clamp(_ctx.StartCell.X + dx, 0, _ctx.MapWidth - 1);
        int y = Math.Clamp(_ctx.StartCell.Y + dy, 0, _ctx.MapHeight - 1);
        return new CellCoord(x, y);
    }

    /// <summary>Explicit Attack on the lowest-health visible enemy that some of our units can already hit.</summary>
    private void FocusFire(PlayerView view, ICommandSink sink)
    {
        ViewActor target = default;
        bool have = false;
        foreach (ViewActor e in _seen)
        {
            if (e.Relation != Relation.Enemy) continue;
            if (have && (e.Health > target.Health || (e.Health == target.Health && !Before(e, target)))) continue;
            if (!AnyInRange(view, e)) continue;
            target = e;
            have = true;
        }
        if (!have) return;
        foreach (ViewActor a in _own)
        {
            if (!InRange(view, a, target)) continue;
            if (a.Mission == MissionKind.Attack && a.Target == target.Id) continue;
            sink.Issue(CommandKind.Attack, a.Id, a.Kind, default, target.Ref);
        }
    }

    private bool AnyInRange(PlayerView view, ViewActor enemy)
    {
        foreach (ViewActor a in _own)
            if (InRange(view, a, enemy)) return true;
        return false;
    }

    private static bool InRange(PlayerView view, ViewActor shooter, ViewActor enemy)
    {
        int range = view.WeaponRange(shooter.Kind, shooter.TypeIndex);
        if (range <= 0) return false;
        // Buildings: allow for the footprint's extent (distance is measured to the footprint, not its centre).
        long reach = range + (enemy.Kind == ActorKind.Building
            ? Math.Max(enemy.FootprintW, enemy.FootprintH) * SimConstants.LeptonsPerCell / 2 : 0);
        return DistSq(shooter.Position, enemy.Position) <= reach * reach;
    }

    private static bool Before(ViewActor a, ViewActor b) =>
        a.Kind != b.Kind ? a.Kind < b.Kind : a.Id.Index < b.Id.Index;

    private static int Chebyshev(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static long DistSq(LeptonCoord a, LeptonCoord b)
    {
        long dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
