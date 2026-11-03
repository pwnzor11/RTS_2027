using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Combat;

/// <summary>Shared helpers for combat tests: the real test rules, a flat map, command builders.</summary>
internal static class CombatFixture
{
    public static readonly RuleSet Loaded = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
    public static readonly Command[] None = Array.Empty<Command>();

    public static int Veh(string id) => SimRules.IndexOf(Loaded.Sim.Vehicles, id, v => v.Id);
    public static int Inf(string id) => SimRules.IndexOf(Loaded.Sim.Infantry, id, v => v.Id);
    public static int Bld(string id) => SimRules.IndexOf(Loaded.Sim.Buildings, id, v => v.Id);

    public static Simulation NewSim(ulong seed = 1, int size = 32) =>
        new(new SimSetup(seed, MapData.CreateFlat(size, size), 2, Loaded.Sim, Loaded.RulesHash) { AllowDebug = true });

    private static ushort _seq;

    public static Command Attack(Simulation sim, byte player, ActorKind subjectKind, EntityId subject, ActorKind targetKind, EntityId target) =>
        new(sim.Tick, player, _seq++, CommandKind.Attack, subject, target, default, (int)subjectKind | ((int)targetKind << 8));

    public static Command AttackMove(Simulation sim, byte player, ActorKind subjectKind, EntityId subject, int x, int y) =>
        new(sim.Tick, player, _seq++, CommandKind.AttackMove, subject, default, new CellCoord(x, y), (int)subjectKind);

    public static Command Guard(Simulation sim, byte player, ActorKind subjectKind, EntityId subject, int x, int y) =>
        new(sim.Tick, player, _seq++, CommandKind.Guard, subject, default, new CellCoord(x, y), (int)subjectKind);

    public static Command Move(Simulation sim, byte player, ActorKind subjectKind, EntityId subject, int x, int y) =>
        new(sim.Tick, player, _seq++, CommandKind.Move, subject, default, new CellCoord(x, y), (int)subjectKind);

    public static void Run(Simulation sim, int ticks, params Command[] first)
    {
        for (int i = 0; i < ticks; i++) sim.Step(i == 0 ? first : None);
    }
}
