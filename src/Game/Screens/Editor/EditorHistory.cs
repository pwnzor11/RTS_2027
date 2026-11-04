// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens.Editor;

/// <summary>The undo / redo command stack. Keeps the newest <see cref="Capacity"/> steps; a new step drops the redo tail.</summary>
public sealed class EditorHistory
{
    /// <summary>Default depth (design 08 §5 asks for at least 100).</summary>
    public const int DefaultCapacity = 128;

    private readonly List<EditorStep> _steps = new();
    private int _position;   // steps[0.._position) are applied

    /// <summary>Creates a history keeping up to <paramref name="capacity"/> steps (at least 100).</summary>
    public EditorHistory(int capacity = DefaultCapacity) => Capacity = Math.Max(100, capacity);

    /// <summary>Most steps kept.</summary>
    public int Capacity { get; }

    /// <summary>How many steps can be undone.</summary>
    public int UndoCount => _position;

    /// <summary>How many steps can be redone.</summary>
    public int RedoCount => _steps.Count - _position;

    /// <summary>Records an applied step. Empty steps are ignored.</summary>
    public void Push(EditorStep step)
    {
        if (step.IsEmpty) return;
        if (_position < _steps.Count) _steps.RemoveRange(_position, _steps.Count - _position);
        _steps.Add(step);
        if (_steps.Count > Capacity) _steps.RemoveAt(0);
        _position = _steps.Count;
    }

    /// <summary>The step an Undo would revert, without moving.</summary>
    public EditorStep? PeekUndo() => _position > 0 ? _steps[_position - 1] : null;

    /// <summary>The step a Redo would re-apply, without moving.</summary>
    public EditorStep? PeekRedo() => _position < _steps.Count ? _steps[_position] : null;

    /// <summary>Moves back one step and returns it (the caller reverts it), or null.</summary>
    public EditorStep? PopUndo() => _position > 0 ? _steps[--_position] : null;

    /// <summary>Moves forward one step and returns it (the caller re-applies it), or null.</summary>
    public EditorStep? PopRedo() => _position < _steps.Count ? _steps[_position++] : null;

    /// <summary>Forgets everything (new or opened map).</summary>
    public void Clear() { _steps.Clear(); _position = 0; }
}
