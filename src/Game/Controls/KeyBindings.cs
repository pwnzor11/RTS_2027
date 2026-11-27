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
    private readonly KeyChord[] _keys;   // indexed by (int)GameAction
    private readonly KeyChord[] _alt;    // QOL-20 (D-313): the second key of each action, KeyChord.None when it has none

    /// <summary>The factory bindings.</summary>
    public static KeyBindings Default { get; } = new(DefaultKeys());

    /// <summary>
    /// M16 U-06 (Q-91): the "Laptop keys" preset: the factory bindings with the twelve sidebar cameos on Alt + 1-9, 0, -, = (no numpad needed).
    /// The numpad defaults are replaced (the second keys stay empty).
    /// </summary>
    public static KeyBindings Laptop { get; } = BuildLaptop();

    private KeyBindings(KeyChord[] keys, KeyChord[]? alt = null)
    {
        _keys = keys;
        _alt = alt ?? new KeyChord[keys.Length];
    }

    private static KeyBindings BuildLaptop()
    {
        Keys[] cameoKeys =
        {
            Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0, Keys.OemMinus, Keys.OemPlus,
        };
        KeyBindings result = Default;
        for (int i = 0; i < cameoKeys.Length; i++) result = result.With(GameAction.CameoSlot1 + i, new KeyChord(cameoKeys[i], true));
        return result;
    }

    /// <summary>The key part of the binding of <paramref name="action"/> (see <see cref="GetChord"/> for the Alt flag).</summary>
    public Keys Get(GameAction action) => _keys[(int)action].Key;

    /// <summary>QOL-20 (D-313): the key part of the second binding of <paramref name="action"/>, or <see cref="Keys.None"/> when it has none (the default). Either binding does the action.</summary>
    public Keys GetAlt(GameAction action) => _alt[(int)action].Key;

    /// <summary>M16 U-06: the binding of <paramref name="action"/> with its Alt flag.</summary>
    public KeyChord GetChord(GameAction action) => _keys[(int)action];

    /// <summary>M16 U-06: the second binding of <paramref name="action"/> with its Alt flag (<see cref="KeyChord.None"/> when it has none).</summary>
    public KeyChord GetAltChord(GameAction action) => _alt[(int)action];

    /// <summary>True when the bare <paramref name="key"/> is one of the two bindings of <paramref name="action"/>.</summary>
    public bool Uses(GameAction action, Keys key) => Uses(action, KeyChord.Of(key));

    /// <summary>True when <paramref name="chord"/> is one of the two bindings of <paramref name="action"/>.</summary>
    public bool Uses(GameAction action, KeyChord chord) => !chord.IsNone && (_keys[(int)action] == chord || _alt[(int)action] == chord);

    /// <summary>True when <paramref name="action"/> still has its factory key.</summary>
    public bool IsDefault(GameAction action) => _keys[(int)action] == GameKeys.InfoOf(action).DefaultChord;

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
    public GameAction? ConflictOf(GameAction action, Keys key) => ConflictOf(action, KeyChord.Of(key));

    /// <summary>As <see cref="ConflictOf(GameAction, Keys)"/> for a whole chord: Alt + 1 and 1 do not clash.</summary>
    public GameAction? ConflictOf(GameAction action, KeyChord key)
    {
        KeyContext mine = GameKeys.InfoOf(action).Context;
        foreach (ActionInfo info in GameKeys.Infos)
        {
            if (info.Action == action || (_keys[(int)info.Action] != key && _alt[(int)info.Action] != key)) continue;
            if (Overlaps(mine, info.Context)) return info.Action;
        }

        return null;
    }

    /// <summary>These bindings with <paramref name="action"/> set to <paramref name="key"/>, with no conflict check (use <see cref="ConflictOf"/> first).</summary>
    public KeyBindings With(GameAction action, Keys key) => With(action, KeyChord.Of(key));

    /// <summary>As <see cref="With(GameAction, Keys)"/> for a whole chord.</summary>
    public KeyBindings With(GameAction action, KeyChord key)
    {
        if (_keys[(int)action] == key) return this;
        var copy = (KeyChord[])_keys.Clone();
        copy[(int)action] = key;
        return new KeyBindings(copy, _alt);
    }

    /// <summary>
    /// QOL-20 (D-313): these bindings with <paramref name="key"/> as the second key of <paramref name="action"/> (<see cref="Keys.None"/> clears it), with
    /// no conflict check (use <see cref="ConflictOf"/> first). A key equal to the action's own first key changes nothing.
    /// </summary>
    public KeyBindings WithAlt(GameAction action, Keys key) => WithAlt(action, KeyChord.Of(key));

    /// <summary>As <see cref="WithAlt(GameAction, Keys)"/> for a whole chord.</summary>
    public KeyBindings WithAlt(GameAction action, KeyChord key)
    {
        if (_alt[(int)action] == key || (!key.IsNone && _keys[(int)action] == key)) return this;
        var copy = (KeyChord[])_alt.Clone();
        copy[(int)action] = key;
        return new KeyBindings(_keys, copy);
    }

    /// <summary>
    /// Binds <paramref name="key"/> to <paramref name="action"/>; when another action had it, that action gets the key <paramref name="action"/>
    /// had (a swap). Null when the key is not bindable or the swap would itself clash with a third action.
    /// </summary>
    public KeyBindings? TrySwap(GameAction action, Keys key) => TrySwap(action, KeyChord.Of(key));

    /// <summary>As <see cref="TrySwap(GameAction, Keys)"/> for a whole chord.</summary>
    public KeyBindings? TrySwap(GameAction action, KeyChord key)
    {
        if (!IsBindable(key.Key)) return null;
        GameAction? other = ConflictOf(action, key);
        if (other is not { } o) return With(action, key);
        KeyBindings result = With(action, key).With(o, GetChord(action));
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
                if (Shares(i, j) && Overlaps(GameKeys.InfoOf((GameAction)i).Context, GameKeys.InfoOf((GameAction)j).Context))
                    list.Add(((GameAction)i, (GameAction)j));
            }
        }

        return list;
    }

    // True when actions i and j have a key in common (the second keys count; None never does).
    private bool Shares(int i, int j) =>
        _keys[i] == _keys[j] || (!_alt[i].IsNone && (_alt[i] == _keys[j] || _alt[i] == _alt[j])) || (!_alt[j].IsNone && _alt[j] == _keys[i]);

    /// <summary>QOL-20 (D-313): the second keys that are set, as (settings id, key name) pairs in action order.</summary>
    public IReadOnlyList<(string Id, string Key)> AltOverrides()
    {
        var list = new List<(string, string)>();
        foreach (ActionInfo info in GameKeys.Infos)
            if (!_alt[(int)info.Action].IsNone) list.Add((info.Id, _alt[(int)info.Action].ToSetting()));
        return list;
    }

    /// <summary>The bindings that differ from the defaults, as (settings id, key name) pairs in action order.</summary>
    public IReadOnlyList<(string Id, string Key)> Overrides()
    {
        var list = new List<(string, string)>();
        foreach (ActionInfo info in GameKeys.Infos)
        {
            if (!IsDefault(info.Action)) list.Add((info.Id, GetChord(info.Action).ToSetting()));
        }

        return list;
    }

    /// <summary>
    /// Builds bindings from saved (id, key name) pairs. Unknown ids, unknown or unbindable key names are ignored (that action keeps its
    /// default); when the rest still clash, the clashing actions go back to their defaults, so the result never has a conflict.
    /// </summary>
    public static KeyBindings FromOverrides(IEnumerable<(string Id, string Key)> saved) => FromOverrides(saved, Array.Empty<(string Id, string Key)>());

    /// <summary>As <see cref="FromOverrides(IEnumerable{ValueTuple{string, string}})"/>, with the saved second keys (QOL-20); a second key that is unknown, unbindable or equal to its action's first key is ignored.</summary>
    public static KeyBindings FromOverrides(IEnumerable<(string Id, string Key)> saved, IEnumerable<(string Id, string Key)> savedAlt)
    {
        KeyChord[] keys = DefaultKeys();
        foreach ((string id, string name) in saved)
        {
            ActionInfo? info = GameKeys.Infos.FirstOrDefault(i => i.Id == id);
            if (info is null || !KeyChord.TryParse(name, out KeyChord key) || !IsBindable(key.Key)) continue;
            keys[(int)info.Action] = key;
        }

        var alts = new KeyChord[keys.Length];
        foreach ((string id, string name) in savedAlt)
        {
            ActionInfo? info = GameKeys.Infos.FirstOrDefault(i => i.Id == id);
            if (info is null || !KeyChord.TryParse(name, out KeyChord key) || !IsBindable(key.Key) || keys[(int)info.Action] == key) continue;
            alts[(int)info.Action] = key;
        }

        var result = new KeyBindings(keys, alts);
        while (result.Conflicts() is { Count: > 0 } clashes)   // each pass resets at least one non-default key, so it ends
        {
            foreach ((GameAction a, GameAction b) in clashes)
            {
                result = result.With(a, GameKeys.InfoOf(a).DefaultChord).With(b, GameKeys.InfoOf(b).DefaultChord).WithAlt(a, KeyChord.None).WithAlt(b, KeyChord.None);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public bool Equals(KeyBindings? other) => other is not null && _keys.AsSpan().SequenceEqual(other._keys) && _alt.AsSpan().SequenceEqual(other._alt);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as KeyBindings);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (KeyChord k in _keys) h.Add(k);
        foreach (KeyChord k in _alt) h.Add(k);
        return h.ToHashCode();
    }

    private static bool Overlaps(KeyContext a, KeyContext b) => a == KeyContext.Both || b == KeyContext.Both || a == b;

    private static KeyChord[] DefaultKeys()
    {
        var keys = new KeyChord[Enum.GetValues<GameAction>().Length];
        foreach (ActionInfo info in GameKeys.Infos) keys[(int)info.Action] = info.DefaultChord;
        return keys;
    }
}
