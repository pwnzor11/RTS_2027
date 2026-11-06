// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Scripting;
using Rts.Sim.Core;

namespace Rts.Game.Sandbox;

/// <summary>
/// M8-06: adapts the mission host's hashed <see cref="AiHookState"/> to the AI's <see cref="IMissionAiHooks"/> (src/AI cannot see src/Scripting).
/// Read-only: it resolves zone names to centre cells and tiers to profiles, and never writes the host (D-051).
/// </summary>
public sealed class MissionAiHooks : IMissionAiHooks
{
    private readonly MissionHost _host;
    private readonly IReadOnlyList<DifficultyProfile> _tiers;

    /// <summary>Creates the adapter over <paramref name="host"/> with the tier profiles (index 0 = tier 1; empty = SetTier is ignored).</summary>
    public MissionAiHooks(MissionHost host, IReadOnlyList<DifficultyProfile> tiers)
    {
        _host = host;
        _tiers = tiers;
    }

    /// <summary>Loads data/ai/difficulty.toml beside <paramref name="rulesDir"/>; an unreadable file gives no tiers.</summary>
    public static IReadOnlyList<DifficultyProfile> LoadTiers(string rulesDir)
    {
        try { return DifficultyLoader.Load(DifficultyLoader.PathBesideRules(rulesDir)); }
        catch (RulesException) { return Array.Empty<DifficultyProfile>(); }
    }

    /// <inheritdoc />
    public AiHooks Read(byte player)
    {
        AiHookState s = _host.AiHookOf(player);
        if (s == AiHookState.None) return AiHooks.None;
        return new AiHooks(Centre(s.AttackZone), Centre(s.DefendZone), s.Tier, s.WaveSeq, s.WaveId, Centre(s.WaveZone));
    }

    /// <inheritdoc />
    public DifficultyProfile? ProfileForTier(int tier) => tier >= 1 && tier <= _tiers.Count ? _tiers[tier - 1] : null;

    private CellCoord? Centre(string zone) =>
        zone.Length > 0 && _host.Def.ZoneNamed(zone) is { } z ? new CellCoord((z.X0 + z.X1) / 2, (z.Y0 + z.Y1) / 2) : null;
}
