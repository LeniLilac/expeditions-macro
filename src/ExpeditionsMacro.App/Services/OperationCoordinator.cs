using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using ExpeditionsMacro.Automation.Diagnostics;

namespace ExpeditionsMacro.App.Services;

public enum OperationState
{
    Idle,
    Armed,
    Running,
    Stopping,
}

public sealed class OperationCoordinator : INotifyPropertyChanged
{
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private Func<CancellationToken, Task>? _armedAction;
    private string? _armedDescription;
    private DeepDebugOperationContext? _armedDebugContext;
    private CancellationTokenSource? _cancellation;
    private OperationState _state;
    private string _description = "Ready";

    public OperationCoordinator(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? StateChanged;

    public event EventHandler<Exception>? OperationFailed;

    public Func<Task>? DefaultIdleHotkeyAction { get; set; }

    public Func<string, DeepDebugOperationContext?, Func<CancellationToken, Task>, CancellationToken, Task>? OperationRunner { get; set; }

    public string HotkeyDisplayName { get; set; } = "F6";

    public OperationState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Description
    {
        get => _description;
        private set
        {
            if (_description == value) return;
            _description = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy => State is not OperationState.Idle;

    public void Arm(
        string description,
        Func<CancellationToken, Task> action,
        DeepDebugOperationContext? debugContext = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            if (State != OperationState.Idle) throw new InvalidOperationException("Another workflow already owns Roblox input.");
            _armedAction = action;
            _armedDescription = description;
            _armedDebugContext = debugContext;
            Description = $"{description}: press {HotkeyDisplayName} to begin";
            State = OperationState.Armed;
        }
    }

    public Task RunNowAsync(
        string description,
        Func<CancellationToken, Task> action,
        DeepDebugOperationContext? debugContext = null)
    {
        lock (_gate)
        {
            if (State != OperationState.Idle) throw new InvalidOperationException("Another workflow already owns Roblox input.");
            _armedAction = action;
            _armedDescription = description;
            _armedDebugContext = debugContext;
        }
        return BeginAsync(description);
    }

    public void Cancel()
    {
        lock (_gate)
        {
            if (State == OperationState.Armed)
            {
                _armedAction = null;
                _armedDescription = null;
                _armedDebugContext = null;
                Description = "Ready";
                State = OperationState.Idle;
                return;
            }
            if (State != OperationState.Running) return;
            State = OperationState.Stopping;
            Description = "Stopping and restoring Roblox";
            _cancellation?.Cancel();
        }
    }

    public void HandleHotkey()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(HandleHotkey);
            return;
        }
        if (State == OperationState.Armed)
        {
            _ = BeginAsync(_armedDescription ?? Description);
        }
        else if (State is OperationState.Running)
        {
            Cancel();
        }
        else if (State == OperationState.Idle && DefaultIdleHotkeyAction is not null)
        {
            _ = DefaultIdleHotkeyAction();
        }
    }

    private async Task BeginAsync(string description)
    {
        Func<CancellationToken, Task> action;
        DeepDebugOperationContext? debugContext;
        lock (_gate)
        {
            action = _armedAction ?? throw new InvalidOperationException("No operation is armed.");
            debugContext = _armedDebugContext;
            _armedAction = null;
            _armedDescription = null;
            _armedDebugContext = null;
            _cancellation = new CancellationTokenSource();
            Description = description;
            State = OperationState.Running;
        }
        try
        {
            if (OperationRunner is null)
            {
                await action(_cancellation.Token);
            }
            else
            {
                await OperationRunner(description, debugContext, action, _cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // User-requested cancellation is an expected terminal state.
        }
        catch (Exception error)
        {
            OperationFailed?.Invoke(this, error);
        }
        finally
        {
            lock (_gate)
            {
                _cancellation.Dispose();
                _cancellation = null;
                Description = "Ready";
                State = OperationState.Idle;
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
