using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage : UserControl, IAppPage
{
    private static readonly TimeSpan SafeSkipDelay = TimeSpan.FromMinutes(5);

    private readonly AppServices _services;
    private readonly ObservableCollection<MacroPlan> _plans = [];
    private readonly ObservableCollection<PlacementSetupRoute>
        _visibleRoutes = [];
    private static readonly IReadOnlyList<
        NamedChoice<ResourceRefuelTarget>>
        UtilityRoutes =
        [
            new(
                ResourceRefuelTarget.GoldMine,
                "Gold Mine refuel"),
            new(
                ResourceRefuelTarget.ResourceDrill,
                "Resource Drill refuel"),
            new(
                ResourceRefuelTarget.Both,
                "Gold Mine + Resource Drill"),
        ];
    private readonly DispatcherTimer _runtimeTimer;
    private DateTimeOffset? _runStarted;
    private string? _editingTaskId;
    private bool _loading;
    private bool _macroOwned;
    private bool _syncingTaskKindButtons;
    private bool _testingWebhook;
    public MacroPage(AppServices services)
    {
        _services = services;
        _planAutoSave = new(
            PersistPlanAsync);
        _suppressPlanAutoSave = true;
        InitializeComponent();
        _suppressPlanAutoSave = false;
        InitializePlanAutoSave();
        InitializeTaskDialog();
        InitializeDashboard();
        DataContext = this;
        LoopEditor.SetTasks(TaskRows);
        LoopEditor.ValueChanged +=
            LoopEditor_ValueChanged;
        LoopEditor.EditTaskRequested +=
            (_, args) => BeginTaskEdit(args.Task);
        LoopEditor.RemoveTaskRequested +=
            (_, args) => RemoveTask(args.Task);
        LoopEditor.AddTaskRequested +=
            (_, args) =>
                OpenTaskEditor(args.Loop);
        PlanCombo.ItemsSource = _plans;
        TaskKindCombo.ItemsSource = Enum
            .GetValues<MacroTaskKind>()
            .Select(kind => new NamedChoice<MacroTaskKind>(kind, Label(kind)))
            .ToArray();
        TaskRouteCombo.ItemsSource = _visibleRoutes;
        TaskDifficultyCombo.ItemsSource = Enumerable
            .Range(1, 3)
            .Select(value =>
                new NamedChoice<int>(
                    value,
                    $"Difficulty {value}"))
            .ToArray();
        TaskDifficultyCombo.DisplayMemberPath =
            nameof(NamedChoice<int>.Name);
        TaskDifficultyCombo.SelectedIndex = 0;
        _runtimeTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateRuntime(), Dispatcher);
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(CoordinatorStateChanged);
        _services.Hotkey.BindingChanged += (_, _) => Dispatcher.BeginInvoke(UpdateHotkeyText);
    }

    public ObservableCollection<MacroTaskRow> TaskRows { get; } = [];

    public Func<Task>? IdleHotkeyAction => StartFromHotkeyAsync;

    public async Task OnShownAsync()
    {
        try
        {
            await _planAutoSave.FlushAsync();
        }
        catch (Exception error)
        {
            ShowPlanBlocksStatus(
                $"Could not save: {error.Message}");
            return;
        }
        await FlushPrivateServerRecoverySettingsAsync();
        _loading = true;
        try
        {
            await RefreshPlansAsync();
            MacroPlan? selected = _plans.FirstOrDefault(plan => plan.Id == _services.Settings.SelectedMacroPlanId) ?? _plans.FirstOrDefault();
            PlanCombo.SelectedItem = selected;
            if (selected is null) ApplyNewPlan();
            else ApplyPlan(selected);

            string webhook = string.Empty;
            try { webhook = _services.SecretProtector.Unprotect(_services.Settings.EncryptedWebhook); } catch { }
            WebhookPassword.Password = webhook;
            WebhookVisible.Text = webhook;
            DiscordUserIdText.Text = _services.Settings.DiscordErrorUserId;
            LoadPrivateServerRecoverySettings();
            TaskKindCombo.SelectedIndex = 0;
            RefreshVisibleRoutes();
            UpdateTaskTargetEditor();
        }
        finally
        {
            _loading = false;
        }
        UpdateHotkeyText();
        RefreshDashboardSettings();
        CoordinatorStateChanged();
    }

    internal void SetSnapshotScroll(
        bool showEnd,
        MacroPlanSnapshotState planState =
            MacroPlanSnapshotState.NestedLoops)
    {
        // Snapshot artifacts are uploaded by CI. Never render locally protected
        // reporting values into those images, even when the normal controls mask
        // part of a webhook on screen.
        WebhookPassword.Password = string.Empty;
        WebhookVisible.Text = string.Empty;
        DiscordUserIdText.Text = string.Empty;
        ShareCodeText.Text = string.Empty;
        SharePlanStatusText.Text = string.Empty;
        ClearPrivateServerRecoverySnapshot();
        WithoutPlanAutoSave(
            () => PopulateSnapshotTasks(
                planState));
        UpdateLayout();
        SetActiveWorkspaceSnapshotScroll(showEnd);
    }

    private async Task<IDetectorPack> LoadDetectorAsync(string id, CancellationToken cancellationToken) =>
        _services.TraceDetector(
            await _services.DetectorPacks.LoadAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Detector pack '{id}' could not be loaded."));

    private async Task HandleRecoverableFailureAsync(
        string macroName,
        string webhook,
        string discordUserId,
        Exception error,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Dispatcher.InvokeAsync(() => AppendLog($"RECOVERABLE: {error.Message}"));
        MacroFailureHandlingResult result =
            await _services.HandleRecoverableMacroFailureAsync(
                macroName,
                error).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await Dispatcher.InvokeAsync(() => AppendFailureHandlingResult(result));
    }

    private static ScheduledTaskResult ToScheduledResult(StageRunResult result) =>
        new(result.Victories, result.Defeats, result.Runtime);

    private void CoordinatorStateChanged()
    {
        bool busy = _services.Coordinator.IsBusy;
        if (busy &&
            TaskEditorOverlay.Visibility ==
                Visibility.Visible)
        {
            ResetTaskEditor();
        }
        StartButton.IsEnabled = !busy;
        StopButton.IsEnabled = busy;
        PlanCombo.IsEnabled = !busy;
        PlanNameText.IsEnabled = !busy;
        DeletePlanButton.IsEnabled =
            !busy &&
            !string.IsNullOrWhiteSpace(
                _persistedPlanId);
        TaskRowsControl.IsEnabled = !busy;
        TaskKindCombo.IsEnabled = !busy;
        TaskRouteCombo.IsEnabled = !busy;
        TaskDefeatRetriesText.IsEnabled = !busy;
        TaskTraitCheck.IsEnabled = !busy;
        TaskStatCheck.IsEnabled = !busy;
        TaskSpriteCheck.IsEnabled = !busy;
        TaskDifficultyCombo.IsEnabled = !busy;
        TaskExtractCheck.IsEnabled = !busy;
        TaskBossNodesText.IsEnabled = !busy;
        TaskHardModeCheck.IsEnabled = !busy;
        AddTaskButton.IsEnabled = !busy;
        CancelTaskEditButton.IsEnabled = !busy;
        OpenTaskEditorButton.IsEnabled = !busy;
        ResetProgressButton.IsEnabled = !busy;
        AddLoopBlockButton.IsEnabled = !busy;
        TaskEditorOverlay.IsEnabled = !busy;
        LoopEditor.SetInteractionEnabled(!busy);
        WebhookPassword.IsEnabled = !busy;
        WebhookVisible.IsEnabled = !busy;
        ShowWebhookCheck.IsEnabled = !busy;
        DiscordUserIdText.IsEnabled = !busy;
        TestWebhookButton.IsEnabled = !busy && !_testingWebhook;
        ShareCodeText.IsEnabled = !busy;
        ExportPlanCodeButton.IsEnabled = !busy;
        CopyPlanCodeButton.IsEnabled =
            !busy &&
            !string.IsNullOrWhiteSpace(
                ShareCodeText.Text);
        ImportPlanCodeButton.IsEnabled = !busy;
        SetPrivateServerRecoveryControlsEnabled(!busy);
        UpdateDashboardBusyState();
        UpdateTaskTargetEditor();

        if (!busy && _macroOwned)
        {
            _macroOwned = false;
            _runtimeTimer.Stop();
            CurrentTaskText.Text = "Current task: none";
            PhaseText.Text = "Plan stopped. Roblox remains at the standard client size.";
            AppendLog("Macro plan stopped.");
        }
    }

    private void AddLoopBlock_Click(
        object sender,
        RoutedEventArgs e) =>
        LoopEditor.AddLoopBlock();

    private void OpenTaskEditor_Click(
        object sender,
        RoutedEventArgs e) =>
        OpenTaskEditor(destination: null);

    private void UpdateHotkeyText()
    {
        string hotkey = _services.Hotkey.DisplayName;
        StartButton.Content = $"Start plan  {hotkey}";
        StopButton.Content = $"Stop plan  {hotkey}";
    }

    private void UpdateRuntime()
    {
        if (_runStarted is not null) RuntimeText.Text = (DateTimeOffset.Now - _runStarted.Value).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private void ShowWebhook_Changed(object sender, RoutedEventArgs e)
    {
        if (ShowWebhookCheck.IsChecked == true)
        {
            WebhookVisible.Text = WebhookPassword.Password;
            WebhookPassword.Visibility = Visibility.Collapsed;
            WebhookVisible.Visibility = Visibility.Visible;
        }
        else
        {
            WebhookPassword.Password = WebhookVisible.Text;
            WebhookVisible.Visibility = Visibility.Collapsed;
            WebhookPassword.Visibility = Visibility.Visible;
        }
    }

    private async void TestWebhook_Click(object sender, RoutedEventArgs e)
    {
        string webhook = CurrentWebhook();
        string discordUserId = DiscordUserIdText.Text.Trim();
        WebhookStatusText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(webhook))
        {
            WebhookStatusText.Text = "Enter a webhook first.";
            return;
        }
        if (!DiscordWebhookClient.ValidateWebhookUrl(webhook))
        {
            WebhookStatusText.Text = "Enter a valid Discord webhook URL.";
            return;
        }
        try
        {
            ValidateDiscord(webhook, discordUserId);
        }
        catch (Exception error)
        {
            WebhookStatusText.Text = error.Message;
            return;
        }

        _testingWebhook = true;
        TestWebhookButton.IsEnabled = false;
        TestWebhookButton.Content = "Sending...";
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
            await _services.TestDiscordWebhookAsync(webhook, timeout.Token);
            await SaveReportingSettingsAsync(webhook, discordUserId);
            WebhookStatusText.Text = "Test message sent.";
        }
        catch (OperationCanceledException)
        {
            WebhookStatusText.Text = "Test timed out.";
        }
        catch (Exception error)
        {
            WebhookStatusText.Text = $"Test failed: {error.Message}";
        }
        finally
        {
            _testingWebhook = false;
            TestWebhookButton.Content = "Test webhook";
            TestWebhookButton.IsEnabled = !_services.Coordinator.IsBusy;
        }
    }

    private Task SaveReportingSettingsAsync(string webhook, string discordUserId) => _services.UpdateSettingsAsync(settings => settings with
    {
        EncryptedWebhook = _services.SecretProtector.Protect(webhook),
        DiscordErrorUserId = discordUserId,
    });

    private void DispatchLog(MacroEvent entry)
    {
        _services.DeepDebug.RecordMacroEvent(entry);
        _services.DiagnosticCapture.RecordActionState($"{entry.State ?? entry.Level.ToString()}: {entry.Message}");
        Dispatcher.BeginInvoke(() => AppendLog(entry.Level == MacroEventLevel.Error ? $"ERROR: {entry.Message}" : entry.Message));
    }

    private void AppendFailureHandlingResult(MacroFailureHandlingResult result)
    {
        if (result.DiagnosticArchivePath is not null) AppendLog($"Automatic error diagnostics saved to {Path.GetFileName(result.DiagnosticArchivePath)}.");
        if (result.DiagnosticError is not null) AppendLog($"ERROR: Automatic error diagnostics: {result.DiagnosticError}");
        if (result.DiscordPingsSent) AppendLog($"Sent {DiscordWebhookClient.ErrorPingCount} Discord error alerts.");
        if (result.DiscordError is not null) AppendLog($"ERROR: Discord error alerts: {result.DiscordError}");
    }

    private void AppendLog(string message)
    {
        if (message.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase)) _services.Log.Error(message[6..].Trim());
        else _services.Log.Info(message);
        LogText.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        if (LogText.LineCount > 500)
        {
            int removeThrough = LogText.GetCharacterIndexFromLineIndex(LogText.LineCount - 500);
            LogText.Text = LogText.Text[removeThrough..];
            LogText.CaretIndex = LogText.Text.Length;
        }
        LogText.ScrollToEnd();
    }

    private void ValidateDiscord(string webhook, string discordUserId)
    {
        if (!DiscordWebhookClient.ValidateWebhookUrl(webhook)) throw new InvalidOperationException("Enter a valid Discord webhook URL, or leave it blank.");
        if (!DiscordWebhookClient.ValidateDiscordUserId(discordUserId)) throw new InvalidOperationException("Enter a valid Discord user ID, or leave it blank.");
        if (discordUserId.Length > 0 && webhook.Length == 0) throw new InvalidOperationException("A Discord webhook is required when an error-ping user ID is entered.");
    }

    private string CurrentWebhook() => ShowWebhookCheck.IsChecked == true ? WebhookVisible.Text.Trim() : WebhookPassword.Password.Trim();

    private MacroTaskKind SelectedTaskKind() => (TaskKindCombo.SelectedItem as NamedChoice<MacroTaskKind>)?.Value ?? MacroTaskKind.Challenge;

    private int IndexOfTask(string? id)
    {
        if (id is null) return -1;
        for (int index = 0; index < TaskRows.Count; index++)
        {
            if (TaskRows[index].Definition.Id == id) return index;
        }
        return -1;
    }

    private static bool SameWork(MacroTaskDefinition left, MacroTaskDefinition right) =>
        left.Kind == right.Kind &&
        left.PresetId == right.PresetId &&
        ((left.PlacementTarget is null &&
          right.PlacementTarget is null) ||
         (left.PlacementTarget is not null &&
          right.PlacementTarget is not null &&
          left.PlacementTarget.Matches(
              right.PlacementTarget))) &&
        left.TargetVictories == right.TargetVictories &&
        left.TargetRuntimeMinutes == right.TargetRuntimeMinutes &&
        left.CompleteOnRuntimeDefeat == right.CompleteOnRuntimeDefeat &&
        left.Difficulty == right.Difficulty &&
        left.HardMode == right.HardMode &&
        left.DefeatRetries == right.DefeatRetries &&
        left.RunTraitChallenge == right.RunTraitChallenge &&
        left.RunStatChallenge == right.RunStatChallenge &&
        left.RunSpriteChallenge == right.RunSpriteChallenge &&
        left.ExtractAtCheckpoint == right.ExtractAtCheckpoint &&
        left.BossesBeforeExtract == right.BossesBeforeExtract &&
        left.RefuelTarget == right.RefuelTarget &&
        left.RefuelIntervalMinutes ==
            right.RefuelIntervalMinutes;

    private static int ParsePositiveInt(TextBox field, string label) =>
        int.TryParse(field.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value
            : throw new InvalidDataException($"{label} must be a positive whole number.");

    private static string Label(MacroTaskKind kind) => kind switch
    {
        MacroTaskKind.Challenge => "Challenge",
        MacroTaskKind.Expedition => "Expedition",
        MacroTaskKind.Story => "Story",
        MacroTaskKind.Raid => "Raid",
        MacroTaskKind.Event => "Event",
        MacroTaskKind.Utility => "Utilities",
        MacroTaskKind.Bounty => "Bounty",
        _ => kind.ToString(),
    };

    private static string Label(ChallengeMapId map) => map switch
    {
        ChallengeMapId.SchoolGrounds => "School Grounds",
        ChallengeMapId.FlowerForest => "Flower Forest",
        ChallengeMapId.RoseKingdom => "Rose Kingdom",
        ChallengeMapId.FairyKingForest => "Fairy King Forest",
        ChallengeMapId.KingsTomb => "King's Tomb",
        _ => map.ToString(),
    };
}
