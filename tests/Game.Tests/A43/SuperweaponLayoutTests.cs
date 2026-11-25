// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Superweapon;

namespace Rts.Game.Tests.A43;

/// <summary>A43 GAMEUI-02: the superweapon button is wide enough for its longest "{TITLE}  {status}" label at every UI scale.</summary>
public sealed class SuperweaponLayoutTests
{
    // A stand-in for BitmapFont.Measure at one scale: a fixed advance per character, so the arithmetic is exact.
    private static Func<string, int> Advance(int perChar) => s => s.Length * perChar;

    [Theory]
    [InlineData("THE SUNRISE DEVICE")]
    [InlineData("THE SECOND DRIFT")]
    [InlineData("SALVO ARRAY")]
    [InlineData("SUNRISE")]
    public void Button_HoldsEveryStatusOfTheCaption(string caption)
    {
        foreach (int perChar in new[] { 7, 14, 21, 28 })   // UI scale 100 % to 200 % in steps
        {
            Func<string, int> measure = Advance(perChar);
            int width = SuperweaponButtons.ButtonWidth(caption, measure);
            foreach (string status in SuperweaponButtons.StatusWords)
                Assert.True(measure($"{caption}  {status}") + SuperweaponButtons.LabelPadding <= width, $"{caption} {status} at {perChar}px/char");
        }
    }

    [Fact]
    public void Width_IsTheWidestLabelPlusPadding()
    {
        // "THE SUNRISE DEVICE  PAUSED" is the widest label (26 characters), so the button is 26 * 10 + 16 px wide.
        Assert.Equal(26 * 10 + SuperweaponButtons.LabelPadding, SuperweaponButtons.ButtonWidth("THE SUNRISE DEVICE", Advance(10)));
        Assert.Equal(SuperweaponButtons.LabelPadding + "SUNRISE  PAUSED".Length * 10, SuperweaponButtons.ButtonWidth("SUNRISE", Advance(10)));
    }

    [Fact]
    public void Width_IsTheSameForEveryStatus()
    {
        // The width depends on the caption alone, so the button does not change size as the charge moves from READY to PAUSED.
        int width = SuperweaponButtons.ButtonWidth("THE SECOND DRIFT", Advance(9));
        Assert.Equal(Advance(9)("THE SECOND DRIFT  PAUSED") + SuperweaponButtons.LabelPadding, width);
    }

    [Fact]
    public void LongerCaptions_GiveWiderButtons()
    {
        int sunrise = SuperweaponButtons.ButtonWidth("SUNRISE", Advance(10));
        int salvo = SuperweaponButtons.ButtonWidth("SALVO ARRAY", Advance(10));
        int drift = SuperweaponButtons.ButtonWidth("THE SECOND DRIFT", Advance(10));
        Assert.True(sunrise < salvo && salvo < drift);
    }
}
