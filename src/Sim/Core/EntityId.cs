namespace Rts.Sim.Core;

/// <summary>Generational handle into a slot array. Index 0 is never allocated, so default == None.</summary>
public readonly record struct EntityId(ushort Index, ushort Generation)
{
    public static readonly EntityId None = default;
    public bool IsNone => Index == 0;
    public override string ToString() => IsNone ? "None" : $"#{Index}g{Generation}";
}
