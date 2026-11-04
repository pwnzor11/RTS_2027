// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Game.Ui.Superweapon;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M3_21;

/// <summary>M3-21: sprite-state resolution for the open gate, the turret and dive layers fed from the snapshot, the dive marker and the
/// superweapon button (ready pulse, blast radius, "ready" message key). No GPU.</summary>
public sealed class ViewRenderStateTests
{
    private static string State(string name, int facings, int frames, int first, int ticks = 1) => string.Join("\n",
        "[[state]]", $"name = \"{name}\"", $"facings = {facings}", $"frames = {frames}", $"first = {first}", $"ticks_per_frame = {ticks}");

    private static string Text(params string[] states) => string.Join("\n",
        "file = \"x.png\"", "frame_w = 64", "frame_h = 64", "origin_x = 0", "origin_y = 0", string.Join("\n", states));

    private static readonly SpriteManifest ClosedOnly =
        SpriteManifest.Parse(Text(State("idle", 1, 1, 0), State("damaged", 1, 1, 1)), "gate", "test");

    private static readonly SpriteManifest WithOpen =
        SpriteManifest.Parse(Text(State("idle", 1, 1, 0), State("damaged", 1, 1, 1), State("open", 1, 2, 2, 4)), "gate", "test");

    [Fact]
    public void An_open_gate_without_open_art_keeps_the_closed_frame()
    {
        Assert.False(SpriteStates.HasState(ClosedOnly, SpriteStates.Open));
        Assert.Equal(0, SpriteStates.BuildingFrame(ClosedOnly, 1000, 100, 100, false, 7, open: true));
        Assert.Equal(1, SpriteStates.BuildingFrame(ClosedOnly, 1000, 10, 100, false, 7, open: true));   // damaged art still applies
    }

    [Fact]
    public void An_open_gate_with_open_art_plays_the_open_state_and_a_closed_gate_does_not()
    {
        Assert.Equal(2, SpriteStates.BuildingFrame(WithOpen, 1000, 100, 100, false, 0, open: true));
        Assert.Equal(3, SpriteStates.BuildingFrame(WithOpen, 1000, 100, 100, false, 4, open: true));
        Assert.Equal(0, SpriteStates.BuildingFrame(WithOpen, 1000, 100, 100, false, 4, open: false));
        Assert.Equal(0, SpriteStates.BuildingFrame(WithOpen, 1000, 100, 100, false, 4));   // the old call shape is unchanged
    }

    [Fact]
    public void A_ghost_never_shows_the_open_state()
    {
        Assert.Equal(0, SpriteStates.BuildingFrame(WithOpen, 1000, 100, 100, true, 0, open: true));
    }

    [Fact]
    public void The_resolver_reports_whether_a_sprite_has_the_open_slot_filled()
    {
        var resolver = new SpriteResolver(null);
        ResolvedSprite placeholder = resolver.Resolve(SpriteKind.Building, "auth_gate", 1, 1);
        Assert.False(SpriteResolver.HasOpenArt(placeholder));

        string root = Path.Combine(Path.GetTempPath(), "rts_open_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "buildings"));
            File.WriteAllText(Path.Combine(root, "buildings", "auth_gate.sprite.toml"), Text(State("idle", 1, 1, 0), State("open", 1, 1, 1)));
            using SpriteLibrary library = SpriteLibrary.Scan(root);
            ResolvedSprite art = new SpriteResolver(library).Resolve(SpriteKind.Building, "auth_gate", 1, 1);
            Assert.Equal(SpriteSource.Library, art.Source);
            Assert.True(SpriteResolver.HasOpenArt(art));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Turret_facing_from_the_snapshot_drives_the_turret_layer_and_a_unit_without_one_uses_the_body_facing()
    {
        SpriteManifest m = SpriteManifest.Parse(Text(State("idle", 16, 1, 0), State("turret", 16, 1, 16)), "t", "test");
        var tank = new UnitView(new EntityId(1, 1), UnitKind.Vehicle, 0, 0, new LeptonCoord(0, 0), 0, 100) { TurretFacing = 64 };
        var plain = new UnitView(new EntityId(2, 1), UnitKind.Vehicle, 0, 0, new LeptonCoord(0, 0), 64, 100);

        UnitLayer turret = UnitLayerPlan.For(m, false, false, tank.Facing, UnitVisuals.TurretFacing(tank), tank.Diving, 0)
            .Single(l => l.Kind == UnitLayerKind.Turret);
        Assert.Equal(16 + 4, turret.Frame);   // 64/256 of a turn with the hull still facing north

        UnitLayer follows = UnitLayerPlan.For(m, false, false, plain.Facing, UnitVisuals.TurretFacing(plain), plain.Diving, 0)
            .Single(l => l.Kind == UnitLayerKind.Turret);
        Assert.Equal(16 + 4, follows.Frame);   // no turret facing: the layer follows the body
        Assert.Null(UnitVisuals.TurretFacing(plain));
        Assert.Null(UnitVisuals.TurretFacing(tank with { Kind = UnitKind.Infantry }));
    }

    [Fact]
    public void The_dive_flag_picks_the_dive_layer_and_the_dive_marker_shows_only_on_the_surface()
    {
        SpriteManifest m = SpriteManifest.Parse(Text(State("idle", 16, 1, 0), State("dive", 16, 1, 32)), "t", "test");
        var diving = new UnitView(new EntityId(1, 1), UnitKind.Vehicle, 0, 0, new LeptonCoord(0, 0), 0, 100) { Diving = true };
        Assert.Contains(UnitLayerPlan.For(m, false, false, 0, null, diving.Diving, 0), l => l.Kind == UnitLayerKind.Dive);
        Assert.DoesNotContain(UnitLayerPlan.For(m, false, false, 0, null, false, 0), l => l.Kind == UnitLayerKind.Dive);
        Assert.True(UnitVisuals.ShowDiveMarker(diving));
        Assert.False(UnitVisuals.ShowDiveMarker(diving with { Underground = true, Diving = false }));
        Assert.False(UnitVisuals.ShowDiveMarker(diving with { Diving = false }));
    }

    // ---- superweapon -----------------------------------------------------------------------------------------------------

    private static readonly EntityId Sun = new(3, 1);

    private static PlayerHud Hud(int charge) =>
        new(0, 0, 0, 0, 0, Array.Empty<QueueView>(), Array.Empty<(ProductionCategory, int)>(), false)
        {
            Superweapons = new[] { new SuperweaponView(Sun, 7, "dir_sunrise", charge, 200, charge >= 200, false, 8) { BlastRadius = 5 } },
        };

    [Fact]
    public void The_button_carries_the_blast_radius_and_the_ready_pulse_stays_in_range_and_loops()
    {
        Assert.Equal(5, SuperweaponButtons.From(Hud(10))[0].BlastRadius);
        for (int t = 0; t < 3 * SuperweaponButtons.PulseTicks; t++)
        {
            byte a = SuperweaponButtons.ReadyPulse(t);
            Assert.InRange(a, 40, 160);
            Assert.Equal(a, SuperweaponButtons.ReadyPulse(t + SuperweaponButtons.PulseTicks));
        }

        Assert.NotEqual(SuperweaponButtons.ReadyPulse(0), SuperweaponButtons.ReadyPulse(SuperweaponButtons.PulseTicks / 2));
    }

    [Fact]
    public void Arming_a_ready_button_exposes_its_blast_radius_for_the_telegraph()
    {
        var c = new SuperweaponController();
        c.Update(Hud(100), 1);
        c.ClickButton(0);
        Assert.Equal(0, c.ArmedBlastRadius);   // not ready: nothing armed
        c.Update(Hud(200), 2);
        c.ClickButton(0);
        Assert.True(c.IsArmed);
        Assert.Equal(5, c.ArmedBlastRadius);
        c.Cancel();
        Assert.Equal(0, c.ArmedBlastRadius);
    }

    [Fact]
    public void The_ready_message_appears_when_a_weapon_finishes_charging_and_fades_after_its_time()
    {
        var c = new SuperweaponController();
        c.Update(Hud(100), 10);
        Assert.Null(c.MessageKey(10));
        c.Update(Hud(200), 11);   // turned ready while watching
        Assert.Equal(SuperweaponText.ReadyKey, c.MessageKey(11));
        Assert.Contains("SUNRISE", c.MessageText(11));
        Assert.Equal(SuperweaponText.ReadyKey, c.MessageKey(11 + SuperweaponController.MessageTicks - 1));
        Assert.Null(c.MessageKey(11 + SuperweaponController.MessageTicks));
        c.Update(Hud(200), 500);   // still ready: no second announcement
        Assert.Null(c.MessageKey(500));
    }

    [Fact]
    public void A_game_loaded_with_a_charged_weapon_does_not_announce_it_again()
    {
        var c = new SuperweaponController();
        c.Update(Hud(200), 900);
        Assert.Null(c.MessageKey(900));
    }

    [Fact]
    public void The_blast_circle_runs_through_the_edge_of_the_outermost_blast_cell()
    {
        Assert.Equal(5.5f, TelegraphRenderer.BlastCircleCells(5));
        Assert.Equal(0.5f, TelegraphRenderer.BlastCircleCells(0));
    }
}
