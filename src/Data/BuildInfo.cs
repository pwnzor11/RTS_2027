using System.Reflection;

namespace Rts.Data;

/// <summary>
/// Identity of this build (M1.5, review C-12): the informational version stamped by Directory.Build.props
/// (version + git SHA). Written into replay and save headers and crash logs. Never used by the sim.
/// </summary>
public static class BuildInfo
{
    /// <summary>e.g. "0.1.5+3f2a9c1"; "dev" when not stamped.</summary>
    public static string Version { get; } =
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";
}
