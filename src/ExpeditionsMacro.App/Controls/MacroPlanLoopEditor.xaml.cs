using System.Collections;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class MacroPlanLoopEditor :
    UserControl
{
    private MacroPlanLoopProgress _progress = new();
    private bool _interactionEnabled = true;
    private bool _updating;

    public MacroPlanLoopEditor()
    {
        InitializeComponent();
    }

    public event EventHandler? ValueChanged;

    public string? StartTaskId =>
        SelectedTaskId(LoopStartCombo.SelectedItem);

    public string? StopTaskId =>
        SelectedTaskId(LoopStopCombo.SelectedItem);

    public void SetTasks(IEnumerable tasks)
    {
        LoopStartCombo.ItemsSource = tasks;
        LoopStopCombo.ItemsSource = tasks;
    }

    public MacroPlanLoopDefinition? ReadDefinition(
        IReadOnlyList<MacroTaskDefinition> tasks)
    {
        if (LoopEnabledCheck.IsChecked != true)
        {
            return null;
        }
        MacroTaskRow start =
            LoopStartCombo.SelectedItem as
                MacroTaskRow ??
            throw new InvalidDataException(
                "Choose the first task in the loop.");
        MacroTaskRow stop =
            LoopStopCombo.SelectedItem as
                MacroTaskRow ??
            throw new InvalidDataException(
                "Choose the last task in the loop.");
        bool forever =
            LoopForeverCheck.IsChecked == true;
        MacroPlanLoopDefinition loop = new()
        {
            StartTaskId = start.Definition.Id,
            StopTaskId = stop.Definition.Id,
            TotalRuns = forever
                ? 1
                : ParseAmount(),
            Forever = forever,
        };
        loop.Validate(tasks);
        return loop;
    }

    public MacroPlanLoopProgress ProgressFor(
        MacroPlanLoopDefinition? loop)
    {
        if (loop is null)
        {
            return new();
        }
        return string.Equals(
                _progress.ConfigurationSignature,
                loop.ConfigurationSignature,
                StringComparison.Ordinal)
            ? _progress
            : new();
    }

    public void Apply(
        MacroPlanLoopDefinition? loop,
        MacroPlanLoopProgress progress)
    {
        _updating = true;
        try
        {
            _progress = progress;
            LoopEnabledCheck.IsChecked =
                loop is not null;
            LoopForeverCheck.IsChecked =
                loop?.Forever == true;
            LoopAmountText.Text =
                (loop?.TotalRuns ?? 2).ToString(
                    CultureInfo.InvariantCulture);
            LoopStartCombo.SelectedItem =
                FindRow(loop?.StartTaskId);
            LoopStopCombo.SelectedItem =
                FindRow(loop?.StopTaskId);
        }
        finally
        {
            _updating = false;
        }
        UpdateState();
    }

    public void UpdateProgress(
        MacroPlanLoopProgress progress)
    {
        _progress = progress;
        UpdateStatus();
    }

    public void RestoreSelections(
        string? startTaskId,
        string? stopTaskId)
    {
        _updating = true;
        try
        {
            LoopStartCombo.SelectedItem =
                FindRow(startTaskId) ??
                FirstRow();
            LoopStopCombo.SelectedItem =
                FindRow(stopTaskId) ??
                LastRow();
        }
        finally
        {
            _updating = false;
        }
        UpdateStatus();
    }

    public void SetInteractionEnabled(
        bool enabled)
    {
        _interactionEnabled = enabled;
        UpdateState();
    }

    private void Control_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_updating ||
            LoopAmountText is null)
        {
            return;
        }
        if (LoopEnabledCheck.IsChecked == true)
        {
            LoopStartCombo.SelectedItem ??=
                FirstRow();
            LoopStopCombo.SelectedItem ??=
                LastRow();
        }
        UpdateState();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Selection_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updating ||
            LoopStatusText is null)
        {
            return;
        }
        UpdateStatus();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Amount_Changed(
        object sender,
        TextChangedEventArgs e)
    {
        if (_updating ||
            LoopStatusText is null)
        {
            return;
        }
        UpdateStatus();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateState()
    {
        bool enabled =
            _interactionEnabled &&
            LoopEnabledCheck.IsChecked == true;
        LoopEnabledCheck.IsEnabled =
            _interactionEnabled;
        LoopStartCombo.IsEnabled = enabled;
        LoopStopCombo.IsEnabled = enabled;
        LoopForeverCheck.IsEnabled = enabled;
        LoopAmountText.IsEnabled =
            enabled &&
            LoopForeverCheck.IsChecked != true;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (LoopEnabledCheck.IsChecked != true)
        {
            LoopStatusText.Text = string.Empty;
            return;
        }
        if (LoopStartCombo.SelectedItem is not
                MacroTaskRow start ||
            LoopStopCombo.SelectedItem is not
                MacroTaskRow stop)
        {
            LoopStatusText.Text =
                "Choose loop start and stop tasks.";
            return;
        }

        bool forever =
            LoopForeverCheck.IsChecked == true;
        int amount = forever
            ? 1
            : ParseAmountOrDefault();
        string signature =
            new MacroPlanLoopDefinition
            {
                StartTaskId =
                    start.Definition.Id,
                StopTaskId =
                    stop.Definition.Id,
                TotalRuns = amount,
                Forever = forever,
            }.ConfigurationSignature;
        long completed = string.Equals(
                _progress.ConfigurationSignature,
                signature,
                StringComparison.Ordinal)
            ? _progress.CompletedRuns
            : 0;
        LoopStatusText.Text = forever
            ? $"{completed} loop run{Plural(completed)} completed; runs continue until stopped."
            : $"{completed} of {amount} loop runs completed.";
    }

    private int ParseAmount()
    {
        if (!int.TryParse(
                LoopAmountText.Text.Trim(),
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

    private int ParseAmountOrDefault() =>
        int.TryParse(
            LoopAmountText.Text.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int amount) &&
        amount is >= 1 and <= 100000
            ? amount
            : 2;

    private MacroTaskRow? FindRow(string? id) =>
        id is null
            ? null
            : LoopStartCombo.Items
                .OfType<MacroTaskRow>()
                .FirstOrDefault(row =>
                    string.Equals(
                        row.Definition.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase));

    private MacroTaskRow? FirstRow() =>
        LoopStartCombo.Items
            .OfType<MacroTaskRow>()
            .FirstOrDefault();

    private MacroTaskRow? LastRow() =>
        LoopStopCombo.Items
            .OfType<MacroTaskRow>()
            .LastOrDefault();

    private static string? SelectedTaskId(
        object? selected) =>
        (selected as MacroTaskRow)?
            .Definition.Id;

    private static string Plural(long count) =>
        count == 1
            ? string.Empty
            : "s";
}
