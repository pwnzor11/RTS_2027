using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Ui;

/// <summary>
/// The F1 debug console for the sandbox: a one-line input bar at the bottom of the screen with the last few output
/// lines above it. It captures typing while open, so game hotkeys are ignored. Commands either issue
/// <see cref="CommandKind.Debug"/> commands through <see cref="SimSnapshotSource.EnqueueDebug"/> (recorded in the
/// replay) or change the front end only (reveal, speed, pause, step). Parsing is in <see cref="DebugCommandParser"/>.
/// </summary>
public sealed class DebugConsole
{
    /// <summary>Output lines shown above the input line.</summary>
    public const int VisibleOutputLines = 6;

    private const int Scale = 2;
    private const int Pad = 8;
    private const int LineGapPx = 4;
    private const int OutputKept = 200;
    private const int HistoryKept = 50;
    private const int InputLimit = 200;
    private static readonly Color PanelColour = new(0, 0, 0, 200);
    private static readonly Color TextColour = new(0xD8, 0xC8, 0x8A);
    private static readonly Color InputColour = new(0xFF, 0xFF, 0xFF);

    private readonly SimSnapshotSource _sim;
    private readonly Selection _selection;
    private readonly Camera _camera;
    private readonly DebugCatalog _catalog;
    private readonly List<string> _output = new();
    private readonly List<string> _history = new();
    private string _input = string.Empty;
    private int _historyIndex = -1; // -1 = not browsing history
    private bool _revealed;
    private int _viewerBeforeReveal;

    /// <summary>Creates a closed console for the sandbox.</summary>
    /// <param name="sim">The sandbox; the console only issues commands through it.</param>
    /// <param name="selection">The selected units, used by `kill`.</param>
    /// <param name="camera">Maps the mouse to the cell that `spawn` uses.</param>
    public DebugConsole(SimSnapshotSource sim, Selection selection, Camera camera)
    {
        _sim = sim;
        _selection = selection;
        _camera = camera;
        _catalog = new DebugCatalog(
            sim.Rules.Vehicles.Select(v => v.Id).ToList(),
            sim.Rules.Infantry.Select(i => i.Id).ToList(),
            SimSnapshotSource.PlayerCount);
    }

    /// <summary>True while the console is open and captures the keyboard.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Opens the console if it is closed, closes it if it is open.</summary>
    public void Toggle() => IsOpen = !IsOpen;

    /// <summary>Adds a typed character (from the window's text-input event). Only printable ASCII is kept.</summary>
    /// <param name="ch">The character typed.</param>
    public void OnTextInput(char ch)
    {
        if (!IsOpen || ch < ' ' || ch > '~' || _input.Length >= InputLimit) return;
        _input += ch;
        _historyIndex = -1;
    }

    /// <summary>
    /// Handles the keys while open: Enter runs the line, Backspace deletes, Up and Down recall history, Esc closes.
    /// </summary>
    /// <param name="keys">Keyboard state this frame.</param>
    /// <param name="previous">Keyboard state last frame, for key-down edges.</param>
    /// <param name="mouse">Mouse position in window pixels, for `spawn`.</param>
    /// <returns>True when Esc closed the console, so the caller does not also treat Esc as quit.</returns>
    public bool Update(KeyboardState keys, KeyboardState previous, Point mouse)
    {
        if (Pressed(keys, previous, Keys.Escape))
        {
            IsOpen = false;
            return true;
        }

        if (Pressed(keys, previous, Keys.Enter))
        {
            string line = _input.Trim();
            _input = string.Empty;
            _historyIndex = -1;
            if (line.Length > 0)
            {
                Remember(line);
                Run(line, mouse);
            }
        }

        if (Pressed(keys, previous, Keys.Back) && _input.Length > 0)
        {
            _input = _input[..^1];
        }

        if (Pressed(keys, previous, Keys.Up) && _history.Count > 0)
        {
            _historyIndex = _historyIndex < 0 ? _history.Count - 1 : Math.Max(0, _historyIndex - 1);
            _input = _history[_historyIndex];
        }

        if (Pressed(keys, previous, Keys.Down) && _historyIndex >= 0)
        {
            _historyIndex++;
            _input = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
            if (_historyIndex >= _history.Count) _historyIndex = -1;
        }

        return false;
    }

    /// <summary>Draws the console along the bottom of the screen. Does nothing while closed.</summary>
    /// <param name="batch">A batch already begun.</param>
    /// <param name="pixel">A 1x1 white texture.</param>
    /// <param name="viewport">The window viewport, for placement and width.</param>
    public void Draw(SpriteBatch batch, Texture2D pixel, Viewport viewport)
    {
        if (!IsOpen) return;

        int lineCount = VisibleOutputLines + 1;
        int lineHeight = BitmapFont.GlyphHeight * Scale + LineGapPx;
        int height = lineCount * lineHeight + Pad;
        int top = viewport.Height - height;
        batch.Draw(pixel, new Rectangle(0, top, viewport.Width, height), PanelColour);

        int maxChars = Math.Max(1, (viewport.Width - 2 * Pad) / (BitmapFont.Advance * Scale));
        int firstOutput = Math.Max(0, _output.Count - VisibleOutputLines);
        for (int i = 0; i < VisibleOutputLines; i++)
        {
            int index = firstOutput + i;
            if (index >= _output.Count) break;
            string text = Clip(_output[index], maxChars);
            BitmapFont.Draw(batch, pixel, text, new Vector2(Pad, top + Pad / 2 + i * lineHeight), TextColour, Scale);
        }

        // The input shows its end (the part being typed); the cursor marks where the next character goes.
        string prompt = "> " + _input + "_";
        if (prompt.Length > maxChars) prompt = "> " + prompt[^(maxChars - 2)..];
        BitmapFont.Draw(batch, pixel, prompt, new Vector2(Pad, top + Pad / 2 + VisibleOutputLines * lineHeight),
            InputColour, Scale);
    }

    private void Run(string line, Point mouse)
    {
        DebugParseResult parsed = DebugCommandParser.Parse(line, _catalog);
        if (!parsed.Succeeded)
        {
            Print(parsed.Error!);
            return;
        }

        switch (parsed.Command)
        {
            case HelpCommand:
                foreach (string help in DebugCommandParser.HelpLines(_catalog)) Print(help);
                break;
            case SpawnCommand spawn:
                Spawn(spawn, mouse);
                break;
            case CashCommand cash:
                _sim.EnqueueDebug(DebugOp.SetCash, cash.Amount, default, default, cash.Player);
                Print($"cash of player {cash.Player} set to {cash.Amount} (next tick)");
                break;
            case KillCommand:
                Kill();
                break;
            case RevealCommand:
                ToggleReveal();
                break;
            case SpeedCommand speed:
                _sim.SpeedMultiplier = speed.Multiplier;
                Print($"speed x{speed.Multiplier}");
                break;
            case PauseCommand:
                _sim.Paused = !_sim.Paused;
                Print(_sim.Paused ? "paused" : "running");
                break;
            case StepCommand step:
                _sim.Paused = true;
                _sim.StepTicks(step.Ticks);
                Print($"paused; stepped {step.Ticks}, now tick {_sim.Tick}");
                break;
        }
    }

    private void Spawn(SpawnCommand spawn, Point mouse)
    {
        CellCoord cell = Camera.CellAt(_camera.WorldFromScreen(mouse));
        if (!_sim.Map.Contains(cell))
        {
            Print("the mouse is off the map; move it over the map and try again");
            return;
        }

        DebugOp op = spawn.Kind == ActorKind.Vehicle ? DebugOp.SpawnVehicle : DebugOp.SpawnInfantry;
        for (int i = 0; i < spawn.Count; i++)
        {
            _sim.EnqueueDebug(op, spawn.TypeIndex, cell, default, spawn.Player);
        }

        Print($"queued {spawn.Count} {spawn.TypeId} for player {spawn.Player} at cell {cell.X},{cell.Y}");
    }

    private void Kill()
    {
        IReadOnlyList<UnitKey> units = _selection.Units;
        if (units.Count == 0)
        {
            Print("nothing selected; select units with the mouse first");
            return;
        }

        foreach (UnitKey unit in units.ToList())
        {
            _sim.EnqueueDebug(DebugOp.Kill, (int)unit.Kind, default, unit.Id);
        }

        Print($"killing {units.Count} selected unit(s) (next tick)");
    }

    // Front-end only: the viewer cycles 0 -> 1 -> omniscient -> 0, so at most two steps reach any view.
    private void ToggleReveal()
    {
        int target;
        if (_revealed)
        {
            target = _viewerBeforeReveal;
            _revealed = false;
        }
        else
        {
            _viewerBeforeReveal = _sim.Viewer;
            target = -1;
            _revealed = true;
        }

        for (int i = 0; i < 3 && _sim.Viewer != target; i++) _sim.CycleViewer();
        Print(_revealed ? "reveal on: omniscient view" : $"reveal off: viewer {_sim.Viewer}");
    }

    private void Remember(string line)
    {
        _history.Add(line);
        if (_history.Count > HistoryKept) _history.RemoveAt(0);
    }

    private void Print(string text)
    {
        _output.Add(text);
        if (_output.Count > OutputKept) _output.RemoveAt(0);
    }

    // True only on the frame the key goes down.
    private static bool Pressed(KeyboardState keys, KeyboardState previous, Keys key) =>
        keys.IsKeyDown(key) && previous.IsKeyUp(key);

    private static string Clip(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[..maxChars];
}
