using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public abstract class MacroPlanBlockNode :
    INotifyPropertyChanged
{
    private int _depth;

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public string EditorId { get; } =
        Guid.NewGuid().ToString("N");

    public virtual bool IsLoop => false;

    public int Depth
    {
        get => _depth;
        set => Set(ref _depth, value);
    }

    protected bool Set<T>(
        ref T field,
        T value,
        [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(
                field,
                value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged(
        [CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(name));
}

public sealed class MacroPlanTaskBlockNode :
    MacroPlanBlockNode
{
    private int _order;
    private MacroTaskRow _taskRow;

    public MacroPlanTaskBlockNode(
        MacroTaskRow taskRow)
    {
        _taskRow = taskRow;
    }

    public MacroTaskRow TaskRow
    {
        get => _taskRow;
        set
        {
            if (ReferenceEquals(_taskRow, value))
            {
                return;
            }
            _taskRow = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(Target));
            OnPropertyChanged(nameof(Status));
        }
    }

    public int Order
    {
        get => _order;
        set => Set(ref _order, value);
    }

    public string Name => TaskRow.Name;

    public string Type => TaskRow.Type;

    public string Target => TaskRow.Target;

    public string Status => TaskRow.Status;
}

public sealed class MacroPlanLoopBlockNode :
    MacroPlanBlockNode
{
    private string _amountText = "2";
    private string _blockLabel = "Loop";
    private bool _forever;
    private string _membershipText =
        "No blocks yet";
    private string _statusText = string.Empty;

    public event EventHandler? ValueChanged;

    public MacroPlanLoopBlockNode()
    {
        Children.CollectionChanged +=
            (_, _) => OnPropertyChanged(
                nameof(EmptyDropVisibility));
    }

    public ObservableCollection<MacroPlanBlockNode>
        Children
    { get; } = [];

    public override bool IsLoop => true;

    public Visibility EmptyDropVisibility =>
        Children.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (Set(ref _amountText, value))
            {
                ValueChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            }
        }
    }

    public bool Forever
    {
        get => _forever;
        set
        {
            if (!Set(ref _forever, value))
            {
                return;
            }
            OnPropertyChanged(
                nameof(FiniteControlsVisibility));
            ValueChanged?.Invoke(
                this,
                EventArgs.Empty);
        }
    }

    public Visibility FiniteControlsVisibility =>
        Forever
            ? Visibility.Collapsed
            : Visibility.Visible;

    public string BlockLabel
    {
        get => _blockLabel;
        set => Set(ref _blockLabel, value);
    }

    public string MembershipText
    {
        get => _membershipText;
        set => Set(ref _membershipText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    public MacroPlanLoopProgress Progress { get; set; } =
        new();

    public int ReadTotalRuns()
    {
        if (Forever)
        {
            return 1;
        }
        if (!int.TryParse(
                AmountText.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int amount) ||
            amount is < 1 or > 100000)
        {
            throw new InvalidDataException(
                "Loop amount must be 1 through 100000.");
        }
        return amount;
    }
}
