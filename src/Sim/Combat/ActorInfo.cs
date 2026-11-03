using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Combat;

/// <summary>Combat-relevant facts about a live actor. Position = unit position, or footprint centre for buildings.</summary>
public readonly record struct ActorInfo(ActorKind Kind, byte Owner, LeptonCoord Position, ArmorClass Armor, int Health);
