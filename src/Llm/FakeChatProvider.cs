// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Llm;

/// <summary>Scripted provider for tests and the offline demo: returns queued results in order and records requests.</summary>
public sealed class FakeChatProvider : IChatProvider
{
    private readonly object _lock = new();
    private readonly Queue<ChatResult> _queue = new();
    private readonly List<ChatRequest> _requests = new();

    /// <summary>Artificial latency applied to every call (honours cancellation).</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <inheritdoc />
    public string Name => "Fake";

    /// <summary>Requests received so far, in order (snapshot).</summary>
    public IReadOnlyList<ChatRequest> Requests { get { lock (_lock) return _requests.ToArray(); } }

    /// <summary>Queues a result.</summary>
    public void Enqueue(ChatResult result) { lock (_lock) _queue.Enqueue(result); }

    /// <summary>Queues a successful text reply.</summary>
    public void EnqueueText(string text, int inputTokens = 0, int outputTokens = 0) =>
        Enqueue(new ChatResult(true, text, null, inputTokens, outputTokens));

    /// <inheritdoc />
    public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock) _requests.Add(request);
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct).ConfigureAwait(false);
        lock (_lock)
            return _queue.Count > 0 ? _queue.Dequeue() : new ChatResult(false, "", "no queued result", 0, 0);
    }
}
