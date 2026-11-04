// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Llm;

/// <summary>Placeholder UI strings of the AI commander settings (M5-08). Keys are the constant names; the wording is ASSUMED
/// and is meant to be replaced by the owner's copy.</summary>
public static class LlmUiText
{
    /// <summary>Row in the main Settings screen that opens the LLM screen.</summary>
    public const string SectionRow = "AI commander (LLM)...";
    /// <summary>Title of the LLM settings panel.</summary>
    public const string Title = "AI commander (LLM)";
    /// <summary>Provider row label.</summary>
    public const string Provider = "Provider";
    /// <summary>Provider value: off.</summary>
    public const string ProviderNone = "Off";
    /// <summary>Provider value: Anthropic.</summary>
    public const string ProviderAnthropic = "Anthropic";
    /// <summary>Provider value: OpenAI-compatible.</summary>
    public const string ProviderOpenAi = "OpenAI-compatible";
    /// <summary>Base URL row label.</summary>
    public const string BaseUrl = "Base URL";
    /// <summary>Model row label.</summary>
    public const string Model = "Model";
    /// <summary>Shown when a text field is empty and the provider default applies.</summary>
    public const string DefaultWord = "default";
    /// <summary>Shown when a model must be typed by the player.</summary>
    public const string TypeModel = "(type a model name)";
    /// <summary>Stronger-model toggle label.</summary>
    public const string StrongModel = "Use stronger model";
    /// <summary>Stronger-model toggle when the provider has none.</summary>
    public const string StrongNotAvailable = "Anthropic only";
    /// <summary>Chat toggle label.</summary>
    public const string Chat = "Commander chat";
    /// <summary>Call cap label.</summary>
    public const string CallCap = "Call cap per game";
    /// <summary>Call cap value for 0.</summary>
    public const string NoCap = "no cap";
    /// <summary>API key row label.</summary>
    public const string ApiKey = "API key";
    /// <summary>Key state: stored.</summary>
    public const string KeySet = "key set";
    /// <summary>Key state: none.</summary>
    public const string NoKey = "no key";
    /// <summary>Set key row.</summary>
    public const string SetKey = "Set API key...";
    /// <summary>Clear key row.</summary>
    public const string ClearKey = "Clear key";
    /// <summary>Test row.</summary>
    public const string TestConnection = "Test connection";
    /// <summary>Test running.</summary>
    public const string Testing = "Testing...";
    /// <summary>Default test result before the real test is wired.</summary>
    public const string NotWired = "not wired yet";
    /// <summary>Test failed without a message.</summary>
    public const string TestFailed = "Test failed.";
    /// <summary>Test needs a provider.</summary>
    public const string NeedProvider = "Choose a provider first.";
    /// <summary>Key could not be saved.</summary>
    public const string KeySaveFailed = "Could not save the key.";
    /// <summary>Back row.</summary>
    public const string Back = "Back";
    /// <summary>Title of the base URL text box.</summary>
    public const string EntryBaseUrl = "Base URL (empty = default)";
    /// <summary>Title of the model text box.</summary>
    public const string EntryModel = "Model name (empty = default)";
    /// <summary>Title of the key text box.</summary>
    public const string EntryKey = "Paste or type your API key";
    /// <summary>Hint under a text box.</summary>
    public const string EntryHint = "Enter = OK    Esc = cancel    Ctrl+V = paste";
}
