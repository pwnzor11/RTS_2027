// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Llm;

/// <summary>An <see cref="ISecretStore"/> that keeps the key in memory only: for tests and non-Windows systems. Never persists, logs or prints the key.</summary>
public sealed class MemorySecretStore : ISecretStore
{
    private string? _key;

    /// <inheritdoc />
    public string? Load() => _key;

    /// <inheritdoc />
    public bool Save(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        _key = key;
        return true;
    }

    /// <inheritdoc />
    public void Clear() => _key = null;

    /// <summary>Never shows the key.</summary>
    public override string ToString() => _key is null ? "MemorySecretStore (no key)" : "MemorySecretStore (key set)";
}
