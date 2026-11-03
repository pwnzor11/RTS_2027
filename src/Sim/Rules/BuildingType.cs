namespace Rts.Sim.Rules;

/// <summary>Sim-side building type. Footprint in cells; Power: positive = produces, negative = drains.</summary>
public sealed record BuildingType(string Id, int FootprintW, int FootprintH, int MaxHealth, ArmorClass Armor,
    int Sight, int PrimaryWeapon, int Cost, int Power, IReadOnlyList<string> Prerequisites,
    IReadOnlyList<string> Provides);
