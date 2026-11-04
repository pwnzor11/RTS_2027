// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;

namespace Rts.Llm.Tests;

public class ContractsTests
{
    [Fact]
    public void Reply_can_keep_the_last_directive()
    {
        var reply = new CommanderReply(null, "Hold the line.");
        Assert.Null(reply.Directive);
    }
}
