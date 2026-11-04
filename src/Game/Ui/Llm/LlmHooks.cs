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
    private static ISecretStore? _store;
    private static ProviderDefaults? _defaults;

    /// <summary>The key store: DPAPI on Windows, in-memory elsewhere. Settable for tests.</summary>
    public static ISecretStore SecretStore
    {
        get => _store ??= CreateDefaultStore();
        set => _store = value;
    }

    /// <summary>Provider defaults from data/llm/providers.toml beside the exe (built-in values if missing). Settable for tests.</summary>
    public static ProviderDefaults Defaults
    {
        get => _defaults ??= ProviderDefaults.Load(Path.Combine(AppContext.BaseDirectory, "data", "llm", "providers.toml"));
        set => _defaults = value;
    }

    /// <summary>Runs "Test connection". Default: returns "not wired yet" and makes no network call.</summary>
    public static TestConnectionFunc TestConnection { get; set; } = (_, _, _) => Task.FromResult(LlmUiText.NotWired);

    private static ISecretStore CreateDefaultStore() => OperatingSystem.IsWindows() ? CreateDpapi() : new MemorySecretStore();

    [SupportedOSPlatform("windows")]
    private static ISecretStore CreateDpapi() => new DpapiSecretStore();
}
