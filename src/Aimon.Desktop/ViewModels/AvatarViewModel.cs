using Aimon.Application.Abstractions;
using Aimon.Application.Graphics;
using Aimon.Domain;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aimon.Desktop.ViewModels;

/// <summary>
/// The on-screen avatar (implementation_plan.md, Phase 6). It drives an <see cref="IGraphicsEngine"/>
/// with a dispatcher timer clocked at each frame set's fps, and reflects the AI's semantic
/// <see cref="AgentState"/> — call <see cref="OnAgentState"/> as the chat stream reports state changes.
/// Per D1 ("reactions first"), the current renderer draws a state-coloured, pulsing placeholder; a
/// real bitmap frame renderer is a later, isolated drop-in behind the same engine.
/// </summary>
public partial class AvatarViewModel : ViewModelBase, IDisposable
{
    private readonly IGraphicsEngine? _engine;
    private readonly DispatcherTimer? _timer;
    private AgentState? _renderedState;

    [ObservableProperty]
    public partial string StateLabel { get; set; }

    [ObservableProperty]
    public partial string FrameLabel { get; set; }

    [ObservableProperty]
    public partial IBrush AvatarBrush { get; set; }

    [ObservableProperty]
    public partial double AvatarDiameter { get; set; }

    // Design-time constructor for the XAML previewer.
    public AvatarViewModel()
    {
        StateLabel = AgentState.Idle.ToString();
        FrameLabel = "—";
        AvatarBrush = BrushFor(AgentState.Idle);
        AvatarDiameter = 120;
    }

    // Runtime constructor resolved through the DI container in App.axaml.cs.
    public AvatarViewModel(IGraphicsEngine engine)
        : this()
    {
        _engine = engine;
        _engine.FrameChanged += OnFrameChanged;

        // FrameSequencer.LoadAsync completes synchronously, so the avatar is ready immediately.
        _ = _engine.LoadAsync(BuiltInAnimationLibrary.Create(), CancellationToken.None);

        _timer = new DispatcherTimer { Interval = IntervalFor(_engine.FramesPerSecond) };
        _timer.Tick += (_, _) => _engine.Advance();
        _timer.Start();

        Refresh();
    }

    /// <summary>Reacts to a semantic state change from the pipeline by switching the played frame set.</summary>
    public void OnAgentState(AgentState state)
    {
        if (_engine is null)
            return;

        _engine.TransitionTo(state);
        if (_timer is not null)
            _timer.Interval = IntervalFor(_engine.FramesPerSecond);
    }

    private void OnFrameChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_engine is null)
            return;

        if (_renderedState != _engine.CurrentState)
        {
            _renderedState = _engine.CurrentState;
            StateLabel = _engine.CurrentState.ToString();
            AvatarBrush = BrushFor(_engine.CurrentState);
        }

        var frameSet = _engine.CurrentFrameSet;
        FrameLabel = frameSet is { Frames.Count: > 0 }
            ? $"frame {_engine.CurrentFrameIndex + 1}/{frameSet.Frames.Count}"
            : "—";

        // A subtle per-frame pulse so the placeholder visibly animates.
        AvatarDiameter = 108 + (10 * _engine.CurrentFrameIndex);
    }

    private static TimeSpan IntervalFor(int framesPerSecond)
        => TimeSpan.FromSeconds(1.0 / Math.Max(1, framesPerSecond));

    private static IBrush BrushFor(AgentState state) => state switch
    {
        AgentState.Idle => new SolidColorBrush(Color.Parse("#2AA198")),
        AgentState.Listening => new SolidColorBrush(Color.Parse("#859900")),
        AgentState.Working => new SolidColorBrush(Color.Parse("#B58900")),
        AgentState.Error => new SolidColorBrush(Color.Parse("#DC322F")),
        _ => new SolidColorBrush(Color.Parse("#2AA198")),
    };

    public void Dispose()
    {
        _timer?.Stop();
        if (_engine is not null)
            _engine.FrameChanged -= OnFrameChanged;
    }
}
