// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game;

/// <summary>
/// S47 U-13: the screenshot key. Saves the frame just drawn as a PNG in the user's application-data folder (RTS_2027/screenshots), named by
/// the local time. View only: nothing here touches the simulation.
/// </summary>
public static class Screenshots
{
    /// <summary>The folder the screenshots go to.</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RTS_2027", "screenshots");

    /// <summary>The file name for a screenshot taken at <paramref name="local"/>: <c>rts2027_20261126_142530.png</c>.</summary>
    public static string FileName(DateTime local) => FormattableString.Invariant($"rts2027_{local:yyyyMMdd_HHmmss}.png");

    /// <summary>
    /// A path in <paramref name="folder"/> for a screenshot taken at <paramref name="local"/> that does not exist yet (a second shot in the same
    /// second gets <c>_2</c>, <c>_3</c> ... before the extension).
    /// </summary>
    public static string FreePath(string folder, DateTime local)
    {
        string name = FileName(local);
        string path = Path.Combine(folder, name);
        for (int n = 2; File.Exists(path) && n < 1000; n++)
            path = Path.Combine(folder, Path.GetFileNameWithoutExtension(name) + FormattableString.Invariant($"_{n}") + ".png");
        return path;
    }

    /// <summary>Saves the back buffer of <paramref name="device"/> as a PNG; returns the path, or null when it could not be written (never throws).</summary>
    public static string? Save(GraphicsDevice device, string? folder = null)
    {
        try
        {
            int w = device.PresentationParameters.BackBufferWidth, h = device.PresentationParameters.BackBufferHeight;
            var data = new Color[w * h];
            device.GetBackBufferData(data);
            for (int i = 0; i < data.Length; i++) data[i].A = 255;
            string dir = folder ?? DefaultFolder;
            Directory.CreateDirectory(dir);
            string path = FreePath(dir, DateTime.Now);
            using var texture = new Texture2D(device, w, h);
            texture.SetData(data);
            using FileStream stream = File.Create(path);
            texture.SaveAsPng(stream, w, h);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            AppLog.Line("screenshot failed: " + e.Message);
            return null;
        }
    }
}
