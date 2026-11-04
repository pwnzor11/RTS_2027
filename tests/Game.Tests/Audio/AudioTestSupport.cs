// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Audio;

/// <summary>Helpers for the audio tests: locating the repo, building small snapshots, and a recording IAudio.</summary>
internal static class AudioTestSupport
{
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "audio", "sfxr.py"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    public static string AudioDir() => Path.Combine(RepoRoot(), "data", "audio");

    public static SimSnapshot Snap(int tick, UnitView[]? units = null, BuildingView[]? buildings = null, FxEvent[]? fx = null,
        TelegraphView[]? telegraphs = null, byte[]? ground = null, int w = 16, int h = 16) =>
        new(tick, w, h, units ?? Array.Empty<UnitView>(), new LandType[w * h], 0UL, buildings ?? Array.Empty<BuildingView>(), 0,
            Array.Empty<byte>(), fx)
        {
            Telegraphs = telegraphs ?? Array.Empty<TelegraphView>(),
            Ground = ground ?? Array.Empty<byte>(),
        };

    public static UnitView Unit(int index, byte player, int cellX, int cellY, int health = 100, bool underground = false) =>
        new(new EntityId((ushort)index, 1), UnitKind.Vehicle, player,
            0, LeptonCoord.CenterOf(new CellCoord(cellX, cellY)), 0, health)
        {
            Underground = underground,
        };

    public static BuildingView Building(int index, byte player, int cellX, int cellY, int health = 100) =>
        new(new EntityId((ushort)index, 1), player, 0, new CellCoord(cellX, cellY), 2, 2, health, false);

    public static PlayerHud Hud(int cash = 1000, int powerOut = 100, int powerUse = 50, int readyStructure = -1, bool stalled = false, ulong upgrades = 0) =>
        new(cash, 0, 0, powerOut, powerUse,
            new[]
            {
                new QueueView(ProductionCategory.Structure,
                    stalled ? new[] { new QueueItemView(0, 100, true) } : Array.Empty<QueueItemView>(), readyStructure),
            },
            Array.Empty<(ProductionCategory, int)>(), false)
        {
            Upgrades = upgrades,
        };

    public static List<AudioEvent> Derive(AudioEventDeriver d, AudioFrame f)
    {
        var list = new List<AudioEvent>();
        d.Derive(f, list);
        return list;
    }

    public static AudioFrame Frame(SimSnapshot prev, SimSnapshot cur, PlayerHud? ph = null, PlayerHud? ch = null) =>
        new(prev, cur, 0) { PreviousHud = ph, CurrentHud = ch };
}

/// <summary>An IAudio that records what it is asked to play.</summary>
internal sealed class RecordingAudio : IAudio
{
    public List<(string Id, float Gain, float Pan)> Sfx { get; } = new();
    public List<string> Music { get; } = new();
    public List<bool> MusicLoops { get; } = new();
    public int Stops { get; private set; }
    public float Fade { get; private set; } = 1f;
    public int MusicVolume { get; set; } = 100;
    public int SfxVolume { get; set; } = 100;
    public void PlaySfx(string id) => Sfx.Add((id, 1f, 0f));
    public void PlaySfx(string id, float gain, float pan) => Sfx.Add((id, gain, pan));
    public void PlayMusic(string id) => PlayMusic(id, true);
    public void PlayMusic(string id, bool loop)
    {
        Music.Add(id);
        MusicLoops.Add(loop);
    }

    public void SetMusicFade(float gain) => Fade = gain;
    public void StopMusic() => Stops++;
}
