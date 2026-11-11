// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Llm;

/// <summary>What <see cref="ProviderKeyring.MigrateLegacy"/> did with the old single key.</summary>
public enum LegacyKeyOutcome
{
    /// <summary>There was no old key (or it was already handled).</summary>
    NoLegacyKey = 0,

    /// <summary>The saved provider was Anthropic, so the old key became the Anthropic key.</summary>
    MovedToAnthropic = 1,

    /// <summary>The old key could not be tied to a provider and was discarded: the player enters the key again.</summary>
    NeedsReentry = 2,
}

/// <summary>
/// One stored API key per provider (S18 AI-07). A key saved for one provider is never read for another, so switching the provider in the
/// settings can never send a key to the wrong service. Each provider's key lives in its own <see cref="ISecretStore"/>.
/// </summary>
public sealed class ProviderKeyring
{
    private readonly Func<LlmProviderKind, ISecretStore?> _storeFor;
    private readonly ISecretStore? _legacy;

    /// <summary>Creates a keyring. <paramref name="storeFor"/> gives the store of a provider (null = this provider has none);
    /// <paramref name="legacy"/> is the old single-key store, read once by <see cref="MigrateLegacy"/>.</summary>
    public ProviderKeyring(Func<LlmProviderKind, ISecretStore?> storeFor, ISecretStore? legacy = null)
    {
        _storeFor = storeFor;
        _legacy = legacy;
    }

    /// <summary>A keyring that keeps every key in memory (tests, and systems without DPAPI).</summary>
    public static ProviderKeyring InMemory(ISecretStore? legacy = null)
    {
        var anthropic = new MemorySecretStore();
        var openAi = new MemorySecretStore();
        return new ProviderKeyring(k => k switch
        {
            LlmProviderKind.Anthropic => anthropic,
            LlmProviderKind.OpenAiCompatible => openAi,
            _ => null,
        }, legacy);
    }

    /// <summary>The keyring on disk: one DPAPI file per provider beside the old <c>llm.key</c>. Windows only.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static ProviderKeyring Dpapi()
    {
        var anthropic = new DpapiSecretStore(DpapiSecretStore.PathFor("llm.anthropic.key"));
        var openAi = new DpapiSecretStore(DpapiSecretStore.PathFor("llm.openai.key"));
        return new ProviderKeyring(k => k switch
        {
            LlmProviderKind.Anthropic => anthropic,
            LlmProviderKind.OpenAiCompatible => openAi,
            _ => null,
        }, new DpapiSecretStore());
    }

    /// <summary>The key stored for <paramref name="kind"/>, or null.</summary>
    public string? Load(LlmProviderKind kind) => _storeFor(kind)?.Load();

    /// <summary>Stores the key of <paramref name="kind"/>. Returns false for provider None, an empty key or a failed write.</summary>
    public bool Save(LlmProviderKind kind, string key) => _storeFor(kind)?.Save(key) ?? false;

    /// <summary>Deletes the key of <paramref name="kind"/>.</summary>
    public void Clear(LlmProviderKind kind) => _storeFor(kind)?.Clear();

    /// <summary>
    /// Handles the key an older version stored for "the" provider. Which provider it was typed for is unknowable (the settings may have
    /// been switched afterwards), so it is trusted only when the saved provider is Anthropic: it then becomes the Anthropic key. Otherwise
    /// it is deleted and the player enters the key again. The old key is gone either way, so it can never be used for a second provider.
    /// </summary>
    public LegacyKeyOutcome MigrateLegacy(LlmProviderKind savedProvider)
    {
        string? old = _legacy?.Load();
        if (old is null) return LegacyKeyOutcome.NoLegacyKey;
        LegacyKeyOutcome outcome = LegacyKeyOutcome.NeedsReentry;
        if (savedProvider == LlmProviderKind.Anthropic)
        {
            // Keep a key the player already entered for Anthropic; the old one only fills an empty slot.
            if (Load(LlmProviderKind.Anthropic) is not null || Save(LlmProviderKind.Anthropic, old)) outcome = LegacyKeyOutcome.MovedToAnthropic;
            else return LegacyKeyOutcome.NeedsReentry;   // could not write the new file: leave the old one for the next start
        }
        _legacy!.Clear();
        return outcome;
    }
}
