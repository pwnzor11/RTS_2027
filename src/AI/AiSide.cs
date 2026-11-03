using Rts.Data;
namespace Rts.AI;

/// <summary>One side of an AI-vs-AI game: an AI id and the difficulty profile it plays with.</summary>
public sealed record AiSide(string AiId, DifficultyProfile Profile)
{
    /// <summary>Label such as "skirmisher:veteran".</summary>
    public string Label => AiId + ":" + Profile.Id;
}
