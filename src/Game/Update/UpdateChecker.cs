// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net.Http;

namespace Rts.Game.Update;

/// <summary>Where the last update check stands, for the Settings "Check now" line (A43 GAMESYS-02).</summary>
public enum UpdateCheckStatus
{
    /// <summary>No check has run in this session.</summary>
    Idle,
    /// <summary>A check is running.</summary>
    Checking,
    /// <summary>The latest release is not newer than this build.</summary>
    UpToDate,
    /// <summary>A newer release was found (see <see cref="UpdateChecker.Found"/>).</summary>
    Available,
    /// <summary>GitHub gave no usable answer (no network, timeout, error status).</summary>
    Failed,
    /// <summary>This build has no readable version, so there is nothing to compare.</summary>
    NoVersion,
}

/// <summary>
/// Asks GitHub whether a newer release exists (M10-23). The start-up check runs once in the background and only when the setting is on;
/// "Check now" runs the same check once on request. Never blocks the game thread, gives up after three seconds and swallows every failure.
/// </summary>
public sealed class UpdateChecker
{
    /// <summary>How long the request may take.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly Func<CancellationToken, Task<string?>> _fetch;
    private volatile UpdateInfo? _found;
    private volatile UpdateCheckStatus _status;

    /// <summary>Creates the checker. <paramref name="fetch"/> returns the release JSON, or null when there was no answer (tests pass a fake); null = GitHub.</summary>
    public UpdateChecker(Func<CancellationToken, Task<string?>>? fetch = null) => _fetch = fetch ?? FetchLatestJson;

    /// <summary>The newer release, once one was found; null until then (and for good if none).</summary>
    public UpdateInfo? Found => _found;

    /// <summary>Where the last check stands.</summary>
    public UpdateCheckStatus Status => _status;

    /// <summary>Starts the start-up check on a background task and returns at once. Does nothing (no request) when <paramref name="enabled"/> is false.</summary>
    public void Start(bool enabled, string currentVersion)
    {
        if (!enabled) return;
        _ = Task.Run(() => CheckAsync(currentVersion));
    }

    /// <summary>"Check now": runs the check once in the background whatever the setting says. Ignored while a check is already running.</summary>
    public void CheckNow(string currentVersion)
    {
        if (_status == UpdateCheckStatus.Checking) return;
        _status = UpdateCheckStatus.Checking;
        _ = Task.Run(() => CheckAsync(currentVersion));
    }

    /// <summary>Runs one check and records the result in <see cref="Found"/> and <see cref="Status"/>.</summary>
    public async Task CheckAsync(string currentVersion)
    {
        if (!UpdateCheck.TryParseVersion(currentVersion, out _))
        {
            _status = UpdateCheckStatus.NoVersion;
            return;
        }
        _status = UpdateCheckStatus.Checking;
        try
        {
            using var cts = new CancellationTokenSource(Timeout);
            string? json = await _fetch(cts.Token).ConfigureAwait(false);
            if (json is null)
            {
                _status = UpdateCheckStatus.Failed;
                return;
            }
            UpdateInfo? found = UpdateCheck.FromReleaseJson(json, currentVersion);
            if (found is not null) _found = found;
            _status = found is null ? UpdateCheckStatus.UpToDate : UpdateCheckStatus.Available;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or InvalidOperationException or IOException)
        {
            _status = UpdateCheckStatus.Failed;   // no network or no answer in time: no notice, the game never depends on this
        }
    }

    /// <summary>The real request: the latest release JSON, or null for any failure or a non-success status.</summary>
    private static async Task<string?> FetchLatestJson(CancellationToken token)
    {
        using var http = new HttpClient { Timeout = Timeout };
        using var request = new HttpRequestMessage(HttpMethod.Get, UpdateCheck.LatestReleaseUrl);
        request.Headers.UserAgent.ParseAdd("RTS_2027");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using HttpResponseMessage response = await http.SendAsync(request, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
    }
}
