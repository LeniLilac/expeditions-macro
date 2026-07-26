using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.App.Controls;

public partial class DetectionBenchmarkPanel :
    UserControl
{
    private AppServices? _services;

    public DetectionBenchmarkPanel()
    {
        InitializeComponent();
        ModeCombo.ItemsSource = new[]
        {
            new BenchmarkOption(
                DetectionBenchmarkMode.ExpeditionNavigation,
                "Expedition full state pass"),
            new BenchmarkOption(
                DetectionBenchmarkMode.ExpeditionMatch,
                "Expedition live match"),
            new BenchmarkOption(
                DetectionBenchmarkMode.ChallengeMatch,
                "Challenge live match"),
            new BenchmarkOption(
                DetectionBenchmarkMode.StoryRaidMatch,
                "Story / Raid live match"),
            new BenchmarkOption(
                DetectionBenchmarkMode.EventMatch,
                "Event live match"),
        };
        ModeCombo.SelectedIndex = 0;
    }

    public void Initialize(AppServices services)
    {
        if (_services is not null) return;
        _services = services;
        _services.Coordinator.StateChanged +=
            Coordinator_StateChanged;
        UpdateEnabled();
    }

    private async void Run_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_services is null ||
            _services.Coordinator.IsBusy)
        {
            return;
        }
        if (ModeCombo.SelectedItem is not
                BenchmarkOption selected ||
            !int.TryParse(
                SamplesText.Text,
                out int samples) ||
            samples is < 3 or > 40)
        {
            ShowStatus(
                "Choose 3 to 40 samples.");
            return;
        }

        DetectionBenchmarkResult? result = null;
        ShowStatus("Benchmark running...");
        ResultsGrid.Visibility = Visibility.Collapsed;
        DeepDebugOperationContext context = new()
        {
            DebugTool = "detection-benchmark",
            DebugStepMode = "Continuous",
            OperationSettings = new
            {
                Mode = selected.Value.ToString(),
                Samples = samples,
                PollMilliseconds =
                    DetectionBenchmarkService
                        .ProductionPollMilliseconds,
            },
        };
        await _services.Coordinator.RunNowAsync(
            "Debug detection benchmark",
            token => Task.Run(
                async () =>
                {
                    IDetectorPack detector =
                        await _services.DetectorPacks
                            .LoadAsync(
                                AnimeExpeditionsDetectorSpec
                                    .PackId,
                                token)
                            .ConfigureAwait(false) ??
                        throw new InvalidOperationException(
                            "The Anime Expeditions detector pack is not installed.");
                    RobloxWindow window =
                        _services.Automation.FindWindow() ??
                        throw new InvalidOperationException(
                            "No visible Roblox window was found.");
                    if (!_services.Automation.Focus(window))
                    {
                        throw new InvalidOperationException(
                            "Windows could not focus Roblox.");
                    }
                    ClientBounds bounds =
                        _services.Automation
                            .GetClientBounds(window);
                    if (bounds.Width !=
                            RobloxClientProfile.Width ||
                        bounds.Height !=
                            RobloxClientProfile.Height)
                    {
                        await _services.Automation
                            .ResizeClientAsync(
                                window,
                                RobloxClientProfile.Width,
                                RobloxClientProfile.Height,
                                token)
                            .ConfigureAwait(false);
                    }
                    result =
                        await _services.DetectionBenchmark
                            .RunAsync(
                                window,
                                _services.TraceDetector(
                                    detector),
                                selected.Value,
                                samples,
                                token)
                            .ConfigureAwait(false);
                },
                token),
            context);

        if (result is null)
        {
            ShowStatus(
                "Benchmark did not complete.");
            return;
        }
        ShowResult(result);
    }

    private void ShowResult(
        DetectionBenchmarkResult result)
    {
        CaptureResult.Text = Format(result.Capture);
        ModeResult.Text = Format(result.ModeDetection);
        RecoveryResult.Text =
            Format(result.RootRecovery);
        TotalResult.Text = Format(result.TotalWork);
        RateResult.Text =
            $"{result.ProductionChecksPerSecond:F2}/s";
        StatusText.Text =
            $"Detection used {result.DetectionWorkPercent:F0}% " +
            $"of measured work. Last: " +
            $"{result.LastModeState} / " +
            $"{result.LastRecoveryState}.";
        StatusText.Visibility = Visibility.Visible;
        ResultsGrid.Visibility = Visibility.Visible;
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }

    private static string Format(
        DetectionBenchmarkMetric metric) =>
        $"{metric.AverageMilliseconds:F1} ms avg | " +
        $"{metric.P95Milliseconds:F1} p95";

    private void Coordinator_StateChanged(
        object? sender,
        EventArgs e) =>
        Dispatcher.BeginInvoke(UpdateEnabled);

    private void UpdateEnabled()
    {
        bool enabled =
            _services is not null &&
            !_services.Coordinator.IsBusy;
        RunButton.IsEnabled = enabled;
        ModeCombo.IsEnabled = enabled;
        SamplesText.IsEnabled = enabled;
    }

    private sealed record BenchmarkOption(
        DetectionBenchmarkMode Value,
        string Label);
}
