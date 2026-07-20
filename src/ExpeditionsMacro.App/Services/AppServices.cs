using System.IO;
using System.Windows.Threading;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Challenges;
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
        ChallengePresets = new ChallengePresetRepository(Paths);
        CameraModels = new CameraModelRepository(Paths);
        DetectorPacks = new DetectorPackRepository(Paths);
        Automation = new WindowsRobloxAutomation();
        SecretProtector = new DpapiSecretProtector();
        Hotkey = new GlobalHotkeyService();
        Coordinator = new OperationCoordinator(dispatcher);
        DiagnosticCapture = new DiagnosticCaptureService(Automation, Paths);
        PlacementCapture = new PlacementCaptureService(Automation);
        Placement = new PlacementService(Automation, PlacementCapture, PlacementModels);
        CameraRegionSelection = new CameraRegionSelectionService(Automation);
        Camera = new CameraAlignmentEngine(Automation, CameraModels);
        _discord = new DiscordWebhookClient();
        Challenges = new ChallengeMacroRunner(Automation, Camera, Placement, _discord);
        Expeditions = new ExpeditionMacroRunner(Automation, Camera, Placement, _discord);
        DetectorUpdates = new DetectorPackUpdateService(DetectorPacks);
        Hotkey.Pressed += (_, _) => Coordinator.HandleHotkey();
        Hotkey.BindingChanged += (_, _) => Coordinator.HotkeyDisplayName = Hotkey.DisplayName;
    }

    public AppPaths Paths { get; }
    public FileLogger Log { get; }
    public AppSettingsStore SettingsStore { get; }
    public PlacementModelRepository PlacementModels { get; }
    public PresetRepository Presets { get; }
    public ChallengePresetRepository ChallengePresets { get; }
    public CameraModelRepository CameraModels { get; }
    public DetectorPackRepository DetectorPacks { get; }
    public IRobloxAutomation Automation { get; }
    public ISecretProtector SecretProtector { get; }
    public GlobalHotkeyService Hotkey { get; }
    public OperationCoordinator Coordinator { get; }
    public DiagnosticCaptureService DiagnosticCapture { get; }
    public IPlacementCaptureService PlacementCapture { get; }
    public PlacementService Placement { get; }
    public CameraRegionSelectionService CameraRegionSelection { get; }
    public CameraAlignmentEngine Camera { get; }
    public ChallengeMacroRunner Challenges { get; }
    public ExpeditionMacroRunner Expeditions { get; }
    public DetectorPackUpdateService DetectorUpdates { get; }
    public AppSettings Settings { get; private set; } = new();

    public static async Task<AppServices> CreateAsync(Dispatcher dispatcher)
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
        services.Hotkey.Configure(configuredHotkey);
        services.Coordinator.HotkeyDisplayName = services.Hotkey.DisplayName;
        await services.EnsureBundledDetectorPackAsync();
        services.Hotkey.Start();
        services.Log.Info($"Startup complete. {services.Hotkey.DisplayName} listener: {(services.Hotkey.IsRegistered ? "ready" : "unavailable")}.");
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
        string source = Path.Combine(AppContext.BaseDirectory, "Resources", "DetectorPacks", AnimeExpeditionsDetectorSpec.PackId, AnimeExpeditionsDetectorSpec.BundledPackVersion);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("The bundled detector pack is missing from this build.");
        DetectorPackManifest bundled = await JsonFileStore.ReadAsync<DetectorPackManifest>(Path.Combine(source, "manifest.json"))
            ?? throw new InvalidDataException("The bundled detector pack manifest is missing.");
        DetectorPackManifest? current = installed.FirstOrDefault(pack => pack.PackId == AnimeExpeditionsDetectorSpec.PackId);
        if (current is not null && !string.Equals(current.Version, bundled.Version, StringComparison.OrdinalIgnoreCase)) return;
        if (current is not null && HasSameFiles(current, bundled)) return;
        await DetectorPacks.InstallDirectoryAsync(source);
    }

    private static bool HasSameFiles(DetectorPackManifest left, DetectorPackManifest right)
    {
        if (left.Files.Count != right.Files.Count) return false;
        Dictionary<string, DetectorPackFile> expected = right.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        return left.Files.All(file => expected.TryGetValue(file.Path, out DetectorPackFile? match) &&
            file.Bytes == match.Bytes &&
            string.Equals(file.Sha256, match.Sha256, StringComparison.OrdinalIgnoreCase));
    }
}
