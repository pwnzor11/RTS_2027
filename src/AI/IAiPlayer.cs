using Rts.Data;
using Rts.Sim.View;

namespace Rts.AI;

/// <summary>An AI player: an external command producer (D-051). It sees only a <see cref="PlayerView"/>, keeps its own
/// memory in its own fields (never sim state) and acts only through <see cref="ICommandSink"/>.</summary>
public interface IAiPlayer
{
    /// <summary>Registry id, e.g. "skirmisher".</summary>
    string Id { get; }

    /// <summary>Called once, before the first Think.</summary>
    void Start(AiContext ctx);

    /// <summary>Called every decision interval (see <see cref="DifficultyProfile.DecisionIntervalTicks"/>).</summary>
    void Think(PlayerView view, ICommandSink sink);
}
