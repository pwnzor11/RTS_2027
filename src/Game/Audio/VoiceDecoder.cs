// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Buffers.Binary;
using NVorbis;

namespace Rts.Game.Audio;

/// <summary>
/// Decodes OGG Vorbis voice files to 16-bit little-endian PCM, because MonoGame's <c>SoundEffect.FromStream</c> reads WAV only.
/// Decoding uses NVorbis (MIT, already credited in docs/CREDITS.md). WAV paths are left to the caller.
/// </summary>
public static class VoiceDecoder
{
    /// <summary>Samples read per <see cref="VorbisReader.ReadSamples(float[], int, int)"/> call, per channel.</summary>
    private const int FramesPerRead = 4096;

    /// <summary>
    /// Decodes an OGG voice from <paramref name="s"/>. Returns false, with empty outputs, when <paramref name="path"/> is not an
    /// <c>.ogg</c> file (the caller keeps the WAV path) or when the OGG has more than two channels (mono and stereo only).
    /// The stream is left open for the caller to dispose.
    /// </summary>
    /// <param name="s">A readable stream positioned at the start of the file.</param>
    /// <param name="path">The file path or relative name; only its extension is checked.</param>
    /// <param name="pcm16">The decoded samples, 16-bit little-endian, interleaved by channel.</param>
    /// <param name="sampleRate">The sample rate in Hz as stored in the OGG.</param>
    /// <param name="channels">1 for mono or 2 for stereo.</param>
    /// <exception cref="InvalidDataException">The stream is not a readable Ogg Vorbis file.</exception>
    public static bool TryDecode(Stream s, string path, out byte[] pcm16, out int sampleRate, out int channels)
    {
        pcm16 = Array.Empty<byte>();
        sampleRate = 0;
        channels = 0;
        if (!string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase)) return false;

        VorbisReader reader;
        try
        {
            reader = new VorbisReader(s, false);
        }
        catch (ArgumentException e)
        {
            // NVorbis reports a file without a valid Ogg container as ArgumentException; the contract says InvalidDataException.
            throw new InvalidDataException($"not an Ogg Vorbis file: {path}", e);
        }

        using (reader)
        {
            if (reader.Channels is < 1 or > 2) return false;
            DecodeAll(reader, out pcm16);
            sampleRate = reader.SampleRate;
            channels = reader.Channels;
            return true;
        }
    }

    private static void DecodeAll(VorbisReader reader, out byte[] pcm16)
    {
        var samples = new float[reader.Channels * FramesPerRead];
        using var pcm = new MemoryStream();
        int read;
        while ((read = reader.ReadSamples(samples, 0, samples.Length)) > 0)
        {
            var chunk = new byte[read * sizeof(short)];
            for (int i = 0; i < read; i++)
            {
                float clamped = Math.Clamp(samples[i], -1f, 1f);
                BinaryPrimitives.WriteInt16LittleEndian(chunk.AsSpan(i * sizeof(short)), (short)MathF.Round(clamped * short.MaxValue));
            }

            pcm.Write(chunk, 0, chunk.Length);
        }

        pcm16 = pcm.ToArray();
    }
}
