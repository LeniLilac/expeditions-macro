using System.ComponentModel;
using System.Runtime.CompilerServices;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public sealed class PlacementStepRow : INotifyPropertyChanged
{
    private int _unitKey;
    private int _x;
    private int _y;
    private int _delayAfterMilliseconds;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int UnitKey { get => _unitKey; set => Set(ref _unitKey, value); }
    public int X { get => _x; set => Set(ref _x, value); }
    public int Y { get => _y; set => Set(ref _y, value); }
    public int DelayAfterMilliseconds { get => _delayAfterMilliseconds; set => Set(ref _delayAfterMilliseconds, value); }

    public PlacementStep ToModel() => new() { UnitKey = UnitKey, X = X, Y = Y, DelayAfterMilliseconds = DelayAfterMilliseconds };

    public static PlacementStepRow FromModel(PlacementStep step) => new()
    {
        UnitKey = step.UnitKey,
        X = step.X,
        Y = step.Y,
        DelayAfterMilliseconds = step.DelayAfterMilliseconds,
    };

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
