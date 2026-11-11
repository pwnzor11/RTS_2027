// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;

namespace Rts.Game.Ui.Llm;

/// <summary>
/// The logic behind the AI commander settings screen, free of drawing so it can be tested: provider / URL / model / chat / cap
/// edits on a <see cref="Settings"/> value, the key store (the key is only passed through, never kept or shown) and the
/// "Test connection" task.
/// </summary>
public sealed class LlmSettingsModel
{
    /// <summary>The call-cap values the row steps through (0 = no cap). ASSUMED (M5-08).</summary>
    public static readonly IReadOnlyList<int> CapSteps = new[] { 0, 30, 60, 120, 240, 500, 1000 };

    private readonly Func<Settings> _get;
    private readonly Action<Settings> _set;
    private readonly ProviderKeyring _keys;
    private readonly ProviderDefaults _defaults;
    private readonly TestConnectionFunc _test;
    private CancellationTokenSource? _cts;
    private Task<string>? _running;
    private bool _hasKey;

    /// <summary>Creates the model over a settings accessor pair, the per-provider keys, the provider defaults and a test function.</summary>
    public LlmSettingsModel(Func<Settings> get, Action<Settings> set, ProviderKeyring keys, ProviderDefaults defaults, TestConnectionFunc test)
    {
        _get = get;
        _set = set;
        _keys = keys;
        _defaults = defaults;
        _test = test;
        RefreshKey();
    }

    /// <summary>Result line of the last test ("" = none yet).</summary>
    public string TestResult { get; private set; } = "";

    /// <summary>True while a test is running.</summary>
    public bool Testing => _running is { IsCompleted: false };

    /// <summary>The provider kind in the settings.</summary>
    public LlmProviderKind Provider => _get().ProviderKind;

    /// <summary>Provider display name.</summary>
    public static string ProviderLabel(LlmProviderKind kind) => kind switch
    {
        LlmProviderKind.Anthropic => LlmUiText.ProviderAnthropic,
        LlmProviderKind.OpenAiCompatible => LlmUiText.ProviderOpenAi,
        _ => LlmUiText.ProviderNone,
    };

    /// <summary>Moves to the next / previous provider (wraps). Changing provider clears the URL and model so each starts at its default.</summary>
    public void CycleProvider(int direction)
    {
        int next = ((int)Provider + (direction >= 0 ? 1 : 2)) % 3;
        _set(_get() with { LlmProvider = Settings.ProviderName((LlmProviderKind)next), LlmBaseUrl = "", LlmModel = "" });
        RefreshKey();   // each provider has its own key: the status now shows the new provider's
    }

    /// <summary>Sets the base URL (empty = provider default).</summary>
    public void SetBaseUrl(string url) => _set(_get() with { LlmBaseUrl = url.Trim() });

    /// <summary>Sets the model (empty = provider default).</summary>
    public void SetModel(string model) => _set(_get() with { LlmModel = model.Trim() });

    /// <summary>The URL row text: the value, or the provider default marked as such.</summary>
    public string BaseUrlText()
    {
        string v = _get().LlmBaseUrl;
        if (v.Length > 0) return v;
        string d = _defaults.BaseUrlFor(Provider);
        return d.Length == 0 ? "-" : $"{d} ({LlmUiText.DefaultWord})";
    }

    /// <summary>The model row text.</summary>
    public string ModelText()
    {
        string v = _get().LlmModel;
        if (v.Length > 0) return v;
        string d = _defaults.ModelFor(Provider);
        if (Provider == LlmProviderKind.None) return "-";
        return d.Length == 0 ? LlmUiText.TypeModel : $"{d} ({LlmUiText.DefaultWord})";
    }

    /// <summary>True when the "stronger model" toggle applies (Anthropic only).</summary>
    public bool StrongAvailable => Provider == LlmProviderKind.Anthropic;

    /// <summary>True when the stronger model is chosen.</summary>
    public bool StrongOn => StrongAvailable && _get().LlmModel == _defaults.AnthropicStrongModel;

    /// <summary>Switches between the default and the stronger Anthropic model. Does nothing for other providers.</summary>
    public void ToggleStrong()
    {
        if (!StrongAvailable) return;
        _set(_get() with { LlmModel = StrongOn ? "" : _defaults.AnthropicStrongModel });
    }

    /// <summary>Toggles the commander chat.</summary>
    public void ToggleChat() => _set(_get() with { LlmChatOn = !_get().LlmChatOn });

    /// <summary>Steps the call cap through <see cref="CapSteps"/> (no wrap; a value in between moves to the next step in that direction).</summary>
    public void StepCap(int direction)
    {
        int cap = _get().LlmCallCap;
        int next = cap;
        if (direction >= 0)
        {
            foreach (int s in CapSteps)
                if (s > cap) { next = s; break; }
        }
        else
        {
            for (int i = CapSteps.Count - 1; i >= 0; i--)
                if (CapSteps[i] < cap) { next = CapSteps[i]; break; }
        }
        _set(_get() with { LlmCallCap = next });
    }

    /// <summary>The cap row text.</summary>
    public string CapText() => _get().LlmCallCap == 0 ? LlmUiText.NoCap : _get().LlmCallCap.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"key set" or "no key" for the current provider; never the key.</summary>
    public string KeyStatus => _hasKey ? LlmUiText.KeySet : LlmUiText.NoKey;

    /// <summary>True when a key is stored.</summary>
    public bool HasKey => _hasKey;

    /// <summary>Stores a key (trimmed). Returns false when empty or the store failed.</summary>
    public bool SetKey(string key)
    {
        string k = key.Trim();
        bool ok = k.Length > 0 && _keys.Save(Provider, k);
        RefreshKey();
        return ok;
    }

    /// <summary>Shows "could not save the key" in the result line.</summary>
    public void ReportKeyFailure() => TestResult = LlmUiText.KeySaveFailed;

    /// <summary>Deletes the stored key of the current provider.</summary>
    public void ClearKey()
    {
        _keys.Clear(Provider);
        RefreshKey();
    }

    /// <summary>Starts "Test connection" (ignored while one runs). The result arrives in <see cref="TestResult"/> via <see cref="Poll"/>.</summary>
    public void StartTest()
    {
        if (Testing) return;
        if (Provider == LlmProviderKind.None)
        {
            TestResult = LlmUiText.NeedProvider;
            return;
        }

        Settings s = _get();
        LlmConfig config = _defaults.Resolve(s.ProviderKind, s.LlmBaseUrl, s.LlmModel);
        string? key = _keys.Load(s.ProviderKind);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        CancellationToken ct = _cts.Token;
        TestResult = LlmUiText.Testing;
        _running = Task.Run(async () =>
        {
            try
            {
                string r = await _test(config, key, ct).ConfigureAwait(false);
                return Scrub(r, key);
            }
            catch (Exception)
            {
                return LlmUiText.TestFailed; // no exception text: it could echo request details
            }
        });
    }

    /// <summary>Call every frame: picks up a finished test result.</summary>
    public void Poll()
    {
        if (_running is { IsCompleted: true } done)
        {
            TestResult = done.IsCompletedSuccessfully ? done.Result : LlmUiText.TestFailed;
            _running = null;
        }
    }

    /// <summary>Cancels a running test (screen closed).</summary>
    public void Cancel()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void RefreshKey() => _hasKey = _keys.Load(Provider) is not null;

    // Belt and braces: a result line never carries the key, whatever the test function returned.
    private static string Scrub(string result, string? key)
    {
        string r = result.Replace("\r", " ").Replace("\n", " ");
        if (!string.IsNullOrEmpty(key)) r = r.Replace(key, "***");
        return r.Length > 160 ? r[..160] : r;
    }
}
