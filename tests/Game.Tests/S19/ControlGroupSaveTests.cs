// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Tests.Saves;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S19;

/// <summary>D-205: control groups are saved with the game (header version 7) and old saves load with none.</summary>
public sealed class ControlGroupSaveTests
{
    private static UnitKey V(int index) => new(ActorKind.Vehicle, new EntityId((ushort)index, 1));

    private static UnitKey I(int index) => new(ActorKind.Infantry, new EntityId((ushort)index, 2));

    private static ControlGroups Groups()
    {
        var g = new ControlGroups();
        g.Set(1, new[] { V(3), V(4), I(9) });
        g.Set(5, new[] { I(2) });
        g.Set(9, new[] { V(7) });
        return g;
    }

    private static GameSaveHeader Header(int version, IReadOnlyList<IReadOnlyList<UnitKey>> groups) => new()
    {
        Version = version, Mode = SaveMode.Skirmish, MapId = "generated:64:77", MapName = "Gen", RulesHash = 1, Tick = 5,
        Skirmish = new Rts.Game.Screens.SkirmishSettings(77, 3, 2, Rts.Sim.Rules.Faction.Directorate, Rts.Sim.Rules.Faction.Authority),
        ControlGroups = groups,
    };

    private static GameSaveHeader RoundTrip(GameSaveHeader h)
    {
        using var t = new SaveTestSupport();
        (SimHost host, _) = t.NewSkirmish();
        var ms = new MemoryStream();
        GameSaveFile.Write(ms, h, host.Sim, Array.Empty<(string, string)>());
        return GameSaveFile.ReadHeader(new MemoryStream(ms.ToArray()));
    }

    [Fact]
    public void Groups_snapshot_and_restore_give_the_same_groups()
    {
        ControlGroups original = Groups();
        var copy = new ControlGroups();
        copy.Restore(original.Snapshot());
        for (int n = 1; n <= ControlGroups.Count; n++) Assert.Equal(original.Members(n), copy.Members(n));
        Assert.Equal(1, copy.GroupOf(V(3)));
        Assert.Equal(5, copy.GroupOf(I(2)));
    }

    [Fact]
    public void Restore_replaces_old_groups_and_keeps_one_group_per_unit()
    {
        var g = new ControlGroups();
        g.Set(2, new[] { V(1) });
        g.Restore(new IReadOnlyList<UnitKey>[] { new[] { V(5) }, new[] { V(5), V(6) } });
        Assert.Equal(new[] { V(5) }, g.Members(1));
        Assert.Equal(new[] { V(6) }, g.Members(2));   // V(5) stays in the first group it was listed in
        Assert.Equal(0, g.GroupOf(V(1)));
    }

    [Fact]
    public void Header_round_trips_the_groups()
    {
        GameSaveHeader back = RoundTrip(Header(GameSaveHeader.CurrentVersion, Groups().Snapshot()));
        Assert.Equal(7, back.Version);
        Assert.Equal(ControlGroups.Count, back.ControlGroups.Count);
        Assert.Equal(new[] { V(3), V(4), I(9) }, back.ControlGroups[0]);
        Assert.Empty(back.ControlGroups[1]);
        Assert.Equal(new[] { I(2) }, back.ControlGroups[4]);
        Assert.Equal(new[] { V(7) }, back.ControlGroups[8]);
    }

    [Fact]
    public void An_old_version_6_save_loads_with_no_groups()
    {
        GameSaveHeader back = RoundTrip(Header(6, Groups().Snapshot()));   // a v6 file has no group section, whatever the object held
        Assert.Equal(6, back.Version);
        Assert.Empty(back.ControlGroups);
        Assert.Equal(5, back.Tick);
    }

    [Fact]
    public void A_damaged_group_section_is_refused_not_trusted()
    {
        using var t = new SaveTestSupport();
        (SimHost host, _) = t.NewSkirmish();
        var ms = new MemoryStream();
        GameSaveFile.Write(ms, Header(7, new IReadOnlyList<UnitKey>[] { new[] { V(1) } }), host.Sim, Array.Empty<(string, string)>());
        byte[] bytes = ms.ToArray();
        int headerLength = BitConverter.ToInt32(bytes, 6);
        int groupStart = 10 + headerLength - (1 + 2 + 5);   // count byte, member count, one 5-byte unit: the section is the end of the header
        bytes[groupStart] = 200;                              // 200 groups
        Assert.Throws<SaveLoadException>(() => GameSaveFile.ReadHeader(new MemoryStream(bytes)));
    }

    [Fact]
    public void Groups_written_by_a_save_target_come_back_after_a_load()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        ControlGroups groups = Groups();
        var target = new GameSaveTarget(t.Catalog, game.SaveTemplate(Rts.Game.Screens.SkirmishSettings.Default), host, t.RulesDir, groups.Snapshot);
        Assert.Null(target.Save("slot"));
        LoadedGame loaded = GameLoader.Load(SaveSlots.PathFor(t.Catalog, SaveKind.Skirmish, "slot"), t.RulesDir);
        var restored = new ControlGroups();
        restored.Restore(loaded.Header.ControlGroups);
        for (int n = 1; n <= ControlGroups.Count; n++) Assert.Equal(groups.Members(n), restored.Members(n));
    }
}
