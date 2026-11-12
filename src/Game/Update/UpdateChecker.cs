// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net.Http;

namespace Rts.Game.Update;

/// <summary>
/// Asks GitHub once, in the background, whether a newer release exists (M10-23). Never blocks the game thread, gives up after
/// three seconds and swallows every failure: no network, no release, a bad reply, all just mean no notice.
/// </summary>
public sealed class UpdateChecker
{
    /// <summary>How long the request may take.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private volatile UpdateInfo? _found;

    /// <summary>The newer release, once one was found; null until then (and for good if none).</summary>
    public UpdateInfo? Found => _found;

    /// <summary>Starts the check on a background task and returns at once. Does nothing when <paramref name="enabled"/> is false.</summary>
    public void Start(bool enabled, string currentVersion)
    {
        if (!enabled || !UpdateCheck.TryParseVersion(currentVersion, out _)) return;
        _ = Task.Run(() => Run(currentVersion));
    }

    private async Task Run(string currentVersion)
    {
        try
        {
            using var cts = new CancellationTokenSource(Timeout);
            using var http = new HttpClient { Timeout = Timeout };
            using var request = new HttpRequestMessage(HttpMethod.Get, UpdateCheck.LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("RTS_2027");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using HttpResponseMessage response = await http.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return;
            string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            _found = UpdateCheck.FromReleaseJson(json, currentVersion);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or InvalidOperationException or IOException)
        {
            // No network or no answer in time: no notice. The game never depends on this.
        }
    }
}
