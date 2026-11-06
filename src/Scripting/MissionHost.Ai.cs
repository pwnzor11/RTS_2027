// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;

namespace Rts.Scripting;

// M8-06 owns this file: AI script hooks (D-051). These run inside the tick, so they ONLY set flags in the hashed mission state
// (_aiHooks, saved in the version-2 tail); the built-in AI reads them on its next decision, outside Step, via MissionHost.AiHookOf.
// M8-02 contract: the flag setters below are complete; M8-06 adds the AI-side reader and any further hook kinds.
public sealed partial class MissionHost
{
    private void RegisterAiApi(Script s)
    {
        NewApiTable(s, "AI",
            ("Attack", a => { int p = AiPlayerArg(a, 0); string z = ZoneName(a, 1); _aiHooks[p] = _aiHooks[p] with { AttackZone = z }; return DynValue.Nil; }),
            ("Defend", a => { int p = AiPlayerArg(a, 0); string z = ZoneName(a, 1); _aiHooks[p] = _aiHooks[p] with { DefendZone = z }; return DynValue.Nil; }),
            ("SetTier", a => { int p = AiPlayerArg(a, 0); int t = Int(a, 1, 1, 5); _aiHooks[p] = _aiHooks[p] with { Tier = t }; return DynValue.Nil; }),
            ("Wave", a =>
            {
                int p = AiPlayerArg(a, 0);
                string id = IdArg(a, 1, "wave id");
                string z = ZoneName(a, 2);
                _aiHooks[p] = _aiHooks[p] with { WaveSeq = _aiHooks[p].WaveSeq + 1, WaveId = id, WaveZone = z };
                return DynValue.Nil;
            }),
            ("Clear", a => { int p = AiPlayerArg(a, 0); _aiHooks[p] = AiHookState.None with { WaveSeq = _aiHooks[p].WaveSeq }; return DynValue.Nil; }));
    }
}
