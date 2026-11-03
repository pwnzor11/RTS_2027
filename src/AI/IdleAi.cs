using Rts.Sim.View;

namespace Rts.AI;

/// <summary>The "idle" AI: never acts. A sparring dummy and the baseline for tests.</summary>
public sealed class IdleAi : IAiPlayer
{
    /// <inheritdoc />
    public string Id => AiRegistry.IdleId;

    /// <inheritdoc />
    public void Start(AiContext ctx) { }

    /// <inheritdoc />
    public void Think(PlayerView view, ICommandSink sink) { }
}
