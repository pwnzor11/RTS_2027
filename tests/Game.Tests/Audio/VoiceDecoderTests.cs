// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;

namespace Rts.Game.Tests.Audio;

/// <summary>
/// Tests for <see cref="VoiceDecoder"/>. The OGG fixture is self-made: <c>ffmpeg -f lavfi -i "sine=frequency=440:duration=0.5:sample_rate=22050"
/// -ac 1 -c:a libvorbis -q:a 3 voice_sine_0_5s_mono_22050.ogg</c>. No LFS rule covers *.ogg, so it is committed as a plain binary
/// under Audio/Fixtures, next to the test that reads it.
/// </summary>
public sealed class VoiceDecoderTests
{
    private const int FixtureRate = 22050;
    private const double FixtureSeconds = 0.5;

    private static string FixturePath() =>
        Path.Combine(AudioTestSupport.RepoRoot(), "tests", "Game.Tests", "Audio", "Fixtures", "voice_sine_0_5s_mono_22050.ogg");

    [Fact]
    public void OggFixtureDecodesToMono16BitPcmOfTheExpectedLength()
    {
        using var stream = File.OpenRead(FixturePath());

        bool ok = VoiceDecoder.TryDecode(stream, "voice/test/line.ogg", out byte[] pcm, out int rate, out int channels);

        Assert.True(ok);
        Assert.Equal(FixtureRate, rate);
        Assert.Equal(1, channels);
        double expectedBytes = FixtureSeconds * rate * 2;
        Assert.InRange(pcm.Length, expectedBytes * 0.95, expectedBytes * 1.05);
        Assert.Equal(0, pcm.Length % 2);
    }

    [Fact]
    public void DecodedFixtureIsAudibleSineNotSilence()
    {
        using var stream = File.OpenRead(FixturePath());
        Assert.True(VoiceDecoder.TryDecode(stream, "line.ogg", out byte[] pcm, out _, out _));

        int peak = 0;
        for (int i = 0; i + 1 < pcm.Length; i += 2)
        {
            short sample = BitConverter.ToInt16(pcm, i);
            peak = Math.Max(peak, Math.Abs((int)sample));
        }

        Assert.InRange(peak, 1000, short.MaxValue);
    }

    [Theory]
    [InlineData("voice/test/line.wav")]
    [InlineData("voice/test/line.WAV")]
    [InlineData("voice/test/line.mp3")]
    public void NonOggPathsReturnFalseAndLeaveTheCallerOnTheWavPath(string path)
    {
        using var stream = new MemoryStream(new byte[] { 0x52, 0x49, 0x46, 0x46 });

        bool ok = VoiceDecoder.TryDecode(stream, path, out byte[] pcm, out int rate, out int channels);

        Assert.False(ok);
        Assert.Empty(pcm);
        Assert.Equal(0, rate);
        Assert.Equal(0, channels);
    }

    [Fact]
    public void CorruptOggThrowsInvalidDataSoPlayVoiceCanReportIt()
    {
        using var stream = new MemoryStream(new byte[] { 0x4F, 0x67, 0x67, 0x53, 0x00, 0x00, 0x00, 0x00 });

        Assert.Throws<InvalidDataException>(() => VoiceDecoder.TryDecode(stream, "line.ogg", out _, out _, out _));
    }
}
