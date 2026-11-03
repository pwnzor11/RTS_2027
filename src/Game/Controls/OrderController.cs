using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>
/// Turns keys and right-clicks into orders for the selected units. Every order goes through
/// <see cref="SimSnapshotSource.Order"/>, the same path a script uses. Keys: A arms attack-move (the next right-click
/// on the ground is an attack-move; A again disarms), S stops, G guards at each unit's own cell, X scatters.
/// Right-click: an enemy under the cursor is an Attack, the ground is a Move (or an attack-move when armed).
/// </summary>
public sealed class OrderController
{
    private readonly SimSnapshotSource _source;
    private readonly Selection _selection;

    /// <summary>Creates the controller for the local player's selection.</summary>
    public OrderController(SimSnapshotSource source, Selection selection)
    {
        _source = source;
        _selection = selection;
    }

    /// <summary>True after A, until the next right-click or A again.</summary>
    public bool AttackMoveArmed { get; private set; }

    /// <summary>Handles a key that went down this frame. Ignores keys that are not orders.</summary>
    public void KeyPressed(Keys key)
    {
        switch (key)
        {
            case Keys.A:
                AttackMoveArmed = !AttackMoveArmed;
                break;
            case Keys.S:
                IssueToSelection(CommandKind.Stop);
                break;
            case Keys.X:
                IssueToSelection(CommandKind.Scatter);
                break;
            case Keys.G:
                foreach (UnitKey unit in _selection.Units.ToList())
                {
                    if (_source.TryGetPosition(unit, out LeptonCoord at)) _source.Order(CommandKind.Guard, unit, at.ToCell());
                }
                break;
        }
    }

    /// <summary>Handles a right-click at a screen point, using the units as drawn this frame for picking.</summary>
    public void RightClick(Point screen, Camera camera, IReadOnlyList<RenderUnit> units)
    {
        bool armed = AttackMoveArmed;
        AttackMoveArmed = false;
        if (_selection.Count == 0) return;

        Vector2 world = camera.WorldFromScreen(screen);
        CellCoord cell = Camera.CellAt(world);
        ActorRef? enemy = TargetPicker.Pick(units, _source.Current, world, SimSnapshotSource.LocalPlayer);
        foreach (UnitKey unit in _selection.Units.ToList())
        {
            if (enemy is ActorRef target)
            {
                _source.Order(CommandKind.Attack, unit, default, target);
            }
            else if (_source.Map.Contains(cell))
            {
                _source.Order(armed ? CommandKind.AttackMove : CommandKind.Move, unit, cell);
            }
        }
    }

    private void IssueToSelection(CommandKind kind)
    {
        foreach (UnitKey unit in _selection.Units.ToList()) _source.Order(kind, unit);
    }
}
