using System.ComponentModel;
using System.Runtime.CompilerServices;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public sealed class PlacementStepRow : INotifyPropertyChanged
{
    public static IReadOnlyList<
        PlacementEditorChoice<MatchStepKind>>
        Kinds
    { get; } =
    [
        new(MatchStepKind.Placement, "Place unit"),
        new(
            MatchStepKind.ReconfigureUnit,
            "Reconfigure unit"),
        new(MatchStepKind.Delay, "Delay"),
        new(MatchStepKind.UpgradeUnit, "Upgrade unit"),
        new(MatchStepKind.SellUnit, "Sell unit"),
    ];

    public static IReadOnlyList<int> UnitSlots
    { get; } = Enumerable.Range(1, 6).ToArray();

    public static IReadOnlyList<UnitTargetingPriority>
        TargetingPriorities
    { get; } =
        Enum.GetValues<UnitTargetingPriority>();

    public static IReadOnlyList<
        PlacementEditorChoice<UnitAutoUpgradePriority>>
        AutoUpgradePriorities
    { get; } =
    [
        new(UnitAutoUpgradePriority.Off, "Off"),
        new(UnitAutoUpgradePriority.Priority1, "Priority 1"),
        new(UnitAutoUpgradePriority.Priority2, "Priority 2"),
        new(UnitAutoUpgradePriority.Priority3, "Priority 3"),
        new(UnitAutoUpgradePriority.Priority4, "Priority 4"),
        new(UnitAutoUpgradePriority.Priority5, "Priority 5"),
        new(UnitAutoUpgradePriority.Priority6, "Priority 6"),
    ];

    public static IReadOnlyList<
        PlacementEditorChoice<MatchAutoUpgradeAction>>
        AutoUpgradeActions
    { get; } =
    [
        new(
            MatchAutoUpgradeAction.NoChange,
            "No change"),
        new(
            MatchAutoUpgradeAction.Disable,
            "Disable"),
        new(
            MatchAutoUpgradeAction.Priority1,
            "Enable · Priority 1"),
        new(
            MatchAutoUpgradeAction.Priority2,
            "Enable · Priority 2"),
        new(
            MatchAutoUpgradeAction.Priority3,
            "Enable · Priority 3"),
        new(
            MatchAutoUpgradeAction.Priority4,
            "Enable · Priority 4"),
        new(
            MatchAutoUpgradeAction.Priority5,
            "Enable · Priority 5"),
        new(
            MatchAutoUpgradeAction.Priority6,
            "Enable · Priority 6"),
    ];

    private int _unitKey;
    private int _x;
    private int _y;
    private string _placementId = string.Empty;
    private string _targetPlacementId = string.Empty;
    private string _displayUnitId = string.Empty;
    private MatchStepKind _kind;
    private int _delayAfterMilliseconds;
    private int _delayAfterStartMilliseconds;
    private int _delayDurationMilliseconds;
    private int _upgradeCount;
    private bool _changeTargetingPriority;
    private MatchAutoUpgradeAction _autoUpgradeAction;
    private PlacementPhase _phase;
    private UnitTargetingPriority _targetingPriority;
    private UnitAutoUpgradePriority
        _autoUpgradePriority =
            PlacementAuthoringRules.DefaultAutoUpgradePriority;
    private PlacementMarkerPresentation
        _markerLayout =
            PlacementMarkerPresentation.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int UnitKey
    {
        get => _unitKey;
        set
        {
            if (!Set(ref _unitKey, value)) return;
            Raise(nameof(MarkerLabel));
            RaiseStepLabels();
        }
    }

    public string PlacementId
    {
        get => _placementId;
        set => Set(
            ref _placementId,
            value ?? string.Empty);
    }

    public string TargetPlacementId
    {
        get => _targetPlacementId;
        set => Set(
            ref _targetPlacementId,
            value ?? string.Empty);
    }
    public int X
    {
        get => _x;
        set
        {
            if (!Set(ref _x, value)) return;
            Raise(nameof(CoordinateLabel));
        }
    }
    public int Y
    {
        get => _y;
        set
        {
            if (!Set(ref _y, value)) return;
            Raise(nameof(CoordinateLabel));
        }
    }
    public MatchStepKind Kind
    {
        get => _kind;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value));
            }
            if (!Set(ref _kind, value)) return;
            Raise(nameof(HasCoordinate));
            Raise(nameof(IsStartGame));
            Raise(nameof(CanEdit));
            Raise(nameof(CanRemove));
            Raise(nameof(MarkerLabel));
            RaiseStepLabels();
        }
    }
    public int DelayAfterMilliseconds { get => _delayAfterMilliseconds; set => Set(ref _delayAfterMilliseconds, value); }
    public int DelayAfterStartMilliseconds
    {
        get => _delayAfterStartMilliseconds;
        set
        {
            if (!Set(ref _delayAfterStartMilliseconds, value)) return;
            Raise(nameof(ScheduleLabel));
        }
    }
    public PlacementPhase Phase
    {
        get => _phase;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value));
            }
            if (!Set(ref _phase, value)) return;
            Raise(nameof(MarkerLabel));
            Raise(nameof(ScheduleLabel));
        }
    }

    public UnitTargetingPriority TargetingPriority
    {
        get => _targetingPriority;
        set
        {
            if (!Set(ref _targetingPriority, value)) return;
            Raise(nameof(TargetingPriorityLabel));
            RaiseActionSummaryLabels();
        }
    }

    public UnitAutoUpgradePriority
        AutoUpgradePriority
    {
        get => _autoUpgradePriority;
        set => Set(
            ref _autoUpgradePriority,
            value,
            propertyChanged: () =>
            {
                Raise(nameof(AutoUpgradePriorityLabel));
                RaiseActionSummaryLabels();
            });
    }

    public bool ChangeTargetingPriority
    {
        get => _changeTargetingPriority;
        set
        {
            if (!Set(
                    ref _changeTargetingPriority,
                    value))
            {
                return;
            }
            RaiseActionSummaryLabels();
        }
    }

    public MatchAutoUpgradeAction AutoUpgradeAction
    {
        get => _autoUpgradeAction;
        set
        {
            if (!Set(ref _autoUpgradeAction, value))
            {
                return;
            }
            RaiseActionSummaryLabels();
        }
    }

    public int DelayDurationMilliseconds
    {
        get => _delayDurationMilliseconds;
        set
        {
            if (!Set(
                    ref _delayDurationMilliseconds,
                    value))
            {
                return;
            }
            RaiseActionSummaryLabels();
        }
    }

    public int UpgradeCount
    {
        get => _upgradeCount;
        set
        {
            if (!Set(ref _upgradeCount, value)) return;
            RaiseActionSummaryLabels();
        }
    }

    public string MarkerLabel =>
        Kind switch
        {
            MatchStepKind.StartGame =>
                string.Empty,
            _ => DisplayUnitId,
        };

    public string TargetingPriorityLabel =>
        TargetingPriority.ToString();

    public string AutoUpgradePriorityLabel =>
        AutoUpgradePriority ==
            UnitAutoUpgradePriority.Off
                ? "Off"
                : $"Priority {(int)AutoUpgradePriority}";

    public bool HasCoordinate =>
        Kind == MatchStepKind.Placement;

    public bool HasPlacementReference =>
        Kind is MatchStepKind.ReconfigureUnit or
            MatchStepKind.UpgradeUnit or
            MatchStepKind.SellUnit;

    public bool IsStartGame =>
        Kind == MatchStepKind.StartGame;

    public bool CanEdit => !IsStartGame;

    public bool CanRemove => !IsStartGame;

    public string StepTypeLabel =>
        Kind switch
        {
            MatchStepKind.Placement => "Place unit",
            MatchStepKind.ReconfigureUnit =>
                "Reconfigure unit",
            MatchStepKind.Delay => "Wait",
            MatchStepKind.UpgradeUnit =>
                "Upgrade unit",
            MatchStepKind.SellUnit => "Sell unit",
            MatchStepKind.StartGame =>
                "Start Game",
            _ => Kind.ToString(),
        };

    public string StepTitle =>
        Kind switch
        {
            MatchStepKind.Delay => "Delay",
            MatchStepKind.StartGame => "Start Game",
            _ => $"{StepTypeLabel} {DisplayUnitId}",
        };

    public string CoordinateLabel =>
        IsStartGame
            ? string.Empty
            : HasCoordinate
                ? $"{X}, {Y}"
                : HasPlacementReference
                    ? $"Placed unit {DisplayUnitId}"
                    : "Timed action";

    public string ScheduleLabel =>
        IsStartGame
            ? string.Empty
            : ActionSummaryLabel;

    public string ActionSummaryLabel =>
        Kind switch
        {
            MatchStepKind.Placement =>
                $"Target {TargetingPriorityLabel} · Auto {AutoUpgradePriorityLabel}",
            MatchStepKind.ReconfigureUnit =>
                ReconfigureSummary(),
            MatchStepKind.Delay =>
                $"Wait {DelayDurationMilliseconds:N0} ms",
            MatchStepKind.UpgradeUnit =>
                $"Press Upgrade Unit {UpgradeCount}×",
            MatchStepKind.SellUnit => "Press Sell Unit",
            MatchStepKind.StartGame =>
                string.Empty,
            _ => string.Empty,
        };

    public string DisplayUnitId =>
        string.IsNullOrWhiteSpace(
            _displayUnitId)
            ? UnitKey.ToString()
            : _displayUnitId;

    public PlacementMarkerPresentation MarkerLayout =>
        _markerLayout;

    public void SetMarkerLayout(
        PlacementMarkerPresentation layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (_markerLayout == layout)
        {
            return;
        }

        _markerLayout = layout;
        Raise(nameof(MarkerLayout));
    }

    public void SetDisplayUnitId(string value)
    {
        value ??= string.Empty;
        if (!Set(
                ref _displayUnitId,
                value,
                name: nameof(DisplayUnitId)))
        {
            return;
        }
        Raise(nameof(MarkerLabel));
        Raise(nameof(StepTitle));
        Raise(nameof(CoordinateLabel));
    }

    public PlacementStep ToModel() => new()
    {
        Kind = Kind,
        PlacementId = PlacementId,
        TargetPlacementId =
            TargetPlacementId,
        UnitKey = UnitKey,
        X = X,
        Y = Y,
        DelayAfterMilliseconds = DelayAfterMilliseconds,
        Phase = Phase,
        DelayAfterStartMilliseconds =
            DelayAfterStartMilliseconds,
        TargetingPriority = TargetingPriority,
        AutoUpgradePriority =
            AutoUpgradePriority,
        ChangeTargetingPriority =
            ChangeTargetingPriority,
        AutoUpgradeAction =
            AutoUpgradeAction,
        DelayDurationMilliseconds =
            DelayDurationMilliseconds,
        UpgradeCount = UpgradeCount,
    };

    public static PlacementStepRow FromModel(PlacementStep step) => new()
    {
        Kind = step.Kind,
        PlacementId = step.PlacementId,
        TargetPlacementId =
            step.TargetPlacementId,
        UnitKey = step.UnitKey,
        X = step.X,
        Y = step.Y,
        DelayAfterMilliseconds = step.DelayAfterMilliseconds,
        Phase = step.Phase,
        DelayAfterStartMilliseconds =
            step.DelayAfterStartMilliseconds,
        TargetingPriority = step.TargetingPriority,
        AutoUpgradePriority =
            step.AutoUpgradePriority,
        ChangeTargetingPriority =
            step.ChangeTargetingPriority,
        AutoUpgradeAction =
            step.AutoUpgradeAction,
        DelayDurationMilliseconds =
            step.DelayDurationMilliseconds,
        UpgradeCount = step.UpgradeCount,
    };

    private bool Set<T>(
        ref T field,
        T value,
        Action? propertyChanged = null,
        [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        propertyChanged?.Invoke();
        return true;
    }

    private string ReconfigureSummary()
    {
        List<string> changes = [];
        if (ChangeTargetingPriority)
        {
            changes.Add(
                $"Target {TargetingPriorityLabel}");
        }
        if (AutoUpgradeAction !=
            MatchAutoUpgradeAction.NoChange)
        {
            changes.Add(
                AutoUpgradeAction ==
                    MatchAutoUpgradeAction.Disable
                        ? "Disable Auto Upgrade"
                        : $"Auto Priority {(int)AutoUpgradeAction - 1}");
        }
        return string.Join(" · ", changes);
    }

    private void RaiseStepLabels()
    {
        Raise(nameof(StepTypeLabel));
        Raise(nameof(StepTitle));
        Raise(nameof(CoordinateLabel));
        RaiseActionSummaryLabels();
    }

    private void RaiseActionSummaryLabels()
    {
        Raise(nameof(ActionSummaryLabel));
        Raise(nameof(ScheduleLabel));
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(name));
}
