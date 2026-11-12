// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>
/// The state of a loading screen (M10-20), without graphics: the steps, how far they got, and the result. One <see cref="Advance"/> runs one step;
/// after the last step <see cref="Finish"/> builds the screen to show. A step that throws leaves the model <see cref="Failed"/> with the exception.
/// </summary>
public sealed class LoadingModel
{
    private readonly IReadOnlyList<LoadingStep> _steps;
    private readonly Func<IScreen> _finish;
    private int _done;

    /// <summary>Creates the model.</summary>
    /// <param name="steps">The work, in order (may be empty).</param>
    /// <param name="finish">Builds the screen that replaces the loading screen; runs once after the last step.</param>
    public LoadingModel(IReadOnlyList<LoadingStep> steps, Func<IScreen> finish)
    {
        _steps = steps;
        _finish = finish;
    }

    /// <summary>A single-step load: <paramref name="build"/> is the work and its screen is the result.</summary>
    public static LoadingModel Single(string label, Func<IScreen> build)
    {
        IScreen? built = null;
        return new LoadingModel(new[] { new LoadingStep(label, () => built = build()) }, () => built!);
    }

    /// <summary>Steps finished.</summary>
    public int Done => _done;

    /// <summary>Steps in all.</summary>
    public int Total => _steps.Count;

    /// <summary>True when the bar can show real progress (more than one step); with one step the screen shows a moving bar instead.</summary>
    public bool HasProgress => _steps.Count > 1;

    /// <summary>Fraction finished, 0 to 1.</summary>
    public float Fraction => _steps.Count == 0 ? 0f : (float)_done / _steps.Count;

    /// <summary>The label of the step that runs next, or "" when none is left.</summary>
    public string CurrentLabel => _done < _steps.Count ? _steps[_done].Label : "";

    /// <summary>The screen to show when the load is over.</summary>
    public IScreen? Result { get; private set; }

    /// <summary>The exception a step or the finish threw, or null.</summary>
    public Exception? Failed { get; private set; }

    /// <summary>True when the result is ready or a step failed.</summary>
    public bool IsOver => Result is not null || Failed is not null;

    /// <summary>Runs the next step (or the finish, when all steps are done). Does nothing once over.</summary>
    public void Advance()
    {
        if (IsOver) return;
        try
        {
            if (_done < _steps.Count)
            {
                _steps[_done].Run();
                _done++;
            }

            if (_done >= _steps.Count) Result = _finish();
        }
        catch (Exception e)
        {
            Failed = e;
        }
    }
}
