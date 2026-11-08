// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;

namespace Rts.Game.Controls;

/// <summary>
/// The click-a-binding, press-a-key flow of the Controls page (D-172 (6)) without any drawing: Idle, then Listening for a key, then
/// (when the key is taken) Conflict, which must be resolved by swapping or cancelling before anything changes.
/// </summary>
public sealed class RebindSession
{
    /// <summary>What the session is waiting for.</summary>
    public enum Phase
    {
        /// <summary>Nothing is being rebound.</summary>
        Idle,

        /// <summary>Waiting for the new key.</summary>
        Listening,

        /// <summary>The key pressed is used by another action; waiting for swap or cancel.</summary>
        Conflict,
    }

    /// <summary>The current phase.</summary>
    public Phase State { get; private set; }

    /// <summary>The action being rebound (meaningful unless Idle).</summary>
    public GameAction Action { get; private set; }

    /// <summary>The key waiting to be confirmed (Conflict phase).</summary>
    public Keys PendingKey { get; private set; }

    /// <summary>The action that has <see cref="PendingKey"/> now (Conflict phase).</summary>
    public GameAction ConflictWith { get; private set; }

    /// <summary>A one-line message for the player (why a key was refused, or what the conflict is); empty when there is nothing to say.</summary>
    public string Message { get; private set; } = "";

    /// <summary>Starts listening for a new key for <paramref name="action"/>.</summary>
    public void Begin(GameAction action)
    {
        Action = action;
        State = Phase.Listening;
        Message = "";
    }

    /// <summary>Leaves the flow without changing anything.</summary>
    public void Cancel()
    {
        State = Phase.Idle;
        Message = "";
    }

    /// <summary>
    /// Handles the key the player pressed while Listening. Returns the new bindings when the key was free and is now bound, otherwise
    /// null (refused, or a conflict is now waiting for <see cref="Swap"/> / <see cref="Cancel"/>).
    /// </summary>
    public KeyBindings? Press(KeyBindings current, Keys key)
    {
        if (State != Phase.Listening) return null;
        if (key == Keys.Escape)
        {
            Cancel();
            return null;
        }

        if (!KeyBindings.IsBindable(key))
        {
            Message = $"{KeyNames.Of(key)} cannot be used here. Press another key, or Esc to cancel.";
            return null;
        }

        if (current.ConflictOf(Action, key) is { } other)
        {
            PendingKey = key;
            ConflictWith = other;
            State = Phase.Conflict;
            Message = $"{KeyNames.Of(key)} is already used by \"{GameKeys.InfoOf(other).Description}\". Enter: swap the two keys.  Esc: cancel.";
            return null;
        }

        KeyBindings result = current.With(Action, key);
        Cancel();
        return result;
    }

    /// <summary>Resolves a Conflict by giving the other action this action's old key. Null (and the flow stays open) when the swap is not possible.</summary>
    public KeyBindings? Swap(KeyBindings current)
    {
        if (State != Phase.Conflict) return null;
        KeyBindings? result = current.TrySwap(Action, PendingKey);
        if (result is null)
        {
            Message = "That swap would clash with another key. Esc: cancel.";
            return null;
        }

        Cancel();
        return result;
    }
}
