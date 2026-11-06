// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;
using Rts.Sim.Core;

namespace Rts.Scripting;

// M8-05: scripted dialogue, camera hints and locale keys (docs/missions/api.md, plan 3.2).
// These are OUTPUTS for the game layer (plan 3.7, D-071): _dialogue and _cameraHint are not part of the "mission" state part, are never
// saved or hashed, and the sim never reads them. Media.MessageKey is the one exception: it appends to the message list, which is state
// like Media.Message, so its text is part of the hash (see the note on Media.MessageKey).
public sealed partial class MissionHost
{
    /// <summary>
    /// Longest dialogue queue (plan 3.1). Dialogue.Say on a full queue drops the oldest line. Dialogue.Clear empties it.
    /// </summary>
    public const int MaxDialogueQueue = 16;

    /// <summary>Numbers dialogue lines and camera hints (outputs only: not state, not saved, not hashed).</summary>
    private int _outputSeq;

    private void RegisterMediaApi(Script s)
    {
        NewApiTable(s, "Dialogue",
            ("Say", DialogueSay),
            ("Clear", _ => { _dialogue.Clear(); return DynValue.Nil; }));

        NewApiTable(s, "Camera",
            ("Focus", CameraFocus));

        NewApiTable(s, "Media",
            ("MessageKey", MessageKey));
    }

    /// <summary>Dialogue.Say(speaker, portrait, key[, voice]): queues a line. Without a voice argument the locale file's voice for the key is used.</summary>
    private DynValue DialogueSay(CallbackArguments a)
    {
        string speaker = IdArg(a, 0, "speaker");
        string portrait = IdArg(a, 1, "portrait");
        string key = IdArg(a, 2, "locale key");
        string voice = Absent(a, 3) ? Def.Locale.VoiceOf(key) ?? "" : VoiceArg(a, 3);
        _dialogue.Add(new MissionDialogue(++_outputSeq, _sim!.Tick, speaker, portrait, key, Def.Locale.Resolve(key), voice));
        if (_dialogue.Count > MaxDialogueQueue) _dialogue.RemoveAt(0);
        return DynValue.Nil;
    }

    /// <summary>Camera.Focus(zoneOrTag, ticks): the latest call replaces any earlier hint.</summary>
    private DynValue CameraFocus(CallbackArguments a)
    {
        string target = ZoneOrTagArg(a, 0);
        int ticks = Int(a, 1, 1, 100_000);
        _cameraHint = new MissionCameraHint(++_outputSeq, target, CameraCellOf(target), _sim!.Tick + ticks);
        return DynValue.Nil;
    }

    private CellCoord CameraCellOf(string target)
    {
        if (Def.ZoneNamed(target) is { } z) return new CellCoord((z.X0 + z.X1) / 2, (z.Y0 + z.Y1) / 2);
        return TryTag(target, out ActorRef r) && _sim!.TryResolve(r) ? CellOfActor(_sim, r) : new CellCoord(-1, -1);
    }

    /// <summary>
    /// Media.MessageKey(key): like Media.Message, with the text taken from the locale file. The message list is saved and hashed, so the
    /// text (and therefore the locale file) is part of the mission's hash; a changed locale file changes replays of such missions.
    /// </summary>
    private DynValue MessageKey(CallbackArguments a)
    {
        string key = IdArg(a, 0, "locale key");
        _messages.Add(new MissionMessage(++_messageSeq, _sim!.Tick, Def.Locale.Resolve(key)));
        if (_messages.Count > MaxMessages) _messages.RemoveAt(0);
        return DynValue.Nil;
    }

    /// <summary>An optional voice file path: "voice/&lt;speaker&gt;/&lt;key&gt;.ogg" (D-154).</summary>
    private string VoiceArg(CallbackArguments a, int i)
    {
        string v = Str(a, i);
        if (!v.StartsWith("voice/", StringComparison.Ordinal) || !v.EndsWith(".ogg", StringComparison.Ordinal) || v.Contains("..") || v.Contains('\\'))
            throw Err("argument " + (i + 1) + " (voice) must look like \"voice/<speaker>/<key>.ogg\"");
        return v;
    }
}
