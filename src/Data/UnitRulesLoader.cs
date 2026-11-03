using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Parses vehicle and infantry entries (vehicles.toml, infantry.toml).</summary>
internal static class UnitRulesLoader
{
    private static readonly string[] VehicleKeys =
    [
        "speed", "max_health", "locomotion", "turn_rate", "armor", "sight", "primary_weapon",
        "has_turret", "turret_turn_rate", "crusher", "cost", "prerequisites",
    ];

    private static readonly string[] InfantryKeys =
        ["speed", "max_health", "armor", "sight", "primary_weapon", "cost", "prerequisites"];

    /// <summary>Parses one vehicle. <paramref name="weapons"/> must already be sorted by id.</summary>
    public static VehicleType? ParseVehicle(RulesFields f, IReadOnlyList<WeaponType> weapons, RulesReferences refs)
    {
        f.CheckKeys(VehicleKeys);
        int? speed = f.Int("speed", 1, int.MaxValue);
        int? maxHealth = f.Int("max_health", 1, int.MaxValue);
        Locomotion? locomotion = f.Choice("locomotion", RulesEnums.ParseLocomotion, RulesEnums.LocomotionKeys);
        int? turnRate = f.Int("turn_rate", 0, 255);
        ArmorClass? armor = f.Choice("armor", RulesEnums.ParseArmor, RulesEnums.ArmorKeys);
        int? sight = f.Int("sight", 0, SimConstants.MaxMapCells);
        int? weapon = f.WeaponIndex(weapons);
        bool? hasTurret = f.Bool("has_turret");
        int? turretTurnRate = f.Int("turret_turn_rate", 0, 255);
        bool? crusher = f.Bool("crusher");
        int? cost = f.Int("cost", 0, int.MaxValue);
        List<string>? prerequisites = f.StrList("prerequisites");
        if (speed is null || maxHealth is null || locomotion is null || turnRate is null || armor is null
            || sight is null || weapon is null || hasTurret is null || turretTurnRate is null || crusher is null
            || cost is null || prerequisites is null)
        {
            return null;
        }
        refs.AddAll(f, "prerequisites", prerequisites);
        return new VehicleType(f.Id, locomotion.Value, speed.Value, turnRate.Value, maxHealth.Value, armor.Value,
            sight.Value, weapon.Value, hasTurret.Value, turretTurnRate.Value, crusher.Value, cost.Value, prerequisites);
    }

    /// <summary>Parses one infantry type. <paramref name="weapons"/> must already be sorted by id.</summary>
    public static InfantryType? ParseInfantry(RulesFields f, IReadOnlyList<WeaponType> weapons, RulesReferences refs)
    {
        f.CheckKeys(InfantryKeys);
        int? speed = f.Int("speed", 1, int.MaxValue);
        int? maxHealth = f.Int("max_health", 1, int.MaxValue);
        ArmorClass? armor = f.Choice("armor", RulesEnums.ParseArmor, RulesEnums.ArmorKeys);
        int? sight = f.Int("sight", 0, SimConstants.MaxMapCells);
        int? weapon = f.WeaponIndex(weapons);
        int? cost = f.Int("cost", 0, int.MaxValue);
        List<string>? prerequisites = f.StrList("prerequisites");
        if (speed is null || maxHealth is null || armor is null || sight is null || weapon is null || cost is null
            || prerequisites is null)
        {
            return null;
        }
        refs.AddAll(f, "prerequisites", prerequisites);
        return new InfantryType(f.Id, speed.Value, maxHealth.Value, armor.Value, sight.Value, weapon.Value,
            cost.Value, prerequisites);
    }
}
