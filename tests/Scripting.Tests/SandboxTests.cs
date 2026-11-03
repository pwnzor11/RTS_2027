// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests;

/// <summary>The determinism sandbox (review A-7): integers only, no os/io/math.random/load, ordered pairs.</summary>
public sealed class SandboxTests
{
    private static MissionHost Run(string body, int ticks = 2)
    {
        var (sim, host) = TestData.Tiny("function OnInit()\n" + body + "\nend");
        TestData.Run(sim, ticks);
        return host;
    }

    [Theory]
    [InlineData("Actor.Create('auth_rifleman', 0, {x = 10.5, y = 10})")]
    [InlineData("Trigger.AfterDelay(1.5, OnInit)")]
    [InlineData("Random.Next(3.2)")]
    [InlineData("Player.AddCash(0, 1/3)")]
    public void Fractional_numbers_are_rejected_at_the_api(string call)
    {
        MissionHost host = Run(call);
        Assert.Single(host.Errors);
        Assert.Contains("integer", host.Errors[0]);
    }

    [Fact]
    public void Integral_floats_are_accepted()
    {
        MissionHost host = Run("Actor.Create('auth_rifleman', 0, {x = 20 / 2, y = 10.0})");
        Assert.Empty(host.Errors);
    }

    [Theory]
    [InlineData("os")]
    [InlineData("io")]
    [InlineData("debug")]
    [InlineData("load")]
    [InlineData("loadstring")]
    [InlineData("dofile")]
    [InlineData("require")]
    [InlineData("coroutine")]
    [InlineData("collectgarbage")]
    public void Dangerous_globals_are_gone(string name)
    {
        MissionHost host = Run("state.t = type(" + name + ")");
        Assert.Empty(host.Errors);
        Assert.Equal("nil", StateString(host, "t"));
    }

    [Fact]
    public void Math_random_and_float_math_are_gone_but_floor_works()
    {
        MissionHost host = Run("state.r = type(math.random); state.s = type(math.sqrt); state.f = math.floor(7 / 2)");
        Assert.Equal("nil", StateString(host, "r"));
        Assert.Equal("nil", StateString(host, "s"));
        Assert.Equal("3", StateString(host, "f"));
    }

    [Fact]
    public void Random_next_is_deterministic_and_in_range()
    {
        string script = "state.v = {}; for i = 1, 20 do state.v[i] = Random.Next(10) end";
        string a = StateString(Run(script), "v"), b = StateString(Run(script), "v");
        Assert.Equal(a, b);
        Assert.All(a.Split(','), x => Assert.InRange(int.Parse(x), 0, 9));
        Assert.True(a.Split(',').Distinct().Count() > 1);
    }

    [Fact]
    public void Pairs_is_ordered_and_rejects_foreign_keys()
    {
        MissionHost ok = Run("state.t = {zeta = 1, alpha = 2, [3] = 3, [1] = 4}; local s = ''; for k, v in pairs(state.t) do s = s .. k .. ',' end; state.order = s");
        Assert.Empty(ok.Errors);
        Assert.Equal("1,3,alpha,zeta,", StateString(ok, "order"));

        MissionHost bad = Run("local t = {}; t[true] = 1; for k in pairs(t) do end");
        Assert.Single(bad.Errors);
        Assert.Contains("pairs", bad.Errors[0]);
    }

    [Fact]
    public void Non_integer_state_halts_the_script_instead_of_corrupting_the_save()
    {
        MissionHost host = Run("state.x = 0.5");
        Assert.True(host.Halted);
        Assert.Contains("not an integer", host.Errors[0]);
    }

    [Fact]
    public void Api_is_locked_while_the_file_loads()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TestData.Tiny("Media.Message('hi')"));
        Assert.Contains("cannot be used while the script loads", ex.Message);
    }

    [Fact]
    public void Callbacks_must_be_named_globals()
    {
        MissionHost host = Run("Trigger.AfterDelay(5, function() end)");
        Assert.Single(host.Errors);
        Assert.Contains("top-level named functions", host.Errors[0]);
    }

    internal static string StateString(MissionHost host, string key)
    {
        // Read back through a save-format round trip of the part: simplest is to ask the script via the part's own writer.
        return TestAccess.StateValue(host, key);
    }
}
