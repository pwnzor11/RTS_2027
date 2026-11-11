// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Game.Screens;
using Rts.Game.Ui.Llm;
using Rts.Llm;

namespace Rts.Game.Sandbox;

/// <summary>
/// M5: the LLM commanders of one game and their shared chat log. Built for a new skirmish (<see cref="SkirmishGame"/>) and again for a
/// loaded save (M5-10), from the slots whose <see cref="SkirmishSlot.Commander"/> is set. Each commander becomes its slot's directive
/// source on the <see cref="AiDriver"/>. Dispose cancels calls still in flight.
/// </summary>
public sealed class CommanderSet : IDisposable
{
    private readonly List<Commander> _commanders = new();

    /// <summary>The commander chat (commanders write, the chat panel reads).</summary>
    public CommanderChatLog Chat { get; } = new();

    /// <summary>One channel per commander, in slot order (empty when no slot has a commander).</summary>
    public IReadOnlyList<ICommanderChannel> Commanders => _commanders;

    /// <summary>Creates a commander for every slot with <see cref="SkirmishSlot.Commander"/> set that has an AI on <paramref name="driver"/>.
    /// Provider "none" or no stored key gives an offline commander (the slot plays its plain tier and the chat says so once).</summary>
    public static CommanderSet Attach(AiDriver driver, IEnumerable<(byte Player, SkirmishSlot Slot)> slots, Settings app)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(app);
        var set = new CommanderSet();
        LlmConfig cfg = LlmHooks.Defaults.Resolve(app.ProviderKind, app.LlmBaseUrl, app.LlmModel);
        IReadOnlyList<CommanderPersona>? personas = null;
        foreach ((byte player, SkirmishSlot slot) in slots)
        {
            if (!slot.Commander || driver.AiOf(player) is null) continue;
            IChatProvider? provider = app.ProviderKind == LlmProviderKind.None ? null : ProviderFactory.Create(cfg, LlmHooks.Keys.Load(app.ProviderKind));   // only this provider's own key (S18 AI-07)
            personas ??= PersonaCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "llm", "personas.toml"));
            string faction = slot.Faction.ToString();
            var commander = new Commander(player, provider, PersonaCatalog.Pick(personas, faction, player), faction, app.LlmCallCap,
                app.LlmChatOn, set.Chat);
            driver.SetDirectiveSource(player, commander);
            set._commanders.Add(commander);
        }
        return set;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (Commander c in _commanders) c.Dispose();
    }
}
