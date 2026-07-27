using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public sealed class MacroPlanTaskRequestedEventArgs(
    MacroTaskRow task) : EventArgs
{
    public MacroTaskRow Task { get; } = task;
}

public sealed class MacroPlanLoopRequestedEventArgs(
    MacroPlanLoopBlockNode loop) : EventArgs
{
    public MacroPlanLoopBlockNode Loop { get; } = loop;
}

public partial class MacroPlanLoopEditor :
    UserControl
{
    private bool _interactionEnabled = true;
    private bool _updating;

    public MacroPlanLoopEditor()
    {
        InitializeComponent();
        InitializeLoopSettings();
        StructureTree.ItemsSource = RootBlocks;
        RootDropZone.Visibility =
            Visibility.Collapsed;
        UpdateState();
    }

    public event EventHandler? ValueChanged;

    public event EventHandler<
        MacroPlanTaskRequestedEventArgs>?
        EditTaskRequested;

    public event EventHandler<
        MacroPlanTaskRequestedEventArgs>?
        RemoveTaskRequested;

    public event EventHandler<
        MacroPlanLoopRequestedEventArgs>?
        AddTaskRequested;

    public ObservableCollection<MacroPlanBlockNode>
        RootBlocks
    { get; } = [];

    public IReadOnlyList<MacroTaskRow>
        OrderedTaskRows =>
        MacroPlanStructure.FlattenTasks(RootBlocks)
            .Select(node => node.TaskRow)
            .ToArray();

    public void SetTasks(IEnumerable tasks)
    {
        MacroPlanStructure.SynchronizeTasks(
            RootBlocks,
            tasks.OfType<MacroTaskRow>()
                .ToArray());
        UpdateState();
    }

    public IReadOnlyList<MacroPlanLoopDefinition>
        ReadDefinitions(
        IReadOnlyList<MacroTaskDefinition> tasks) =>
        MacroPlanStructure.ReadDefinitions(
            RootBlocks,
            tasks);

    public IReadOnlyList<MacroPlanLoopProgress>
        ProgressFor(
        IReadOnlyList<MacroPlanLoopDefinition> loops)
    {
        IReadOnlyList<MacroPlanLoopBlockNode> nodes =
            MacroPlanStructure.FlattenLoops(
                RootBlocks);
        IReadOnlyList<MacroPlanLoopDefinition> current =
            MacroPlanStructure.ReadDefinitions(
                RootBlocks,
                OrderedTaskRows.Select(row =>
                        row.Definition)
                    .ToArray());
        return loops.Select(loop =>
        {
            int index = current
                .Select((value, valueIndex) =>
                    new
                    {
                        Value = value,
                        Index = valueIndex,
                    })
                .First(value =>
                    string.Equals(
                        value.Value
                            .ConfigurationSignature,
                        loop.ConfigurationSignature,
                        StringComparison.Ordinal))
                .Index;
            MacroPlanLoopProgress progress =
                nodes[index].Progress;
            return string.Equals(
                    progress.ConfigurationSignature,
                    loop.ConfigurationSignature,
                    StringComparison.Ordinal)
                ? progress
                : new MacroPlanLoopProgress
                {
                    ConfigurationSignature =
                        loop.ConfigurationSignature,
                    Phase = MacroPlanLoopPhase.Loop,
                };
        }).ToArray();
    }

    public void Apply(
        IReadOnlyList<MacroPlanLoopDefinition> loops,
        IReadOnlyList<MacroPlanLoopProgress> progress)
    {
        MacroTaskRow[] taskRows =
            OrderedTaskRows.ToArray();
        _updating = true;
        try
        {
            UnsubscribeAll();
            RootBlocks.Clear();
            ObservableCollection<MacroPlanBlockNode>
                built = MacroPlanStructure.Build(
                    taskRows,
                    loops,
                    progress);
            foreach (MacroPlanBlockNode node in built)
            {
                RootBlocks.Add(node);
            }
            SubscribeAll();
        }
        finally
        {
            _updating = false;
        }
        UpdateState();
    }

    public void UpdateProgress(
        IReadOnlyList<MacroPlanLoopProgress> progress)
    {
        try
        {
            IReadOnlyList<MacroPlanLoopDefinition>
                definitions =
                    MacroPlanStructure.ReadDefinitions(
                        RootBlocks,
                        OrderedTaskRows.Select(row =>
                                row.Definition)
                            .ToArray());
            IReadOnlyList<MacroPlanLoopBlockNode> nodes =
                MacroPlanStructure.FlattenLoops(
                    RootBlocks);
            for (int index = 0;
                 index < nodes.Count;
                 index++)
            {
                nodes[index].Progress =
                    progress.FirstOrDefault(value =>
                        string.Equals(
                            value.ConfigurationSignature,
                            definitions[index]
                                .ConfigurationSignature,
                            StringComparison.Ordinal)) ??
                    new();
            }
        }
        catch (InvalidDataException)
        {
            // An empty user-authored loop has no persisted signature yet.
        }
        Refresh();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _interactionEnabled = enabled;
        UpdateState();
    }

    public void AddLoopBlock()
    {
        if (!_interactionEnabled)
        {
            return;
        }
        MacroPlanLoopBlockNode loop =
            MacroPlanStructure.AddLoopBlock(
                RootBlocks);
        Subscribe(loop);
        CompleteChange();
    }

    public IReadOnlyList<MacroPlanLoopBlockNode>
        LoopBlocks =>
        MacroPlanStructure.FlattenLoops(
            RootBlocks);

    public MacroPlanLoopBlockNode? ParentLoopFor(
        MacroTaskRow row)
    {
        MacroPlanTaskBlockNode? node =
            MacroPlanStructure.FlattenTasks(
                    RootBlocks)
                .FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.TaskRow
                            .Definition.Id,
                        row.Definition.Id,
                        StringComparison
                            .OrdinalIgnoreCase));
        if (node is null ||
            !MacroPlanStructureMove.TryFindOwner(
                RootBlocks,
                node,
                out _,
                out MacroPlanLoopBlockNode? parent))
        {
            return null;
        }
        return parent;
    }

    public bool TryPlaceTask(
        MacroTaskRow row,
        MacroPlanLoopBlockNode? destination,
        out string error)
    {
        MacroPlanTaskBlockNode? node =
            MacroPlanStructure.FlattenTasks(
                    RootBlocks)
                .FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.TaskRow
                            .Definition.Id,
                        row.Definition.Id,
                        StringComparison
                            .OrdinalIgnoreCase));
        if (node is null)
        {
            error =
                "The task could not be found in the plan.";
            return false;
        }
        MacroPlanLoopBlockNode? current =
            ParentLoopFor(row);
        if (ReferenceEquals(current, destination))
        {
            error = string.Empty;
            return true;
        }
        bool moved = destination is null
            ? MacroPlanStructureMove.TryMoveToRoot(
                RootBlocks,
                node,
                out error)
            : MacroPlanStructureMove.TryMoveInside(
                RootBlocks,
                node,
                destination,
                out error);
        if (moved)
        {
            CompleteChange();
        }
        return moved;
    }

    private void AddTaskToLoop_Click(
        object sender,
        RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is
            MacroPlanLoopBlockNode loop)
        {
            AddTaskRequested?.Invoke(
                this,
                new MacroPlanLoopRequestedEventArgs(
                    loop));
        }
    }

    private void RemoveLoop_Click(
        object sender,
        RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not
            MacroPlanLoopBlockNode loop)
        {
            return;
        }
        loop.ValueChanged -= Loop_ValueChanged;
        MacroPlanStructureMove
            .RemoveLoopAndPromoteChildren(
                RootBlocks,
                loop);
        CompleteChange();
    }

    private void EditTask_Click(
        object sender,
        RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is
            MacroTaskRow task)
        {
            EditTaskRequested?.Invoke(
                this,
                new MacroPlanTaskRequestedEventArgs(
                    task));
        }
    }

    private void RemoveTask_Click(
        object sender,
        RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is
            MacroTaskRow task)
        {
            RemoveTaskRequested?.Invoke(
                this,
                new MacroPlanTaskRequestedEventArgs(
                    task));
        }
    }

    private void Loop_ValueChanged(
        object? sender,
        EventArgs e)
    {
        if (_updating ||
            sender is not MacroPlanLoopBlockNode loop)
        {
            return;
        }
        if (loop.Forever)
        {
            _updating = true;
            try
            {
                MacroPlanStructureMove.MakeForever(
                    RootBlocks,
                    loop);
            }
            finally
            {
                _updating = false;
            }
        }
        CompleteChange();
    }

    private void CompleteChange()
    {
        Refresh();
        ValueChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void Refresh()
    {
        MacroPlanStructure.RefreshPresentation(
            RootBlocks);
        ShowValidation(string.Empty);
        UpdateState();
    }

    private void UpdateState()
    {
        StructureTree.Visibility =
            RootBlocks.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        StructureTree.IsEnabled =
            _interactionEnabled;
    }

    private void ShowValidation(string message)
    {
        LoopValidationText.Text = message;
        LoopValidationText.Visibility =
            string.IsNullOrWhiteSpace(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void SubscribeAll()
    {
        foreach (MacroPlanLoopBlockNode loop in
                 MacroPlanStructure.FlattenLoops(
                     RootBlocks))
        {
            Subscribe(loop);
        }
    }

    private void UnsubscribeAll()
    {
        foreach (MacroPlanLoopBlockNode loop in
                 MacroPlanStructure.FlattenLoops(
                     RootBlocks))
        {
            loop.ValueChanged -= Loop_ValueChanged;
        }
    }

    private void Subscribe(
        MacroPlanLoopBlockNode loop)
    {
        loop.ValueChanged -= Loop_ValueChanged;
        loop.ValueChanged += Loop_ValueChanged;
    }
}
