// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>A game event that can make a sound. Each has a snake_case name in data/audio/sounds.toml (see <see cref="AudioEventNames"/>).</summary>
public enum AudioEventKind
{
    /// <summary>Small-arms hit (instant weapons).</summary>
    FireBullet,
    /// <summary>A shell was fired.</summary>
    FireShell,
    /// <summary>A missile or rocket was fired.</summary>
    FireMissile,
    /// <summary>A small explosion (unit death, small splash).</summary>
    ExplosionSmall,
    /// <summary>A large explosion (building death, big splash).</summary>
    ExplosionLarge,
    /// <summary>The player selected a unit.</summary>
    UnitSelect,
    /// <summary>The player gave a move or attack order.</summary>
    MoveAck,
    /// <summary>A building was placed.</summary>
    BuildingPlaced,
    /// <summary>A structure finished and waits to be placed.</summary>
    ConstructionComplete,
    /// <summary>A new unit left its factory.</summary>
    UnitReady,
    /// <summary>Power use exceeds output.</summary>
    LowPower,
    /// <summary>A building was sold.</summary>
    Sold,
    /// <summary>A harvester unloaded.</summary>
    HarvesterUnload,
    /// <summary>A unit went underground.</summary>
    TunnelEnter,
    /// <summary>A surfacing telegraph is rumbling.</summary>
    SurfaceRumble,
    /// <summary>A unit stands in radiation.</summary>
    RadiationCrackle,
    /// <summary>Assets sit under a Blackout cloud.</summary>
    BlackoutHum,
    /// <summary>An upgrade finished.</summary>
    UpgradeComplete,
    /// <summary>Production is stalled for lack of money.</summary>
    LowFunds,
    /// <summary>One of the player's actors was hit.</summary>
    UnderAttack,
}

/// <summary>Event names used in data/audio/sounds.toml.</summary>
public static class AudioEventNames
{
    /// <summary>The toml name of <paramref name="kind"/>, e.g. <c>fire_bullet</c>.</summary>
    public static string Of(AudioEventKind kind)
    {
        string s = kind.ToString();
        var sb = new System.Text.StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i])) sb.Append('_');
            sb.Append(char.ToLowerInvariant(s[i]));
        }

        return sb.ToString();
    }
}
