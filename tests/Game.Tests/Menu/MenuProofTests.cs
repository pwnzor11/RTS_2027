// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;
using Rts.Game.Sprites;

namespace Rts.Game.Tests.Menu;

/// <summary>
/// Renders every menu scene, and the main menu on top of the first one, with the CPU renderer and writes the pictures to
/// <c>/home/claude/wt/art_out/menu_scene_&lt;n&gt;.png</c> (or <c>scratch/</c> in the repo when that folder does not exist) so the owner can
/// look at them. It also checks that each picture is not blank and that the scenes differ from each other.
/// </summary>
public sealed class MenuProofTests
{
    private const int W = 1280;
    private const int H = 720;

    private static string OutDir()
    {
        const string shared = "/home/claude/wt/art_out";
        if (Directory.Exists(shared)) return shared;
        string dir = Path.Combine(Path.GetDirectoryName(SceneSetTests.DataDir)!, "scratch");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Each_scene_and_the_main_menu_render_to_png()
    {
        using SpriteLibrary lib = SpriteLibrary.Scan(Path.Combine(SceneSetTests.DataDir, "sprites"));
        SceneSet set = SceneSet.Load(Path.Combine(SceneSetTests.DataDir, "menu", "scenes.toml"), id => lib.Ids.Contains(id));
        var composer = new SceneComposer(id => lib.Ids.Contains(id) ? lib.Manifest(id) : null);
        string dir = OutDir();

        var means = new List<(float, float, float)>();
        for (int i = 0; i < set.Scenes.Count; i++)
        {
            // A moment mid-scene so the drift and the particles are off their starting positions.
            float t = set.Scenes.Take(i).Sum(s => s.Duration) + 9f;
            var ops = new List<DrawOp>();
            MenuBackground.BuildFrame(set, composer, t, W, H, ops);
            var canvas = new SoftwareCanvas(W, H, lib);
            canvas.Draw(ops);
            File.WriteAllBytes(Path.Combine(dir, $"menu_scene_{i + 1}.png"), canvas.ToPng());

            (float r, float g, float b) = canvas.Mean(0, 0, W, H);
            Assert.InRange(r + g + b, 30f, 700f);               // neither black nor white
            means.Add((r, g, b));
            // The vignette darkens the corners relative to the middle.
            (float cr, float cg, float cb) = canvas.Mean(0, 0, 60, 60);
            (float mr, float mg, float mb) = canvas.Mean(W / 2 - 30, H / 2 - 30, 60, 60);
            Assert.True(cr + cg + cb < mr + mg + mb, $"scene {i + 1}: corner is darker than the middle");
        }

        for (int i = 0; i < means.Count; i++)
            for (int j = i + 1; j < means.Count; j++)
                Assert.True(Math.Abs(means[i].Item1 - means[j].Item1) + Math.Abs(means[i].Item2 - means[j].Item2) + Math.Abs(means[i].Item3 - means[j].Item3) > 6f,
                    $"scenes {i + 1} and {j + 1} look different");

        // The main menu over the first scene, with a disabled row selected so the tooltip shows.
        foreach ((int index, bool hasSave, int selected, string file) in new[] { (0, false, 3, "menu_lobby.png"), (1, true, 0, "menu_lobby_2.png") })
        {
            float t = set.Scenes.Take(index).Sum(s => s.Duration) + 9f;
            var ops = new List<DrawOp>();
            MenuBackground.BuildFrame(set, composer, t, W, H, ops);
            IReadOnlyList<MainMenuEntry> entries = MainMenuModel.Build(hasSave);
            LobbyLayout layout = LobbyMenuView.Measure(W, H, 2, entries);
            LobbyMenuView.Build(ops, layout, entries, selected, t, set.Scenes[index].Caption, "0.1.5", W, H);
            var canvas = new SoftwareCanvas(W, H, lib);
            canvas.Draw(ops);
            File.WriteAllBytes(Path.Combine(dir, file), canvas.ToPng());
            // Readable contrast: the text of the selected row sits on the dark scrim, so the strip behind the menu is dark.
            (float r, float g, float b) = canvas.Mean(layout.Rows[0].X, layout.Rows[0].Y, layout.Rows[0].Width, layout.Rows[^1].Bottom - layout.Rows[0].Y);
            Assert.True((r + g + b) / 3f < 70f, $"menu strip is dark enough (was {(r + g + b) / 3f:0})");
        }

        Assert.True(File.Exists(Path.Combine(dir, "menu_scene_1.png")));
    }
}
