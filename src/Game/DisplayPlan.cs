// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game;

/// <summary>
/// The window mode and back buffer size to ask the graphics device for. Full screen is borderless (no hardware mode switch) at the
/// desktop size; the <c>fullscreen</c> setting key is unchanged. Pure, so it is unit-tested (S18 GAMEREND-07).
/// </summary>
/// <param name="Fullscreen">Whether to be full screen.</param>
/// <param name="Width">Back buffer width.</param>
/// <param name="Height">Back buffer height.</param>
public readonly record struct DisplayPlan(bool Fullscreen, int Width, int Height)
{
    /// <summary>Always false: a hardware mode switch changes the monitor mode instead of giving a borderless window.</summary>
    public const bool HardwareModeSwitch = false;

    /// <summary>The plan for <paramref name="settings"/> on a desktop of the given size (0 = unknown, the saved window size is used).</summary>
    public static DisplayPlan For(Settings settings, int desktopWidth, int desktopHeight) =>
        settings.Fullscreen && desktopWidth > 0 && desktopHeight > 0
            ? new DisplayPlan(true, desktopWidth, desktopHeight)
            : new DisplayPlan(settings.Fullscreen, settings.WindowWidth, settings.WindowHeight);
}
