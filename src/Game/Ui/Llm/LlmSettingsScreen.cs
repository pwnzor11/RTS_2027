// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Screens;
using Rts.Llm;

namespace Rts.Game.Ui.Llm;

/// <summary>
/// The "AI commander (LLM)" settings page: provider, base URL, model, stronger-model toggle, chat on/off, call cap, API key
/// (set / clear, shows only "key set" or "no key") and Test connection. Reached from the Settings screen. Settings are
/// written when the page closes; the key goes to the encrypted key store only, never into settings.
/// </summary>
public sealed class LlmSettingsScreen : IScreen
{
    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private readonly string? _path;
    private Point _mouse;

    /// <summary>Creates the page. <paramref name="settingsPath"/> overrides the settings file (tests); null = default.</summary>
    public LlmSettingsScreen(GameServices services, string? settingsPath = null)
    {
        _s = services;
        _path = settingsPath;
        Model = new LlmSettingsModel(() => _s.Settings, v => _s.Settings = v, LlmHooks.SecretStore, LlmHooks.Defaults, LlmHooks.TestConnection);
        LlmSettingsModel m = Model;
        _panel = new MenuPanel(LlmUiText.Title, new[]
        {
            new MenuItem(() => $"{LlmUiText.Provider}: {LlmSettingsModel.ProviderLabel(m.Provider)}", () => m.CycleProvider(1), m.CycleProvider,
                new MenuButton("<", () => m.CycleProvider(-1)), new MenuButton(">", () => m.CycleProvider(1))),
            new MenuItem(() => $"{LlmUiText.BaseUrl}: {m.BaseUrlText()}", EditUrl),
            new MenuItem(() => $"{LlmUiText.Model}: {m.ModelText()}", EditModel),
            new MenuItem(() => $"{LlmUiText.StrongModel}: {(m.StrongAvailable ? OnOff(m.StrongOn) : LlmUiText.StrongNotAvailable)}",
                m.ToggleStrong, _ => m.ToggleStrong(), new MenuButton("Toggle", m.ToggleStrong)),
            new MenuItem(() => $"{LlmUiText.Chat}: {OnOff(_s.Settings.LlmChatOn)}", m.ToggleChat, _ => m.ToggleChat(),
                new MenuButton("Toggle", m.ToggleChat)),
            new MenuItem(() => $"{LlmUiText.CallCap}: {m.CapText()}", null, m.StepCap,
                new MenuButton("-", () => m.StepCap(-1)), new MenuButton("+", () => m.StepCap(1))),
            new MenuItem(() => $"{LlmUiText.ApiKey}: {m.KeyStatus}", EditKey),
            new MenuItem(() => LlmUiText.SetKey, EditKey),
            new MenuItem(() => LlmUiText.ClearKey, m.ClearKey),
            new MenuItem(() => LlmUiText.TestConnection, m.StartTest),
            new MenuItem(() => m.TestResult.Length == 0 ? " " : m.TestResult),
            new MenuItem(() => LlmUiText.Back, Back),
        });
    }

    /// <summary>The screen's logic, exposed for tests.</summary>
    public LlmSettingsModel Model { get; }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
    }

    /// <inheritdoc />
    public void Exit()
    {
        Model.Cancel();
        _s.Settings.Save(_path);
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        Model.Poll();
        if (input.ConsumePressed(Keys.Escape))
        {
            Back();
            return;
        }

        int before = _panel.Selected;
        _panel.Update(input, _s.Viewport, _s.UiScale);
        if (_panel.Selected != before) MenuSounds.Play(_s.Audio, MenuSounds.Move);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }

    private static string OnOff(bool on) => on ? "On" : "Off";

    private void EditUrl() =>
        _s.Screens.Push(new TextEntryScreen(_s, LlmUiText.EntryBaseUrl, _s.Settings.LlmBaseUrl, false, 200, Model.SetBaseUrl));

    private void EditModel() =>
        _s.Screens.Push(new TextEntryScreen(_s, LlmUiText.EntryModel, _s.Settings.LlmModel, false, 100, Model.SetModel));

    private void EditKey() =>
        _s.Screens.Push(new TextEntryScreen(_s, LlmUiText.EntryKey, "", true, 400, key =>
        {
            if (key.Length > 0 && !Model.SetKey(key)) Model.ReportKeyFailure();
        }));

    private void Back()
    {
        MenuSounds.Play(_s.Audio, MenuSounds.Back);
        _s.Screens.Pop();
    }
}
