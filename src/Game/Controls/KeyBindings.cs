// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;

namespace Rts.Game.Controls;

/// <summary>
/// The player's key for every <see cref="GameAction"/> (D-172 (6)). Immutable: only the keys that differ from the defaults are stored,
/// so a settings file without them (or with unknown ones) gives the defaults. Two actions may not share a key while their
/// <see cref="KeyContext"/>s overlap; <see cref="ConflictOf"/> finds the clash and <see cref="TrySwap"/> resolves it.
/// </summary>
public sealed class KeyBindings : IEquatable<KeyBindings>
{
    private readonly Keys[] _keys;   // indexed by (int)GameAction

    /// <summary>The factory bindings.</summary>
    public static KeyBindings Default { get; } = new(DefaultKeys());

    private KeyBindings(Keys[] keys) => _keys = keys;

    /// <summary>The key bound to <paramref name="action"/>.</summary>
    public Keys Get(GameAction action) => _keys[(int)action];

    /// <summary>True when <paramref name="action"/> still has its factory key.</summary>
    public bool IsDefault(GameAction action) => _keys[(int)action] == GameKeys.InfoOf(action).Default;

    /// <summary>True when every action has its factory key.</summary>
    public bool IsAllDefault => this.Equals(Default);

    /// <summary>
    /// True when <paramref name="key"/> may be bound: not None, not Escape or F1 (the pause menu and help stay reachable), not
    /// Backspace (chat editing), not Shift or Ctrl (fixed modifiers), not Windows / menu keys.
    /// </summary>
    public static bool IsBindable(Keys key) =>
        key is not (Keys.None or Keys.Escape or Keys.F1 or Keys.Back or Keys.LeftShift or Keys.RightShift or Keys.LeftControl or Keys.RightControl
            or Keys.LeftAlt or Keys.RightAlt or Keys.LeftWindows or Keys.RightWindows or Keys.Apps or Keys.CapsLock or Keys.NumLock or Keys.Scroll)
        && Enum.IsDefined(key);

    /// <summary>The other action that already uses <paramref name="key"/> in an overlapping context, or null when the key is free for <paramref name="action"/>.</summary>
    public GameAction? ConflictOf(GameAction action, Keys key)
    {
        KeyContext mine = GameKeys.InfoOf(action).Context;
        foreach (ActionInfo info in GameKeys.Infos)
        {
            if (info.Action == action || _keys[(int)info.Action] != key) continue;
            if (Overlaps(mine, info.Context)) return info.Action;
        }

        return null;
    }

    /// <summary>These bindings with <paramref name="action"/> set to <paramref name="key"/>, with no conflict check (use <see cref="ConflictOf"/> first).</summary>
    public KeyBindings With(GameAction action, Keys key)
    {
        if (_keys[(int)action] == key) return this;
        var copy = (Keys[])_keys.Clone();
        copy[(int)action] = key;
        return new KeyBindings(copy);
    }

    /// <summary>
    /// Binds <paramref name="key"/> to <paramref name="action"/>; when another action had it, that action gets the key <paramref name="action"/>
    /// had (a swap). Null when the key is not bindable or the swap would itself clash with a third action.
    /// </summary>
    public KeyBindings? TrySwap(GameAction action, Keys key)
    {
        if (!IsBindable(key)) return null;
        GameAction? other = ConflictOf(action, key);
        if (other is not { } o) return With(action, key);
        KeyBindings result = With(action, key).With(o, Get(action));
        return result.Conflicts().Count == 0 ? result : null;
    }

    /// <summary>Every pair of actions that share a key in overlapping contexts (empty for a valid set).</summary>
    public IReadOnlyList<(GameAction A, GameAction B)> Conflicts()
    {
        var list = new List<(GameAction, GameAction)>();
        for (int i = 0; i < _keys.Length; i++)
        {
            for (int j = i + 1; j < _keys.Length; j++)
            {
                if (_keys[i] == _keys[j] && Overlaps(GameKeys.InfoOf((GameAction)i).Context, GameKeys.InfoOf((GameAction)j).Context))
                    list.Add(((GameAction)i, (GameAction)j));
            }
        }

        return list;
    }

    /// <summary>The bindings that differ from the defaults, as (settings id, key name) pairs in action order.</summary>
    public IReadOnlyList<(string Id, string Key)> Overrides()
    {
        var list = new List<(string, string)>();
        foreach (ActionInfo info in GameKeys.Infos)
        {
            if (!IsDefault(info.Action)) list.Add((info.Id, Get(info.Action).ToString()));
        }

        return list;
    }

    /// <summary>
    /// Builds bindings from saved (id, key name) pairs. Unknown ids, unknown or unbindable key names are ignored (that action keeps its
    /// default); when the rest still clash, the clashing actions go back to their defaults, so the result never has a conflict.
    /// </summary>
    public static KeyBindings FromOverrides(IEnumerable<(string Id, string Key)> saved)
    {
        Keys[] keys = DefaultKeys();
        foreach ((string id, string name) in saved)
        {
            ActionInfo? info = GameKeys.Infos.FirstOrDefault(i => i.Id == id);
            if (info is null || !Enum.TryParse(name, ignoreCase: true, out Keys key) || !IsBindable(key)) continue;
            keys[(int)info.Action] = key;
        }

        var result = new KeyBindings(keys);
        while (result.Conflicts() is { Count: > 0 } clashes)   // each pass resets at least one non-default key, so it ends
        {
            foreach ((GameAction a, GameAction b) in clashes)
            {
                result = result.With(a, GameKeys.InfoOf(a).Default).With(b, GameKeys.InfoOf(b).Default);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public bool Equals(KeyBindings? other) => other is not null && _keys.AsSpan().SequenceEqual(other._keys);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as KeyBindings);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (Keys k in _keys) h.Add((int)k);
        return h.ToHashCode();
    }

    private static bool Overlaps(KeyContext a, KeyContext b) => a == KeyContext.Both || b == KeyContext.Both || a == b;

    private static Keys[] DefaultKeys()
    {
        var keys = new Keys[Enum.GetValues<GameAction>().Length];
        foreach (ActionInfo info in GameKeys.Infos) keys[(int)info.Action] = info.Default;
        return keys;
    }
}
