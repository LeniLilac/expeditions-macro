using System.ComponentModel;
using System.Runtime.CompilerServices;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public sealed class PlacementStepRow : INotifyPropertyChanged
{
    public static IReadOnlyList<UnitTargetingPriority>
        TargetingPriorities
    { get; } =
        Enum.GetValues<UnitTargetingPriority>();

    private int _unitKey;
    private int _x;
    private int _y;
    private int _delayAfterMilliseconds;
    private int _delayAfterStartMilliseconds;
    private PlacementPhase _phase;
    private UnitTargetingPriority _targetingPriority;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int UnitKey
    {
        get => _unitKey;
        set
        {
            if (!Set(ref _unitKey, value)) return;
            Raise(nameof(MarkerLabel));
        }
    }
    public int X { get => _x; set => Set(ref _x, value); }
    public int Y { get => _y; set => Set(ref _y, value); }
    public int DelayAfterMilliseconds { get => _delayAfterMilliseconds; set => Set(ref _delayAfterMilliseconds, value); }
    public int DelayAfterStartMilliseconds
    {
        get => _delayAfterStartMilliseconds;
        set
        {
            if (!Set(ref _delayAfterStartMilliseconds, value)) return;
            Raise(nameof(AfterStartDelayLabel));
            Raise(nameof(DelayAfterStartSeconds));
        }
    }
    public double DelayAfterStartSeconds
    {
        get => DelayAfterStartMilliseconds / 1000d;
        set => DelayAfterStartMilliseconds =
            Math.Max(
                0,
                (int)Math.Round(
                    value * 1000,
                    MidpointRounding.AwayFromZero));
    }
    public PlacementPhase Phase
    {
        get => _phase;
        set
        {
            if (!Set(ref _phase, value)) return;
            Raise(nameof(MarkerLabel));
            Raise(nameof(PhaseLabel));
            Raise(nameof(PhaseShortLabel));
            Raise(nameof(AfterStartDelayLabel));
        }
    }

    public UnitTargetingPriority TargetingPriority
    {
        get => _targetingPriority;
        set
        {
            if (!Set(ref _targetingPriority, value)) return;
            Raise(nameof(TargetingPriorityLabel));
        }
    }

    public string PhaseLabel =>
        Phase == PlacementPhase.BeforeStart
            ? "Before Start"
            : "After Start";

    public string MarkerLabel =>
        UnitKey.ToString();

    public string PhaseShortLabel =>
        Phase == PlacementPhase.BeforeStart ? "B" : "A";

    public string AfterStartDelayLabel =>
        Phase == PlacementPhase.BeforeStart
            ? "Before Start"
            : $"{DelayAfterStartMilliseconds / 1000d:0.###}s after Start";

    public string TargetingPriorityLabel =>
        TargetingPriority.ToString();

    public PlacementStep ToModel() => new()
    {
        UnitKey = UnitKey,
        X = X,
        Y = Y,
        DelayAfterMilliseconds = DelayAfterMilliseconds,
        Phase = Phase,
        DelayAfterStartMilliseconds =
            DelayAfterStartMilliseconds,
        TargetingPriority = TargetingPriority,
    };

    public static PlacementStepRow FromModel(PlacementStep step) => new()
    {
        UnitKey = step.UnitKey,
        X = step.X,
        Y = step.Y,
        DelayAfterMilliseconds = step.DelayAfterMilliseconds,
        Phase = step.Phase,
        DelayAfterStartMilliseconds =
            step.DelayAfterStartMilliseconds,
        TargetingPriority = step.TargetingPriority,
    };

    private bool Set<T>(
        ref T field,
        T value,
        [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(name));
}
