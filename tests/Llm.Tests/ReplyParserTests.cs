// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;

namespace Rts.Llm.Tests;

public class ReplyParserTests
{
    private const string Full = "{\"directive\":{\"posture\":\"aggressive\",\"focus\":\"army\",\"target\":2,\"mix\":\"anti-air\",\"expand\":true,\"focus_cell\":[10,20]},\"say\":\"Forward!\"}";

    private static readonly DirectiveDraft FullDraft = new(2, 2, 2, 3, true, 10, 20);

    [Fact]
    public void Plain_json()
    {
        var r = ReplyParser.Parse(Full);
        Assert.Equal(FullDraft, r.Directive);
        Assert.Equal("Forward!", r.Say);
    }

    [Fact]
    public void Fenced_json() =>
        Assert.Equal(FullDraft, ReplyParser.Parse("Sure:\n```json\n" + Full + "\n```\nDone.").Directive);

    [Fact]
    public void Prose_around_json_with_braces_in_prose_first()
    {
        var r = ReplyParser.Parse("Thinking {not json} then " + Full + " bye");
        Assert.Equal(FullDraft, r.Directive);
    }

    [Fact]
    public void Braces_inside_strings_are_ignored()
    {
        var r = ReplyParser.Parse("{\"say\":\"a } b { c\",\"directive\":{\"posture\":0}}");
        Assert.Equal("a } b { c", r.Say);
        Assert.Equal((byte)0, r.Directive!.Value.Posture);
    }

    [Fact]
    public void Numbers_and_names_and_strings_of_numbers()
    {
        var r = ReplyParser.Parse("{\"directive\":{\"posture\":3,\"focus\":\"4\",\"mix\":\"Anti Structure\",\"target\":\"auto\",\"expand\":\"yes\"}}");
        Assert.Equal(new DirectiveDraft(3, 4, 255, 4, true, -1, -1), r.Directive);
        Assert.Null(r.Say);
    }

    [Theory]
    [InlineData("all-in", 3)]
    [InlineData("ALL_IN", 3)]
    [InlineData("Turtle", 0)]
    [InlineData("balanced", 1)]
    public void Posture_names(string name, int expected) =>
        Assert.Equal((byte)expected, ReplyParser.Parse("{\"directive\":{\"posture\":\"" + name + "\"}}").Directive!.Value.Posture);

    [Fact]
    public void Out_of_range_clamps()
    {
        var r = ReplyParser.Parse("{\"directive\":{\"posture\":99,\"focus\":-4,\"mix\":17,\"target\":300,\"focus_cell\":[-1,5]}}");
        Assert.Equal(new DirectiveDraft(3, 0, 255, 6, false, -1, -1), r.Directive);
    }

    [Fact]
    public void Huge_and_fractional_numbers_do_not_throw()
    {
        var r = ReplyParser.Parse("{\"directive\":{\"posture\":1e40,\"focus\":2.9,\"target\":-1,\"focus_cell\":[99999999999,3]}}");
        Assert.Equal(new DirectiveDraft(3, 2, 255, 0, false, -1, -1), r.Directive);
    }

    [Fact]
    public void Unknown_values_use_defaults()
    {
        var r = ReplyParser.Parse("{\"directive\":{\"posture\":\"zen\",\"focus\":null,\"mix\":[1],\"target\":\"bob\",\"expand\":\"maybe\",\"focus_cell\":[1]}}");
        Assert.Equal(new DirectiveDraft(1, 2, 255, 0, false, -1, -1), r.Directive);
    }

    [Fact]
    public void Empty_directive_object_gives_defaults() =>
        Assert.Equal(new DirectiveDraft(1, 2, 255, 0, false, -1, -1), ReplyParser.Parse("{\"directive\":{}}").Directive);

    [Theory]
    [InlineData("{\"say\":\"hi\"}")]
    [InlineData("{\"directive\":null,\"say\":\"hi\"}")]
    [InlineData("{\"directive\":5,\"say\":\"hi\"}")]
    public void Missing_directive_is_null(string text)
    {
        var r = ReplyParser.Parse(text);
        Assert.Null(r.Directive);
        Assert.Equal("hi", r.Say);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no json here")]
    [InlineData("{")]
    [InlineData("{\"directive\":{\"posture\":")]
    [InlineData("[1,2,3]")]
    [InlineData("}{}{{{{")]
    public void Garbage_gives_empty_reply(string text)
    {
        var r = ReplyParser.Parse(text);
        Assert.Null(r.Directive);
        Assert.Null(r.Say);
    }

    [Fact]
    public void Deeply_nested_garbage_does_not_throw() =>
        Assert.Null(ReplyParser.Parse(new string('{', 5000) + new string('}', 5000)).Directive);

    [Fact]
    public void Say_is_trimmed_stripped_and_capped()
    {
        var r = ReplyParser.Parse("{\"say\":\"  a\\u0007b\\nc  \"}");
        Assert.Equal("ab c", r.Say);
        var big = ReplyParser.Parse("{\"say\":\"" + new string('x', 100000) + "\"}");
        Assert.Equal(200, big.Say!.Length);
    }

    [Fact]
    public void Say_cap_does_not_split_surrogate_pair()
    {
        var r = ReplyParser.Parse("{\"say\":\"" + new string('x', 199) + "\ud83d\ude00\"}");
        Assert.Equal(199, r.Say!.Length);
    }

    [Fact]
    public void Blank_say_is_null() =>
        Assert.Null(ReplyParser.Parse("{\"say\":\"   \"}").Say);

    [Fact]
    public void Wrong_typed_say_is_null() =>
        Assert.Null(ReplyParser.Parse("{\"say\":42}").Say);
}
