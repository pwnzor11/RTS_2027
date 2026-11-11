// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Rts.Llm;

/// <summary>
/// An <see cref="ISecretStore"/> that keeps the API key in a file encrypted for the current Windows user with DPAPI
/// (<see cref="ProtectedData"/>, CurrentUser scope, plus an app-specific entropy). Default file:
/// <c>%APPDATA%/RTS_2027/llm.key</c>. Load of a missing, corrupt or foreign-user file gives null and never throws. No
/// exception, message or <see cref="ToString"/> ever carries the key.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RTS_2027.llm.key.v1");
    private readonly string _path;

    /// <summary>Creates the store. <paramref name="path"/> overrides the key file (tests); null = the default file.</summary>
    public DpapiSecretStore(string? path = null) => _path = path ?? DefaultPath;

    /// <summary>The default key file path.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RTS_2027", "llm.key");

    /// <summary>The path of another key file beside the default one (<c>%APPDATA%/RTS_2027/&lt;name&gt;</c>).</summary>
    public static string PathFor(string fileName) => Path.Combine(Path.GetDirectoryName(DefaultPath)!, fileName);

    /// <inheritdoc />
    public string? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser);
            try
            {
                string key = Encoding.UTF8.GetString(plain);
                return key.Length == 0 ? null : key;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
        catch (Exception)
        {
            return null; // corrupt, wrong user, unreadable: treated as no key
        }
    }

    /// <inheritdoc />
    public bool Save(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        byte[] plain = Encoding.UTF8.GetBytes(key);
        try
        {
            byte[] sealedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(_path, sealedBytes);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch (Exception)
        {
            // Nothing useful to do; the next Load still decides.
        }
    }

    /// <summary>Never shows the key.</summary>
    public override string ToString() => "DpapiSecretStore";
}
