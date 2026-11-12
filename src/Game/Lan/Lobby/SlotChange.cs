// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Game.Sprites;
using Rts.Net;
using Rts.Sim.Rules;

namespace Rts.Game.Lan.Lobby;

/// <summary>
/// What a client may ask for its own slot (the blob of <c>NetMessage.SlotRequest</c>): faction, colour, team, and optionally to move to
/// another open slot. The host checks it and answers with a new lobby state.
/// </summary>
/// <param name="Faction">Wanted faction.</param>
/// <param name="Colour">Wanted colour index.</param>
/// <param name="Team">Wanted team (0..3).</param>
/// <param name="MoveTo">Index of an open slot to move to, or -1 to stay.</param>
public sealed record SlotChange(Faction Faction, int Colour, int Team, int MoveTo = -1)
{
    /// <summary>Encodes the request.</summary>
    public byte[] Encode()
    {
        var w = new BlobWriter();
        w.U8(LobbyCodec.Version); w.U8((int)Faction); w.U8(Colour); w.U8(Team); w.U8(MoveTo + 1);
        return w.ToArray();
    }

    /// <summary>Decodes a request; <see cref="InvalidDataException"/> on anything wrong.</summary>
    public static SlotChange Decode(byte[] blob)
    {
        var r = new BlobReader(blob);
        if (r.U8() != LobbyCodec.Version) throw new InvalidDataException("Unknown request version.");
        int faction = r.Ranged((int)Faction.Expedition, "faction");
        int colour = r.Ranged(PlayerColours.Count - 1, "colour");
        int team = r.Ranged(SkirmishSettings.MaxSlots - 1, "team");
        int move = r.Ranged(SkirmishSettings.MaxSlots, "slot") - 1;
        r.End();
        return new SlotChange((Faction)faction, colour, team, move);
    }
}
