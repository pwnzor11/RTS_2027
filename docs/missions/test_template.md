# Bot test template for a mission (M8-09)

Every mission gets one test file that plays it with a scripted human. `MissionBot` (tests/Scripting.Tests/MissionBot.cs) wraps a
`MissionSession` and sends the same commands the UI sends. The tutorial test (`TutorialMissionTests.cs`) is the full worked example.

## What the bot gives you
| Call | Does |
|---|---|
| `new MissionBot(session)` | plays slot 0 (pass a slot as 2nd argument for another player) |
| `Idle()` / `Idle(n)` | run 1 / n ticks with no command (stops when the game is over) |
| `OrderAll(CommandKind.Move or AttackMove, cell)` | order every soldier and non-harvester vehicle of the player, one tick |
| `Train("auth_rifleman")` | queue any unit or building by rules id |
| `TryPlace(id, cell)` | place a finished building with its top-left on `cell`; true if it appeared |
| `BuildAndPlace(id, candidateCells, maxTicks)` | queue, then try the cells every 10 ticks until it stands |
| `Count(id)`, `Cash`, `Done(objectiveId)` | read the world |
| `RunUntil(cond, tickLimit, every)` / `RunUntilObjective(id, tickLimit)` | idle until true; tick limits are absolute ticks |
| `Diagnostics()` | one-line dump (tick, outcome, objectives, cash, messages, errors): use it as the assert message |

## Write a test
1. Create `tests/Scripting.Tests/<Mission>Tests.cs`, same header as the other files, namespace `Rts.Scripting.Tests`.
2. One idle test: run 3000 ticks with no commands, assert `Host.Errors` empty, `m.Def.Locale.Warnings` empty, no `Won`.
3. One winning test: walk the mission in script order, wait for each objective, assert `Won` before the design tick limit.
4. Cells for buildings must be on buildable land (not Rough, Rock, Cliff, Water) and within the build radius of an own building;
   pass a ring of candidates and let `BuildAndPlace` find the first that works.
5. Do not touch `src/**`. If a step cannot be driven with the bot, add a method to `MissionBot` (keep it mission-neutral).

## Example
```csharp
public sealed class Auth02Tests
{
    [Fact]
    public void A_bot_wins()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_02");
        var bot = new MissionBot(m);

        bot.OrderAll(CommandKind.AttackMove, new CellCoord(40, 12));
        Assert.True(bot.RunUntilObjective("reach", 6000), bot.Diagnostics());

        var spots = new[] { new CellCoord(22, 20), new CellCoord(25, 20), new CellCoord(22, 24) };
        Assert.True(bot.BuildAndPlace("auth_power", spots), bot.Diagnostics());
        bot.Train("auth_rifleman");

        Assert.True(bot.RunUntil(() => m.Host.Outcome == MissionOutcome.Won, 12000, every: 10), bot.Diagnostics());
        Assert.Empty(m.Host.Errors);
    }
}
```

## Check without a test
`dotnet run --project src/Game -c Release -- --mission-check <id> <ticks>` runs the mission with an idle human and the AI playing
its slots, then prints errors and objective counts (exit 1 on a script error). It does not prove the mission is winnable; the bot test does.

## Known limits
- The script API cannot read stored credits (`Player.Cash` is cash only; refinery income lands in storage), so "collect income"
  steps use another signal (the tutorial watches the Scraper visit the patch and leave).
- A team with no units and buildings ends the game at once. Keep an enemy anchor alive until the script wins (tut_01's `reserve`).
