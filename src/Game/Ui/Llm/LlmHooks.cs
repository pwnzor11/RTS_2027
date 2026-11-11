// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Runtime.Versioning;
using Rts.Llm;

namespace Rts.Game.Ui.Llm;

/// <summary>Test-connection delegate: config, the stored key (or null), cancellation; returns a short result line that never holds the key.</summary>
public delegate Task<string> TestConnectionFunc(LlmConfig config, string? key, CancellationToken ct);

/// <summary>Process-wide wiring points of the LLM settings screen. A later package replaces <see cref="TestConnection"/> with the
/// real provider call; tests replace <see cref="SecretStore"/>.</summary>
public static class LlmHooks
{
    private static ProviderKeyring? _keys;
    private static ProviderDefaults? _defaults;

    /// <summary>
    /// The API keys, one per provider (S18 AI-07): DPAPI files on Windows, in memory elsewhere. Settable for tests. The first use moves
    /// the key an older version stored for "the" provider (see <see cref="ProviderKeyring.MigrateLegacy"/>).
    /// </summary>
    public static ProviderKeyring Keys
    {
        get => _keys ??= CreateDefaultKeys();
        set => _keys = value;
    }

    /// <summary>What the first-use migration of the old single key did (<see cref="LegacyKeyOutcome.NoLegacyKey"/> until it runs).</summary>
    public static LegacyKeyOutcome LegacyOutcome { get; private set; }

    /// <summary>Provider defaults from data/llm/providers.toml beside the exe (built-in values if missing). Settable for tests.</summary>
    public static ProviderDefaults Defaults
    {
        get => _defaults ??= ProviderDefaults.Load(Path.Combine(AppContext.BaseDirectory, "data", "llm", "providers.toml"));
        set => _defaults = value;
    }

    /// <summary>Runs "Test connection": one small real call through the provider (M5-05). Settable for tests.</summary>
    public static TestConnectionFunc TestConnection { get; set; } = (config, key, ct) => LlmConnectionTest.RunAsync(config, key, ct);

    private static ProviderKeyring CreateDefaultKeys()
    {
        if (!OperatingSystem.IsWindows()) return ProviderKeyring.InMemory();
        ProviderKeyring keys = CreateDpapi();
        LegacyOutcome = keys.MigrateLegacy(Settings.Load().ProviderKind);
        return keys;
    }

    [SupportedOSPlatform("windows")]
    private static ProviderKeyring CreateDpapi() => ProviderKeyring.Dpapi();
}
