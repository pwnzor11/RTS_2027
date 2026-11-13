// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Llm;

// M5 contracts (docs/design/09_m5_plan.md). Fixed for the M5 waves; change only through the lead.

/// <summary>One chat turn sent to a provider.</summary>
public sealed record ChatMessage(ChatRole Role, string Text);

/// <summary>Who said a <see cref="ChatMessage"/>.</summary>
public enum ChatRole : byte { User = 0, Assistant = 1 }

/// <summary>A request to a chat provider. <see cref="System"/> is the system prompt.</summary>
public sealed record ChatRequest(string System, IReadOnlyList<ChatMessage> Messages, int MaxTokens = 600);

/// <summary>A provider's answer. On failure <see cref="Ok"/> is false, <see cref="Text"/> is empty and <see cref="Error"/>
/// is a short reason that never contains the API key or request headers.</summary>
public sealed record ChatResult(bool Ok, string Text, string? Error, int InputTokens, int OutputTokens);

/// <summary>A chat-completion backend (Anthropic Messages API or an OpenAI-compatible endpoint).</summary>
public interface IChatProvider
{
    /// <summary>Short display name, e.g. "Anthropic".</summary>
    string Name { get; }

    /// <summary>Sends one request. Never throws for network/HTTP/API errors: returns a failed <see cref="ChatResult"/>.
    /// Throws only <see cref="OperationCanceledException"/> when <paramref name="ct"/> is cancelled.</summary>
    Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct);
}

/// <summary>Provider kinds the settings can name.</summary>
public enum LlmProviderKind : byte { None = 0, Anthropic = 1, OpenAiCompatible = 2 }

/// <summary>Everything needed to build a provider, except the key. <paramref name="Effort"/> (Anthropic only) is sent as
/// <c>output_config.effort</c> when not empty; "" sends nothing (older models reject the field).</summary>
public sealed record LlmConfig(LlmProviderKind Provider, string BaseUrl, string Model, int TimeoutSeconds = 20, string Effort = "");

/// <summary>Where the API key lives. Implementations never log, print or serialise the key anywhere else.</summary>
public interface ISecretStore
{
    /// <summary>The stored key, or null if none.</summary>
    string? Load();

    /// <summary>Stores the key (replaces any old one). Returns false on failure.</summary>
    bool Save(string key);

    /// <summary>Deletes the stored key.</summary>
    void Clear();
}

/// <summary>The LLM's strategy choice before it is packed into a sim command (M5-01). Every field is an integer
/// already clamped to its range; see docs/design/09_m5_plan.md §3.</summary>
public readonly record struct DirectiveDraft(
    byte Posture,        // 0 turtle, 1 balanced, 2 aggressive, 3 all-in
    byte Focus,          // 0 economy, 1 tech, 2 army, 3 air, 4 superweapon
    byte TargetPlayer,   // slot, or 255 = AI chooses
    byte Mix,            // 0 auto, 1 infantry, 2 vehicles, 3 anti-air, 4 anti-structure, 5 anti-armour, 6 anti-infantry (S24)
    bool Expand,
    short FocusX,        // -1 = no focus cell
    short FocusY,
    bool StrongMix = false);   // S24 (M5-12): counter-building strong mix (packed as DirectiveCodec.PackStrong)

/// <summary>S24 (M5-12): one standing order from a reply, before it is packed. <see cref="Kind"/> None clears the slot.
/// <see cref="CellX"/>/<see cref="CellY"/> are -1 when the order has no cell. <see cref="Minute"/> is the game minute an
/// AttackAt launches (0 = at once); <see cref="Amount"/> is what the kind's codec documents.</summary>
public readonly record struct OrderDraft(
    byte Slot,                   // 0..3
    CommanderOrderKind Kind,
    short CellX,
    short CellY,
    int Amount,
    int Minute);

/// <summary>A parsed commander reply: the directive (null = keep the last one), the standing orders (at most four; empty = none)
/// and the in-character line (null = silent).</summary>
public sealed record CommanderReply(DirectiveDraft? Directive, string? Say, IReadOnlyList<OrderDraft>? Orders = null)
{
    /// <summary>The orders, never null (empty when the reply carried none).</summary>
    public IReadOnlyList<OrderDraft> OrderList => Orders ?? Array.Empty<OrderDraft>();
}
