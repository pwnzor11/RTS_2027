namespace Rts.Sim.Rules;

/// <summary>Sim-side infantry type (5 per cell, crushable by Crusher vehicles).</summary>
public sealed record InfantryType(string Id, int Speed, int MaxHealth, ArmorClass Armor, int Sight,
    int PrimaryWeapon, int Cost, IReadOnlyList<string> Prerequisites);
