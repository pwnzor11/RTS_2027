using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// Fixed default terrain table built in code until M1-02 loads it from TOML. Speed percent per land type
/// and locomotion. Air flies at 100% over everything.
/// </summary>
public static class DefaultTerrain
{
    public static TerrainTable Create()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        var buildable = new bool[RuleCounts.LandTypes];
        for (int land = 0; land < RuleCounts.LandTypes; land++)
        {
            for (int loco = 0; loco < RuleCounts.Locomotions; loco++)
                speed[land * RuleCounts.Locomotions + loco] = Percent((LandType)land, (Locomotion)loco);
        }
        buildable[(int)LandType.Clear] = true;
        buildable[(int)LandType.Road] = true;
        buildable[(int)LandType.Ash] = true;
        return new TerrainTable(speed, buildable);
    }

    private static int Percent(LandType land, Locomotion loco)
    {
        bool air = loco == Locomotion.Air;
        if (air) return 100;
        switch (land)
        {
            case LandType.Water: return loco == Locomotion.Hover ? 100 : 0;
            case LandType.Cliff: return 0;
            case LandType.Rock: return 0;
            case LandType.Rough: return 75;
            case LandType.Road: return loco == Locomotion.Wheeled ? 125 : 110;
            default: return 100; // Clear, Ash
        }
    }
}
