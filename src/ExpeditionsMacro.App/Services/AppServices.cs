using System.IO;
using System.Windows.Threading;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Events;
using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Automation.Refuel;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Automation.Settings;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Camera;
using ExpeditionsMacro.Vision.Diagnostics;
using ExpeditionsMacro.Vision.Packs;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.App.Services;

public sealed record MacroFailureHandlingResult(
    string? DiagnosticArchivePath,
    bool DiscordPingsSent,
    string? DiagnosticError,
    string? DiscordError);

public sealed class AppServices : IDisposable
{
    private readonly DiscordWebhookClient _discord;
    private readonly SemaphoreSlim _settingsUpdateGate =
        new(1, 1);

    private AppServices(Dispatcher dispatcher)
    {
        Paths = new AppPaths();
        Paths.EnsureCreated();
        Log = new FileLogger(Paths.Logs);
        SettingsStore = new AppSettingsStore(Paths);
        PlacementModels = new PlacementModelRepository(Paths);
        ManualRecordings =
            new ManualInputRecordingRepository(Paths);
        Presets = new PresetRepository(Paths);
        ChallengePresets = new ChallengePresetRepository(Paths);
        StoryPresets = new StoryPresetRepository(Paths);
        RaidPresets = new RaidPresetRepository(Paths);
        MacroPlans = new MacroPlanRepository(Paths);
        FastNoAlignShare = new FastNoAlignShareService(
            MacroPlans,
            PlacementModels,
            Presets,
            ChallengePresets,
            StoryPresets,
            RaidPresets);
        PresetDeletion = new PresetDeletionService(Presets, ChallengePresets, StoryPresets, RaidPresets, MacroPlans);
        CameraModels = new CameraModelRepository(Paths);
        CameraShortcuts = new CameraSpawnShortcutRepository(Paths);
        DetectorPacks = new DetectorPackRepository(Paths);
        WindowsRobloxAutomation automation = new();
        automation.DiagnosticMessage += Log.Info;
        DeepDebug = new DeepDebugSessionService(
            Paths,
            () => Settings,
            () => Log.CurrentFile,
            Log.Info,
            Log.Warning);
        DebugCheckpoints = new DebugCheckpointController();
        VisionTrace.Detected += RecordVisionTrace;
        automation.AutomationTrace += DeepDebug.RecordWindowsTrace;
        IRobloxAutomation tracedAutomation =
            new DeepDebugRobloxAutomation(automation, DeepDebug);
        Automation = new DebugSteppingRobloxAutomation(
            tracedAutomation,
            DebugCheckpoints);
        DetectionBenchmark =
            new DetectionBenchmarkService(Automation);
        DebugCheckpoints.CheckpointAdded += checkpoint =>
            DeepDebug.RecordEvent(
                "debug_checkpoint",
                checkpoint.Kind.ToString().ToLowerInvariant(),
                new
                {
                    checkpoint.Sequence,
                    checkpoint.Title,
                    checkpoint.Detail,
                    checkpoint.State,
                    checkpoint.Confidence,
                    HasFrame = checkpoint.Frame is not null,
                });
        SecretProtector = new DpapiSecretProtector();
        Hotkey = new GlobalHotkeyService();
        Coordinator = new OperationCoordinator(dispatcher);
        Coordinator.OperationRunner =
            (
                description,
                debugContext,
                action,
                cancellationToken) =>
                DeepDebug.RunOperationAsync(
                    description,
                    debugContext,
                    async operationToken =>
                    {
                        operationToken.ThrowIfCancellationRequested();
                        if (automation.FindWindow() is { } window)
                        {
                            WindowsRobloxDisplayScale
                                .EnsureOneHundredPercent(window);
                        }
                        await action(operationToken)
                            .ConfigureAwait(false);
                    },
                    cancellationToken);
        DiagnosticCapture = new DiagnosticCaptureService(Automation, Paths);
        PlacementCaptureService placementCapture = new(Automation);
        placementCapture.InputTrace += DeepDebug.RecordPlacementInput;
        placementCapture.TraceEnabled = () => DeepDebug.IsActive;
        PlacementCapture = placementCapture;
        Placement = new PlacementService(
            Automation,
            PlacementCapture,
            PlacementModels,
            () => AppSettings
                .ParseChangeUnitTargetingKey(Settings),
            () => AppSettings
                .ParseAutoUpgradeUnitKey(Settings));
        ManualRecorder =
            new WindowsManualInputRecorder(Automation);
        WindowsManualInputPlayback manualPlayback =
            new(Automation);
        manualPlayback.SummaryObserved += summary =>
            DeepDebug.RecordEvent(
                "manual_input",
                "playback_summary",
                new
                {
                    summary.RecordingId,
                    summary.SentEvents,
                    summary.TotalEvents,
                    summary.MaximumDriftMicroseconds,
                    summary.Succeeded,
                });
        ManualPlayback = manualPlayback;
        ManualRoutes = new ManualInputRouteService(
            ManualRecordings,
            ManualPlayback);
        RoutePositioning =
            new PlacementRoutePositioningService(
                Automation);
        CameraPose = new CameraPosePreparationService(
            Automation,
            () => AppSettings.ParseShiftLockKey(
                Settings.ShiftLockVirtualKey,
                Settings.MacroHotkeyVirtualKey,
                Settings.PlayMenuKey,
                Settings.UnitMenuKey,
                Settings.AreasMenuKey,
                Settings.CancelPlacementKey));
        FastNoAlign = new FastNoAlignPreparationSession(
            CameraPose);
        Camera = new CameraAlignmentEngine(
            Automation,
            CameraModels,
            CameraShortcuts,
            posePreparation: CameraPose);
        RobloxRecovery = new RobloxPrivateServerRecoveryService(
            Automation,
            new WindowsRobloxProcessController(),
            async cancellationToken =>
            {
                IDetectorPack detector =
                    await DetectorPacks.LoadAsync(
                        AnimeExpeditionsDetectorSpec.PackId,
                        cancellationToken).ConfigureAwait(false) ??
                    throw new InvalidOperationException(
                        "The Anime Expeditions detector pack is unavailable for Roblox restart recovery.");
                return TraceDetector(detector);
            });
        ResourceRefuel = new ResourceRefuelService(
            Automation,
            RobloxRecovery);
        StartupPreflight =
            new MacroStartupPreflightService(
                Automation,
                CameraPose);
        MatchLobby = new MatchLobbyNavigator(Automation);
        _discord = new DiscordWebhookClient();
        Teams = new TeamSelectionService(Automation);
        Stages = new StageMacroRunner(
            Automation,
            Camera,
            Placement,
            Teams,
            _discord,
            FastNoAlign,
            ManualRoutes);
        Scheduler = new MacroScheduler(MacroPlans);
        RecoveringScheduler = new RecoveringMacroScheduler(
            Scheduler,
            MacroPlans,
            RobloxRecovery);
        Challenges = new ChallengeMacroRunner(
            Automation,
            Camera,
            Placement,
            Teams,
            _discord,
            FastNoAlign,
            ManualRoutes);
        Expeditions = new ExpeditionMacroRunner(
            Automation,
            Camera,
            Placement,
            Teams,
            _discord,
            FastNoAlign,
            ManualRoutes);
        Events = new EventMacroRunner(
            Automation,
            Placement,
            Teams,
            _discord,
            FastNoAlign,
            ManualRoutes);
        Hotkey.Pressed += (_, _) =>
        {
            DeepDebug.RecordEvent("input", "global_hotkey_pressed", new { Hotkey = Hotkey.DisplayName, CoordinatorState = Coordinator.State });
            Coordinator.HandleHotkey();
        };
        Hotkey.BindingChanged += (_, _) => Coordinator.HotkeyDisplayName = Hotkey.DisplayName;
    }

    public AppPaths Paths { get; }
    public FileLogger Log { get; }
    public AppSettingsStore SettingsStore { get; }
    public PlacementModelRepository PlacementModels { get; }
    public ManualInputRecordingRepository ManualRecordings { get; }
    public PresetRepository Presets { get; }
    public ChallengePresetRepository ChallengePresets { get; }
    public StoryPresetRepository StoryPresets { get; }
    public RaidPresetRepository RaidPresets { get; }
    public MacroPlanRepository MacroPlans { get; }
    public FastNoAlignShareService FastNoAlignShare { get; }
    public PresetDeletionService PresetDeletion { get; }
    public CameraModelRepository CameraModels { get; }
    public CameraSpawnShortcutRepository CameraShortcuts { get; }
    public DetectorPackRepository DetectorPacks { get; }
    public DeepDebugSessionService DeepDebug { get; }
    public DebugCheckpointController DebugCheckpoints { get; }
    public IRobloxAutomation Automation { get; }
    public DetectionBenchmarkService DetectionBenchmark { get; }
    public ISecretProtector SecretProtector { get; }
    public GlobalHotkeyService Hotkey { get; }
    public OperationCoordinator Coordinator { get; }
    public DiagnosticCaptureService DiagnosticCapture { get; }
    public IPlacementCaptureService PlacementCapture { get; }
    public PlacementService Placement { get; }
    public IManualInputRecorder ManualRecorder { get; }
    public IManualInputPlayback ManualPlayback { get; }
    public ManualInputRouteService ManualRoutes { get; }
    public PlacementRoutePositioningService RoutePositioning { get; }
    public CameraPosePreparationService CameraPose { get; }
    public FastNoAlignPreparationSession FastNoAlign { get; }
    public CameraAlignmentEngine Camera { get; }
    public RobloxPrivateServerRecoveryService RobloxRecovery { get; }
    public ResourceRefuelService ResourceRefuel { get; }
    public MacroStartupPreflightService StartupPreflight { get; }
    public MatchLobbyNavigator MatchLobby { get; }
    public TeamSelectionService Teams { get; }
    public StageMacroRunner Stages { get; }
    public MacroScheduler Scheduler { get; }
    public RecoveringMacroScheduler RecoveringScheduler { get; }
    public ChallengeMacroRunner Challenges { get; }
    public ExpeditionMacroRunner Expeditions { get; }
    public EventMacroRunner Events { get; }
    public AppSettings Settings { get; private set; } = new();

    public event EventHandler? SettingsChanged;

    public static async Task<AppServices> CreateAsync(
        Dispatcher dispatcher,
        bool startRuntimeServices = true)
    {
        AppServices services = new(dispatcher);
        services.Log.Info("Starting Expeditions Macro.");
        services.Settings = await services.SettingsStore.LoadAsync();
        int configuredHotkey = services.Settings.MacroHotkeyVirtualKey;
        if (!GlobalHotkeyService.IsSupportedVirtualKey(configuredHotkey))
        {
            configuredHotkey = GlobalHotkeyService.DefaultVirtualKey;
            services.Settings = services.Settings with { MacroHotkeyVirtualKey = configuredHotkey };
            await services.SettingsStore.SaveAsync(services.Settings);
        }
        if (services.Settings.ShiftLockVirtualKey != 0 &&
            !KeyboardKey.IsSupportedShiftLockKey(
                services.Settings.ShiftLockVirtualKey))
        {
            services.Settings = services.Settings with { ShiftLockVirtualKey = AppSettings.DefaultShiftLockVirtualKey };
            await services.SettingsStore.SaveAsync(services.Settings);
        }
        services.Hotkey.Configure(configuredHotkey);
        services.Coordinator.HotkeyDisplayName = services.Hotkey.DisplayName;
        await services.EnsureBundledDetectorPackAsync();
        if (startRuntimeServices)
        {
            await services.NormalizeRobloxWindowOnStartupAsync();
            services.Hotkey.Start();
            services.Log.Info($"Startup complete. {services.Hotkey.DisplayName} listener: {(services.Hotkey.IsRegistered ? "ready" : "unavailable")}.");
        }
        else
        {
            services.Log.Info(
                "Snapshot startup complete without Roblox normalization or global hotkey registration.");
        }
        return services;
    }

    public async Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        await _settingsUpdateGate.WaitAsync();
        try
        {
            AppSettings updated = update(Settings);
            await SettingsStore.SaveAsync(updated);
            Settings = updated;
        }
        finally
        {
            _settingsUpdateGate.Release();
        }
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public IDetectorPack TraceDetector(IDetectorPack detector) => new DeepDebugDetectorPack(detector, DeepDebug);

    public async Task<MacroFailureHandlingResult> HandleMacroFailureAsync(
        string macroName,
        string webhookUrl,
        string discordUserId,
        Exception error)
    {
        Task<(string? Path, string? Error)> diagnosticTask = Settings.AutoCaptureOnMacroError
            ? CaptureFailureDiagnosticsAsync(macroName, error.Message)
            : Task.FromResult<(string?, string?)>((null, null));
        Task<(bool Sent, string? Error)> pingTask =
            string.IsNullOrWhiteSpace(webhookUrl) || string.IsNullOrWhiteSpace(discordUserId)
                ? Task.FromResult<(bool, string?)>((false, null))
                : SendFailurePingsAsync(macroName, webhookUrl, discordUserId, error.Message);

        await Task.WhenAll(diagnosticTask, pingTask);
        (string? path, string? diagnosticError) = await diagnosticTask;
        (bool sent, string? discordError) = await pingTask;
        return new MacroFailureHandlingResult(path, sent, diagnosticError, discordError);
    }

    public async Task<MacroFailureHandlingResult>
        HandleRecoverableMacroFailureAsync(
            string macroName,
            Exception error)
    {
        (string? path, string? diagnosticError) =
            Settings.AutoCaptureOnMacroError
                ? await CaptureFailureDiagnosticsAsync(
                    $"{macroName} recovery",
                    error.Message)
                : (null, null);
        return new MacroFailureHandlingResult(
            path,
            DiscordPingsSent: false,
            diagnosticError,
            DiscordError: null);
    }

    public async Task TestDiscordWebhookAsync(string webhookUrl, CancellationToken cancellationToken)
    {
        await _discord.SendTestAsync(webhookUrl, cancellationToken);
        Log.Info("Discord webhook test message sent.");
    }

    public void Dispose()
    {
        Coordinator.Cancel();
        VisionTrace.Detected -= RecordVisionTrace;
        if (Automation is IDisposable automation) automation.Dispose();
        Log.Info("Application closing.");
        Hotkey.Dispose();
        _discord.Dispose();
    }

    private void RecordVisionTrace(VisionDetectionTrace trace)
    {
        DeepDebug.RecordVisionTrace(trace);
        DebugCheckpoints.RecordDetection(trace);
    }

    private async Task EnsureBundledDetectorPackAsync()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "Resources", "DetectorPacks", AnimeExpeditionsDetectorSpec.PackId, AnimeExpeditionsDetectorSpec.BundledPackVersion);
        if (await DetectorPacks.EnsureBundledAsync(source))
        {
            Log.Info("Refreshed the bundled detector references.");
        }
    }

    private async Task<(string? Path, string? Error)> CaptureFailureDiagnosticsAsync(string macroName, string failure)
    {
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            string captureName = $"error-{macroName.ToLowerInvariant().Replace(' ', '-')}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
            DiagnosticCaptureResult result = await DiagnosticCapture.CaptureFailureAsync(
                captureName,
                failure,
                cancellationToken: timeout.Token,
                logFilePath: Settings.IncludeLogsInDiagnosticArchives ? Log.CurrentFile : null);
            Log.Info($"Automatic failure diagnostics saved to {Path.GetFileName(result.ArchivePath)}.");
            if (result.ArchivesPruned > 0)
            {
                Log.Info($"Removed {result.ArchivesPruned} older automatic error archive(s); retaining the newest {DiagnosticCaptureService.MaximumAutomaticArchives}.");
            }
            return (result.ArchivePath, null);
        }
        catch (Exception captureError)
        {
            Log.Error("Automatic failure diagnostics could not be saved.", captureError);
            return (null, captureError.Message);
        }
    }

    private async Task NormalizeRobloxWindowOnStartupAsync()
    {
        RobloxWindow? window = Automation.FindWindow();
        if (window is null)
        {
            Log.Info("Roblox was not open during startup. Standard sizing will be applied when a workflow starts.");
            return;
        }

        try
        {
            await Automation.ResizeClientAsync(
                window.Value,
                RobloxClientProfile.Width,
                RobloxClientProfile.Height,
                CancellationToken.None);
            Log.Info($"Roblox client resized to {RobloxClientProfile.Width} by {RobloxClientProfile.Height} during startup using {window.Value.ProcessDescription}.");
        }
        catch (Exception error)
        {
            Log.Warning($"Roblox could not be resized during startup: {error.Message}");
        }
    }

    private async Task<(bool Sent, string? Error)> SendFailurePingsAsync(
        string macroName,
        string webhookUrl,
        string discordUserId,
        string detail)
    {
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            await _discord.SendErrorPingsAsync(webhookUrl, discordUserId, macroName, detail, timeout.Token);
            Log.Info($"Sent {DiscordWebhookClient.ErrorPingCount} Discord error alerts.");
            return (true, null);
        }
        catch (Exception discordError)
        {
            Log.Error("Discord error alerts could not be sent.", discordError);
            return (false, discordError.Message);
        }
    }

}
