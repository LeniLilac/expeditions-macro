using System.IO;
using System.Windows.Threading;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Updates;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Camera;
using ExpeditionsMacro.Vision.Packs;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.App.Services;

public sealed class AppServices : IDisposable
{
    private readonly DiscordWebhookClient _discord;

    private AppServices(Dispatcher dispatcher)
    {
        Paths = new AppPaths();
        Paths.EnsureCreated();
        Log = new FileLogger(Paths.Logs);
        SettingsStore = new AppSettingsStore(Paths);
        PlacementModels = new PlacementModelRepository(Paths);
        Presets = new PresetRepository(Paths);
        CameraModels = new CameraModelRepository(Paths);
        DetectorPacks = new DetectorPackRepository(Paths);
        Automation = new WindowsRobloxAutomation();
        SecretProtector = new DpapiSecretProtector();
        Hotkey = new GlobalHotkeyService();
        Coordinator = new OperationCoordinator(dispatcher);
        DiagnosticCapture = new DiagnosticCaptureService(Automation, Paths);
        PlacementCapture = new PlacementCaptureService(Automation);
        Placement = new PlacementService(Automation, PlacementCapture, PlacementModels);
        Camera = new CameraAlignmentEngine(Automation, CameraModels);
        _discord = new DiscordWebhookClient();
        Expeditions = new ExpeditionMacroRunner(Automation, Camera, Placement, _discord);
        DetectorUpdates = new DetectorPackUpdateService(DetectorPacks);
        Hotkey.F6Pressed += (_, _) => Coordinator.HandleF6();
    }

    public AppPaths Paths { get; }
    public FileLogger Log { get; }
    public AppSettingsStore SettingsStore { get; }
    public PlacementModelRepository PlacementModels { get; }
    public PresetRepository Presets { get; }
    public CameraModelRepository CameraModels { get; }
    public DetectorPackRepository DetectorPacks { get; }
    public IRobloxAutomation Automation { get; }
    public ISecretProtector SecretProtector { get; }
    public GlobalHotkeyService Hotkey { get; }
    public OperationCoordinator Coordinator { get; }
    public DiagnosticCaptureService DiagnosticCapture { get; }
    public IPlacementCaptureService PlacementCapture { get; }
    public PlacementService Placement { get; }
    public CameraAlignmentEngine Camera { get; }
    public ExpeditionMacroRunner Expeditions { get; }
    public DetectorPackUpdateService DetectorUpdates { get; }
    public AppSettings Settings { get; private set; } = new();

    public static async Task<AppServices> CreateAsync(Dispatcher dispatcher)
    {
        AppServices services = new(dispatcher);
        services.Log.Info("Starting Expeditions Macro.");
        services.Settings = await services.SettingsStore.LoadAsync();
        await services.EnsureBundledDetectorPackAsync();
        services.Hotkey.Start();
        services.Log.Info($"Startup complete. F6 listener: {(services.Hotkey.IsRegistered ? "ready" : "unavailable")}.");
        return services;
    }

    public async Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update)
    {
        Settings = update(Settings);
        await SettingsStore.SaveAsync(Settings);
    }

    public void Dispose()
    {
        Coordinator.Cancel();
        Log.Info("Application closing.");
        Hotkey.Dispose();
        DetectorUpdates.Dispose();
        _discord.Dispose();
    }

    private async Task EnsureBundledDetectorPackAsync()
    {
        IReadOnlyList<DetectorPackManifest> installed = await DetectorPacks.ListAsync();
        if (installed.Any(pack => pack.PackId == AnimeExpeditionsDetectorSpec.PackId)) return;
        string source = Path.Combine(AppContext.BaseDirectory, "Resources", "DetectorPacks", AnimeExpeditionsDetectorSpec.PackId, AnimeExpeditionsDetectorSpec.BundledPackVersion);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("The bundled detector pack is missing from this build.");
        await DetectorPacks.InstallDirectoryAsync(source);
    }
}
