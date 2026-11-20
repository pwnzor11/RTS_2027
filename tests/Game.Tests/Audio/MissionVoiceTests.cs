// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Reflection;
using Rts.Game.Audio;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;

namespace Rts.Game.Tests.Audio;

/// <summary>M10-27: a campaign radio line with a voice path reaches the audio backend exactly once, and every shipped voice file is audible.</summary>
public sealed class MissionVoiceTests
{
    private sealed class VoiceRecorder : IAudio
    {
        public List<string> HasVoiceCalls { get; } = new();
        public List<string> Played { get; } = new();
        public int MusicVolume { get; set; }
        public int SfxVolume { get; set; }
        public void PlaySfx(string id) { }
        public void PlayMusic(string id) { }
        public void StopMusic() { }
        public bool HasVoice(string relativePath) { HasVoiceCalls.Add(relativePath); return true; }
        public void PlayVoice(string relativePath) => Played.Add(relativePath);
    }

    // MissionOverlay is internal to the Game assembly; drive its Advance (the call GameScreen makes every frame) by reflection.
    private static (Action<int, IAudio> Advance, Func<Rts.Scripting.MissionDialogue?> Current) Overlay(MissionGame game)
    {
        Type type = typeof(MissionDialoguePresenter).Assembly.GetType("Rts.Game.Screens.MissionOverlay")!;
        object overlay = Activator.CreateInstance(type, game.Host, "", null)!;
        MethodInfo advance = type.GetMethod("Advance")!;
        PropertyInfo current = type.GetProperty("CurrentDialogue")!;
        return ((tick, audio) => advance.Invoke(overlay, new object[] { tick, audio }), () => (Rts.Scripting.MissionDialogue?)current.GetValue(overlay));
    }

    [Theory]
    [InlineData("auth_01", "voice/leader_auth_wrenfield/a1.radio.start.ogg")]
    [InlineData("auth_02", "voice/leader_auth_pell/a2.radio.pell.ogg")]
    public void The_first_radio_line_plays_its_voice_exactly_once(string mission, string expectedVoice)
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, mission);
        var overlay = Overlay(game);
        var audio = new VoiceRecorder();

        for (int i = 0; i < 40; i++)   // well inside the 120-tick life of the line
        {
            game.Sim.Step(Array.Empty<Rts.Sim.Core.Command>());
            overlay.Advance(game.Sim.Tick, audio);
        }

        Assert.NotNull(overlay.Current());
        Assert.Equal(expectedVoice, overlay.Current()!.Voice);
        Assert.Equal(new[] { expectedVoice }, audio.Played);
    }

    [Fact]
    public void A_line_without_a_voice_file_calls_no_PlayVoice()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_01");
        var overlay = Overlay(game);
        var audio = new NoVoiceRecorder();
        for (int i = 0; i < 5; i++)
        {
            game.Sim.Step(Array.Empty<Rts.Sim.Core.Command>());
            overlay.Advance(game.Sim.Tick, audio);
        }

        Assert.Empty(audio.Played);
    }

    private sealed class NoVoiceRecorder : IAudio
    {
        public List<string> Played { get; } = new();
        public int MusicVolume { get; set; }
        public int SfxVolume { get; set; }
        public void PlaySfx(string id) { }
        public void PlayMusic(string id) { }
        public void StopMusic() { }
        public bool HasVoice(string relativePath) => false;
        public void PlayVoice(string relativePath) => Played.Add(relativePath);
    }

    [Fact]
    public void Every_shipped_voice_file_decodes_to_audible_mono_or_stereo_pcm()
    {
        string voiceDir = Path.Combine(AudioTestSupport.AudioDir(), "voice");
        string[] files = Directory.GetFiles(voiceDir, "*.ogg", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        var bad = new List<string>();
        foreach (string file in files)
        {
            using var stream = File.OpenRead(file);
            if (!VoiceDecoder.TryDecode(stream, file, out byte[] pcm, out int rate, out int channels) || rate is < 8000 or > 48000 || channels is < 1 or > 2 || pcm.Length < rate)
            {
                bad.Add(Path.GetRelativePath(voiceDir, file) + " (undecodable or under a second)");
                continue;
            }

            double sum = 0;
            int n = pcm.Length / 2;
            for (int i = 0; i < n; i++)
            {
                double v = BitConverter.ToInt16(pcm, i * 2) / 32768.0;
                sum += v * v;
            }

            double rms = Math.Sqrt(sum / n);
            if (rms < 0.01) bad.Add($"{Path.GetRelativePath(voiceDir, file)} (RMS {rms:0.0000})");
        }

        Assert.Empty(bad);
    }
}
