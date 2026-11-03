namespace Rts.Sim.Tests;

/// <summary>
/// Locations of the test fixture copies (rules and maps) that are copied to the test output folder.
/// Tests use these instead of the shipped <c>data/</c> folder, so balance and content work does not change test inputs.
/// </summary>
public static class TestData
{
    /// <summary>Fixture rules directory (a copy of <c>data/rules</c> with test-only values).</summary>
    public static string RulesDir { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "rules");

    /// <summary>Fixture maps directory (<c>test_*.map.toml</c>).</summary>
    public static string MapsDir { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "maps");
}
