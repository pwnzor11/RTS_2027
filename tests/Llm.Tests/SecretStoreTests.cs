// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;

namespace Rts.Llm.Tests;

public sealed class SecretStoreTests
{
    [Fact]
    public void Memory_store_round_trips_and_clears()
    {
        var store = new MemorySecretStore();
        Assert.Null(store.Load());
        Assert.True(store.Save("sk-1"));
        Assert.Equal("sk-1", store.Load());
        Assert.True(store.Save("sk-2"));
        Assert.Equal("sk-2", store.Load());
        store.Clear();
        Assert.Null(store.Load());
        Assert.False(store.Save(""));
    }

    [Fact]
    public void Memory_store_to_string_hides_the_key()
    {
        var store = new MemorySecretStore();
        store.Save("sk-TOPSECRET");
        Assert.DoesNotContain("TOPSECRET", store.ToString());
    }

    private static string TempFile() => Path.Combine(Path.GetTempPath(), "rts2027-key-" + Guid.NewGuid().ToString("N"), "llm.key");

    private static void Cleanup(string file)
    {
        string? dir = Path.GetDirectoryName(file);
        if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    [Fact]
    public void Dpapi_round_trip_and_file_is_not_plain_text()
    {
        if (!OperatingSystem.IsWindows()) return; // DPAPI is Windows-only; CI is Windows
        string file = TempFile();
        try
        {
            var store = new DpapiSecretStore(file);
            Assert.Null(store.Load());
            Assert.True(store.Save("sk-ant-ROUNDTRIP"));
            Assert.Equal("sk-ant-ROUNDTRIP", new DpapiSecretStore(file).Load());
            byte[] raw = File.ReadAllBytes(file);
            Assert.DoesNotContain("ROUNDTRIP", System.Text.Encoding.UTF8.GetString(raw));
            store.Clear();
            Assert.False(File.Exists(file));
            Assert.Null(store.Load());
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void Dpapi_corrupt_or_empty_file_loads_as_null()
    {
        if (!OperatingSystem.IsWindows()) return;
        string file = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var store = new DpapiSecretStore(file);
            File.WriteAllBytes(file, new byte[] { 1, 2, 3, 4, 5 });
            Assert.Null(store.Load());
            File.WriteAllBytes(file, Array.Empty<byte>());
            Assert.Null(store.Load());
            File.WriteAllText(file, "sk-plain-text-key");
            Assert.Null(store.Load());
            Assert.DoesNotContain("plain", store.ToString());
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void Dpapi_unwritable_path_returns_false_without_throwing()
    {
        if (!OperatingSystem.IsWindows()) return;
        string file = TempFile();
        try
        {
            Directory.CreateDirectory(file); // a folder where the file should be
            Assert.False(new DpapiSecretStore(file).Save("sk-x"));
        }
        finally
        {
            Cleanup(file);
        }
    }
}
