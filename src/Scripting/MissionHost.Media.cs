// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;

namespace Rts.Scripting;

// M8-05 owns this file: scripted dialogue, camera hints, locale keys (docs/missions/api.md).
// M8-02 contract: these functions validate their arguments and then DO NOTHING (presentation only; the sim is unaffected).
// M8-05 fills them: append to _dialogue / set _cameraHint (outputs, not state, not hashed).
public sealed partial class MissionHost
{
    private void RegisterMediaApi(Script s)
    {
        NewApiTable(s, "Dialogue",
            ("Say", a =>
            {
                IdArg(a, 0, "speaker"); IdArg(a, 1, "portrait"); IdArg(a, 2, "locale key");
                if (!Absent(a, 3)) VoiceArg(a, 3);
                return DynValue.Nil;
            }),
            ("Clear", _ => DynValue.Nil));

        NewApiTable(s, "Camera",
            ("Focus", a => { ZoneOrTagArg(a, 0); Int(a, 1, 1, 100_000); return DynValue.Nil; }));

        NewApiTable(s, "Media",
            ("MessageKey", a => { IdArg(a, 0, "locale key"); return DynValue.Nil; }));
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
