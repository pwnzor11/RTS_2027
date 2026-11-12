// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Audio;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Audio;

/// <summary>The real data/rules, the real sounds.toml and an <see cref="AudioRules"/> built from them, for the S22 audio tests.</summary>
internal static class AudioRulesFixture
{
    public static readonly SimRules Rules = LoadRules();
    public static readonly SoundBank Bank = SoundBank.Load(Path.Combine(AudioTestSupport.AudioDir(), "sounds.toml"));
    public static readonly AudioRules Info = AudioRules.Build(Rules, Bank);

    private static SimRules LoadRules()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        return RulesLoader.Load(dir).Sim;
    }

    public static int Veh(string id) => Enumerable.Range(0, Rules.Vehicles.Count).First(i => Rules.Vehicles[i].Id == id);

    public static int Inf(string id) => Enumerable.Range(0, Rules.Infantry.Count).First(i => Rules.Infantry[i].Id == id);

    public static int Bld(string id) => Enumerable.Range(0, Rules.Buildings.Count).First(i => Rules.Buildings[i].Id == id);

    public static int Wpn(string id) => Enumerable.Range(0, Rules.Weapons.Count).First(i => Rules.Weapons[i].Id == id);

    public static LeptonCoord Cell(int x, int y) => LeptonCoord.CenterOf(new CellCoord(x, y));

    public static UnitView Vehicle(int slot, byte player, string typeId, int x, int y, int health = 100) =>
        new(new EntityId((ushort)slot, 1), UnitKind.Vehicle, player, Veh(typeId), Cell(x, y), 0, health);

    public static UnitView Soldier(int slot, byte player, string typeId, int x, int y, int health = 100) =>
        new(new EntityId((ushort)slot, 1), UnitKind.Infantry, player, Inf(typeId), Cell(x, y), 0, health);

    public static BuildingView Building(int slot, byte player, string typeId, int x, int y) =>
        new(new EntityId((ushort)slot, 1), player, Bld(typeId), new CellCoord(x, y), 2, 2, 100, false);

    /// <summary>Derives one step with the real rules attached.</summary>
    public static List<AudioEvent> Derive(SimSnapshot prev, SimSnapshot cur, Func<AudioFrame, AudioFrame>? edit = null, PlayerHud? hud = null)
    {
        var d = new AudioEventDeriver();
        return Derive(d, prev, cur, edit, hud);
    }

    public static List<AudioEvent> Derive(AudioEventDeriver d, SimSnapshot prev, SimSnapshot cur, Func<AudioFrame, AudioFrame>? edit = null, PlayerHud? hud = null)
    {
        AudioFrame f = new AudioFrame(prev, cur, 0) { PreviousHud = hud, CurrentHud = hud, Rules = Info };
        if (edit is not null) f = edit(f);
        return AudioTestSupport.Derive(d, f);
    }
}
