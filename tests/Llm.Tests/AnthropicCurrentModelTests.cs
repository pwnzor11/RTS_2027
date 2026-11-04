// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.Json;
using Rts.Llm;
using Rts.Sim.Core;

namespace Rts.Llm.Tests;

/// <summary>Session 7: current Anthropic models (Q-40) think before answering and take output_config.effort; M5-01 directive conversion.</summary>
public class AnthropicCurrentModelTests
{
    private static readonly ChatRequest Req = new("sys", new[] { new ChatMessage(ChatRole.User, "hi") }, 900);

    [Fact]
    public async Task Text_after_thinking_blocks_is_read()
    {
        var h = StubHandler.Json("{\"content\":[{\"type\":\"thinking\",\"thinking\":\"\",\"signature\":\"abc\"},"
            + "{\"type\":\"redacted_thinking\",\"data\":\"zz\"},{\"type\":\"text\",\"text\":\"{\\\"say\\\":\\\"ok\\\"}\"}],"
            + "\"usage\":{\"input_tokens\":3,\"output_tokens\":40}}");
        var r = await new AnthropicProvider(new(LlmProviderKind.Anthropic, "", "m"), "k", h).CompleteAsync(Req, CancellationToken.None);
        Assert.True(r.Ok);
        Assert.Equal("{\"say\":\"ok\"}", r.Text);
        Assert.Equal(40, r.OutputTokens);
    }

    [Fact]
    public async Task Only_thinking_is_a_bad_response()
    {
        var h = StubHandler.Json("{\"content\":[{\"type\":\"thinking\",\"thinking\":\"\",\"signature\":\"abc\"}]}");
        var r = await new AnthropicProvider(new(LlmProviderKind.Anthropic, "", "m"), "k", h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
    }

    [Theory]
    [InlineData("low", true)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    public async Task Effort_is_sent_only_when_set(string effort, bool sent)
    {
        var h = StubHandler.Json("{\"content\":[{\"type\":\"text\",\"text\":\"x\"}]}");
        await new AnthropicProvider(new(LlmProviderKind.Anthropic, "", "m", Effort: effort), "k", h).CompleteAsync(Req, CancellationToken.None);
        using var doc = JsonDocument.Parse(h.Body!);
        Assert.Equal(sent, doc.RootElement.TryGetProperty("output_config", out var oc));
        if (sent) Assert.Equal("low", oc.GetProperty("effort").GetString());
    }

    [Fact]
    public void Builtin_and_data_defaults_name_current_models()
    {
        Assert.Equal("claude-haiku-5-5", ProviderDefaults.Builtin.AnthropicModel);
        Assert.Equal("claude-sonnet-5-5", ProviderDefaults.Builtin.AnthropicStrongModel);
        var d = ProviderDefaults.Parse("[anthropic]\ndefault_model = \"a\"\neffort = \"\"\n");
        Assert.Equal("", d.AnthropicEffort);
        Assert.Equal("", d.Resolve(LlmProviderKind.Anthropic, "", "").Effort);
        Assert.Equal("low", ProviderDefaults.Builtin.Resolve(LlmProviderKind.Anthropic, "", "").Effort);
        Assert.Equal("", ProviderDefaults.Builtin.Resolve(LlmProviderKind.OpenAiCompatible, "", "x").Effort);
    }

    [Fact]
    public void Draft_converts_to_a_directive_command_and_back()
    {
        var draft = new DirectiveDraft(3, 1, 2, 4, true, 10, 12);
        Command c = draft.ToCommand(500, 1, 7);
        Assert.Equal((CommandKind.Directive, 500, (byte)1, (ushort)7, new CellCoord(10, 12)), (c.Kind, c.ExecTick, c.Player, c.Sequence, c.Cell));
        Assert.Equal(((byte)3, (byte)1, (byte)2, (byte)4, true), DirectiveCodec.Unpack(c.Arg));
        Assert.Equal(Directive.NoCell, (draft with { FocusX = -1 }).ToCommand(0, 0, 0).Cell);
        Assert.Null(DirectiveCommands.ToDraft(Directive.None));
        var stored = new Directive(true, 3, 1, 2, 4, true, new CellCoord(10, 12), 9);
        Assert.Equal(draft, DirectiveCommands.ToDraft(stored));
    }
}
