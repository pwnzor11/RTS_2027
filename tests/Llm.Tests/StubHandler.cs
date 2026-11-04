// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using System.Text;

namespace Rts.Llm.Tests;

/// <summary>Fake HttpMessageHandler: never touches the network. Records the request and answers from a delegate.</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    public HttpRequestMessage? Request { get; private set; }
    public string? Body { get; private set; }
    public int Calls { get; private set; }

    public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    public static StubHandler Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        Request = request;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        return await _respond(request, ct);
    }
}
