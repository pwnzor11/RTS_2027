// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Audio;

/// <summary>One sound file: <see cref="Volume"/> scales it, <see cref="MaxVoices"/> caps how many copies play at once.</summary>
public sealed record SoundDef(string Id, string File, float Volume, int MaxVoices);

/// <summary>An event's variants and how it is played. <see cref="CooldownMs"/> is the least time between two plays of the event.</summary>
public sealed record EventDef(string Name, IReadOnlyList<string> Sounds, float Volume, bool Positional, int CooldownMs);

/// <summary>A looping ambience: the sound that plays for a biome key (<c>glass_coast</c>, <c>open_sea</c> ...) and how loud.</summary>
public sealed record AmbienceDef(string Key, string SoundId, float Volume);

/// <summary>
/// data/audio/sounds.toml: the sound files, and the mapping from game events to sound ids (several variants per event,
/// one picked at random). Event names may be qualified (<c>unit_died.infantry.directorate</c>, S22): <see cref="Resolve"/> tries the most
/// specific key first and falls back to the plain event. Parsing throws <see cref="InvalidDataException"/> on a malformed file.
/// </summary>
public sealed class SoundBank
{
    private readonly Dictionary<string, SoundDef> _sounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EventDef> _events = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _weapons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AmbienceDef> _ambience = new(StringComparer.Ordinal);
    private readonly Dictionary<(AudioEventKind, string?, string?), EventDef?> _resolved = new();

    private SoundBank()
    {
    }

    /// <summary>Default cap on copies of one sound playing at once.</summary>
    public int DefaultMaxVoices { get; private set; } = 4;

    /// <summary>Distance in cells beyond which a positional sound is silent.</summary>
    public float HearingRadiusCells { get; private set; } = 48f;

    /// <summary>Horizontal distance in cells that pans a sound fully to one side.</summary>
    public float PanRadiusCells { get; private set; } = 24f;

    /// <summary>Every sound, ordered by id.</summary>
    public IEnumerable<SoundDef> Sounds => _sounds.Values.OrderBy(s => s.Id, StringComparer.Ordinal);

    /// <summary>Every event, ordered by name.</summary>
    public IEnumerable<EventDef> Events => _events.Values.OrderBy(e => e.Name, StringComparer.Ordinal);

    /// <summary>Every ambience loop, ordered by key.</summary>
    public IEnumerable<AmbienceDef> Ambience => _ambience.Values.OrderBy(a => a.Key, StringComparer.Ordinal);

    /// <summary>The ambience for a biome key, or null when sounds.toml has none (that biome is then silent).</summary>
    public AmbienceDef? AmbienceFor(string key) => _ambience.GetValueOrDefault(key);

    /// <summary>Weapon id and sound class of every entry of the [weapon] table, ordered by weapon id.</summary>
    public IEnumerable<KeyValuePair<string, string>> WeaponClassTable => _weapons.OrderBy(w => w.Key, StringComparer.Ordinal);

    /// <summary>Loads and validates the file at <paramref name="path"/>.</summary>
    public static SoundBank Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>Parses sounds.toml text.</summary>
    public static SoundBank Parse(string text)
    {
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        }
        catch (Exception e)
        {
            throw new InvalidDataException("sounds.toml: " + e.Message, e);
        }

        var bank = new SoundBank();
        if (root.TryGetValue("settings", out object? so) && so is TomlTable settings)
        {
            bank.DefaultMaxVoices = Math.Max(1, (int)Num(settings, "default_max_voices", bank.DefaultMaxVoices));
            bank.HearingRadiusCells = Math.Max(1f, (float)Num(settings, "hearing_radius_cells", bank.HearingRadiusCells));
            bank.PanRadiusCells = Math.Max(1f, (float)Num(settings, "pan_radius_cells", bank.PanRadiusCells));
        }

        foreach ((string id, TomlTable t) in Tables(root, "sound"))
        {
            string file = Str(t, "file") ?? throw new InvalidDataException($"sounds.toml: sound '{id}' has no file");
            bank._sounds[id] = new SoundDef(id, file, (float)Num(t, "volume", 1.0),
                Math.Max(1, (int)Num(t, "max_voices", bank.DefaultMaxVoices)));
        }

        if (root.TryGetValue("event", out object? eo) && eo is TomlTable eventGroup)
        {
            foreach ((string name, TomlTable t) in FlattenEvents(eventGroup, ""))
            {
                var ids = new List<string>();
                if (t.TryGetValue("sounds", out object? arr) && arr is TomlArray list)
                {
                    foreach (object? item in list)
                    {
                        if (item is string s) ids.Add(s);
                    }
                }

                if (ids.Count == 0) throw new InvalidDataException($"sounds.toml: event '{name}' lists no sounds");
                foreach (string id in ids)
                {
                    if (!bank._sounds.ContainsKey(id)) throw new InvalidDataException($"sounds.toml: event '{name}' names unknown sound '{id}'");
                }

                bool positional = t.TryGetValue("positional", out object? p) && p is bool b && b;
                bank._events[name] = new EventDef(name, ids, (float)Num(t, "volume", 1.0), positional, (int)Num(t, "cooldown_ms", 0));
            }
        }

        foreach ((string key, TomlTable t) in Tables(root, "ambience"))
        {
            string soundId = Str(t, "sound") ?? throw new InvalidDataException($"sounds.toml: ambience '{key}' has no sound");
            if (!bank._sounds.ContainsKey(soundId)) throw new InvalidDataException($"sounds.toml: ambience '{key}' names unknown sound '{soundId}'");
            bank._ambience[key] = new AmbienceDef(key, soundId, (float)Num(t, "volume", 1.0));
        }

        if (root.TryGetValue("weapon", out object? wo) && wo is TomlTable weapons)
        {
            foreach ((string weaponId, object? value) in weapons)
            {
                if (value is string evt) bank._weapons[weaponId] = evt;
            }
        }

        return bank;
    }

    /// <summary>The sound with this id, or null.</summary>
    public SoundDef? Sound(string id) => _sounds.GetValueOrDefault(id);

    /// <summary>The event definition for <paramref name="kind"/>, or null when sounds.toml has none (the event is then silent).</summary>
    public EventDef? Event(AudioEventKind kind) => _events.GetValueOrDefault(AudioEventNames.Of(kind));

    /// <summary>
    /// The sound class of a weapon id (the [weapon] table of sounds.toml, e.g. <c>auth_rocket = "rocket"</c>; see <see cref="WeaponClasses"/>),
    /// or null when the weapon is not listed. A table value that is an old event name (<c>fire_missile</c>) is not a class and gives null here;
    /// <see cref="EventForWeapon"/> still understands it.
    /// </summary>
    public string? WeaponClassOf(string weaponId) =>
        _weapons.TryGetValue(weaponId, out string? v) && WeaponClasses.IsKnown(v) ? v : null;

    /// <summary>
    /// The event family a weapon id fires: <see cref="AudioEventKind.FireBullet"/>, <see cref="AudioEventKind.FireShell"/> or
    /// <see cref="AudioEventKind.FireMissile"/>, from its class (or from an old-style event name in the [weapon] table). Null when the weapon is not
    /// listed or its class makes no shot sound (<c>silent</c>, <c>bomb</c>).
    /// </summary>
    public AudioEventKind? EventForWeapon(string weaponId)
    {
        if (!_weapons.TryGetValue(weaponId, out string? name)) return null;
        if (WeaponClasses.IsKnown(name)) return WeaponClasses.FamilyOf(name);
        foreach (AudioEventKind kind in Enum.GetValues<AudioEventKind>())
        {
            if (AudioEventNames.Of(kind) == name) return kind;
        }

        return null;
    }

    /// <summary>
    /// The event definition to play for <paramref name="e"/> (S22). Keys are tried most specific first: <c>name.class.faction</c>,
    /// <c>name.class</c>, <c>name.faction</c>, <c>name</c> (a missing class or faction is skipped). When none exists the event's stand-in
    /// (<see cref="AudioFallbacks.Next"/>: unit_died to explosion_small ...) is tried the same way. Null means silence. The answer is cached.
    /// </summary>
    public EventDef? Resolve(AudioEvent e)
    {
        var key = (e.Kind, e.Class, e.Faction);
        if (_resolved.TryGetValue(key, out EventDef? hit)) return hit;
        EventDef? found = null;
        for (AudioEvent? cur = e; cur is { } c && found is null; cur = AudioFallbacks.Next(c))
        {
            found = ResolveOne(c);
        }

        _resolved[key] = found;
        return found;
    }

    /// <summary>The keys <see cref="Resolve"/> tries for one event (without its stand-ins), most specific first.</summary>
    public static IReadOnlyList<string> LookupKeys(AudioEvent e)
    {
        string name = AudioEventNames.Of(e.Kind);
        var keys = new List<string>(4);
        if (e.Class is not null && e.Faction is not null) keys.Add($"{name}.{e.Class}.{e.Faction}");
        if (e.Class is not null) keys.Add($"{name}.{e.Class}");
        if (e.Faction is not null) keys.Add($"{name}.{e.Faction}");
        keys.Add(name);
        return keys;
    }

    private EventDef? ResolveOne(AudioEvent e)
    {
        foreach (string key in LookupKeys(e))
        {
            if (_events.TryGetValue(key, out EventDef? def)) return def;
        }

        return null;
    }

    /// <summary>Picks one variant of <paramref name="def"/> with <paramref name="rng"/> (game-side randomness, not the sim's).</summary>
    public SoundDef Pick(EventDef def, Random rng) => _sounds[def.Sounds[def.Sounds.Count == 1 ? 0 : rng.Next(def.Sounds.Count)]];

    /// <summary>Sound ids whose file is not under <paramref name="audioDir"/> (the folder holding sounds.toml).</summary>
    public IReadOnlyList<string> MissingFiles(string audioDir) =>
        _sounds.Values.Where(s => !System.IO.File.Exists(Path.Combine(audioDir, s.File))).Select(s => s.Id).OrderBy(s => s, StringComparer.Ordinal).ToList();

    // [event.fire_shell] with a child table [event.fire_shell.cannon.authority] arrives as nested tables; a table with a "sounds" key is an event
    // named by its path, and its child tables are searched too. (A quoted key [event."fire_shell.cannon"] works as well.)
    private static IEnumerable<(string Name, TomlTable Table)> FlattenEvents(TomlTable group, string prefix)
    {
        foreach ((string key, object? value) in group)
        {
            if (value is not TomlTable t) continue;
            string name = prefix.Length == 0 ? key : prefix + "." + key;
            if (t.ContainsKey("sounds")) yield return (name, t);
            foreach ((string, TomlTable) inner in FlattenEvents(t, name)) yield return inner;
        }
    }

    private static IEnumerable<(string Key, TomlTable Table)> Tables(TomlTable root, string name)
    {
        if (!root.TryGetValue(name, out object? o) || o is not TomlTable group) yield break;
        foreach ((string key, object? value) in group)
        {
            if (value is TomlTable t) yield return (key, t);
        }
    }

    private static string? Str(TomlTable t, string key) => t.TryGetValue(key, out object? v) && v is string s ? s : null;

    private static double Num(TomlTable t, string key, double fallback) =>
        t.TryGetValue(key, out object? v)
            ? v switch
            {
                long l => l,
                double d => d,
                _ => fallback,
            }
            : fallback;
}
