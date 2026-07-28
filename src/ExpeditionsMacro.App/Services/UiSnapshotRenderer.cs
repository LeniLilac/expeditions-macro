using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Pages;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Services;

internal static class UiSnapshotRenderer
{
    private static readonly (
        string Key,
        string File,
        bool ShowPageEnd,
        bool ShowDebugUtilities,
        MacroPlanSnapshotState MacroPlanState,
        ManualRecordingsSnapshotState
            RecordingsState)[] Pages =
    [
        ("Dashboard", "dashboard", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Dashboard", "dashboard-run-log", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Dashboard", "dashboard-controls", true, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Dashboard", "dashboard-controls-configured", true, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Dashboard", "dashboard-navigation-collapsed", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Dashboard", "dashboard-small-navigation-collapsed", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Dashboard", "dashboard-run-log-small-navigation-collapsed", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan-empty", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan-empty-loop", false, false, MacroPlanSnapshotState.EmptyLoop, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan-tasks-only", false, false, MacroPlanSnapshotState.TasksOnly, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan", false, false, MacroPlanSnapshotState.NestedLoops, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan-loop-settings", false, false, MacroPlanSnapshotState.LoopSettingsPopup, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan-add-task", false, false, MacroPlanSnapshotState.TaskPopup, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan-add-story-act", false, false, MacroPlanSnapshotState.StoryActTaskPopup, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan-add-story-mastery", false, false, MacroPlanSnapshotState.StoryMasteryTaskPopup, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan-add-story-infinite", false, false, MacroPlanSnapshotState.StoryInfiniteTaskPopup, ManualRecordingsSnapshotState.Ready),
        ("Macro Plan", "macro-plan-share", true, false, MacroPlanSnapshotState.NestedLoops, ManualRecordingsSnapshotState.Ready),
        ("Placement Setup", "placement-setup", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Placement Setup", "placement-setup-timing", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Placement Setup", "placement-setup-recording", true, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Placement Setup", "placement-setup-small-controls", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Placement Setup", "placement-setup-small-steps", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Placement Setup", "placement-setup-medium-steps", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Placement Setup", "placement-setup-recording-small-controls", true, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Placement Setup", "placement-setup-catalog-collapsed", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Placement Setup", "placement-setup-both-rails-collapsed", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Recordings", "recordings", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Recordings", "recordings-armed-recording", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.ArmedRecording),
        ("Recordings", "recordings-running-recording", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.RunningRecording),
        ("Recordings", "recordings-armed-playback", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.ArmedPlayback),
        ("Recordings", "recordings-running-playback", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.RunningPlayback),
        ("Debug", "debug", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Debug", "debug-refuel", true, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Debug", "debug-utilities", false, true, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Settings", "settings", false, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Settings", "settings-diagnostics", false, true, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
        ("Settings", "settings-debug", true, false, MacroPlanSnapshotState.Empty, ManualRecordingsSnapshotState.Ready),
    ];

    public static async Task RenderAsync(AppServices services, string outputDirectory)
    {
        string output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        string progressPath =
            Path.Combine(output, "snapshot-progress.txt");
        await File.WriteAllTextAsync(
            progressPath,
            "Creating snapshot window.");
        MainWindow window = new(services, snapshotMode: true)
        {
            Width = 1200,
            Height = 780,
            Left = 0,
            Top = 0,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0.01,
        };
        VerifyBundledFont(window);
        window.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Loaded);
            await File.WriteAllTextAsync(
                progressPath,
                "Refreshing model-backed pages.");
            await window.VerifyBackgroundModelRefreshAsync();
            int rendered = 0;
            foreach (AppTheme theme in new[] { AppTheme.Dark, AppTheme.Light })
            {
                ThemeService.Apply(theme);
                foreach ((
                    string key,
                    string file,
                    bool showPageEnd,
                    bool showAlternateState,
                    MacroPlanSnapshotState macroPlanState,
                    ManualRecordingsSnapshotState
                        recordingsState) in Pages)
                {
                    SetSnapshotRailState(
                        window,
                        file);
                    await window.SelectPageForSnapshotAsync(
                        key,
                        showPageEnd,
                        showAlternateState,
                        macroPlanState,
                        recordingsState);
                    Size size = SnapshotSize(key, file);
                    await File.WriteAllTextAsync(
                        progressPath,
                        $"Rendering {file} ({theme.ToString().ToLowerInvariant()}) at {size.Width:0}x{size.Height:0}.");
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    if (window.Content is not FrameworkElement root) throw new InvalidOperationException("The main window has no renderable content.");
                    window.Width = size.Width;
                    window.Height = size.Height;
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();
                    PrepareCompactPlacementSnapshot(
                        root,
                        file);
                    PrepareDashboardSnapshot(
                        root,
                        file);
                    RenderTargetBitmap bitmap = new(
                        (int)size.Width,
                        (int)size.Height,
                        96,
                        96,
                        PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    if (macroPlanState ==
                            MacroPlanSnapshotState
                                .LoopSettingsPopup)
                    {
                        bitmap =
                            RenderLoopSettingsPopup(
                                root,
                                bitmap,
                                size);
                    }
                    EnsureVisiblePixels(bitmap, file, theme);
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    await using FileStream stream = new(Path.Combine(output, $"{file}-{theme.ToString().ToLowerInvariant()}.png"), FileMode.Create, FileAccess.Write, FileShare.None);
                    encoder.Save(stream);
                    rendered++;
                }
            }
            await File.WriteAllTextAsync(
                progressPath,
                $"Completed {rendered} snapshots.");
        }
        finally
        {
            window.Close();
        }
    }

    private static Size SnapshotSize(
        string key,
        string file)
    {
        Size standard = new(1200, 780);
        if (string.Equals(
                file,
                "placement-setup-medium-steps",
                StringComparison.OrdinalIgnoreCase))
        {
            return new Size(1400, 1080);
        }
        if (string.Equals(
                file,
                "dashboard",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                file,
                "dashboard-run-log",
                StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("CI"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                return standard;
            }
            return new Size(1660, 1040);
        }
        if (file.Contains(
                "-small",
                StringComparison.OrdinalIgnoreCase) ||
            file.Contains(
                "-collapsed",
                StringComparison.OrdinalIgnoreCase))
        {
            return new Size(960, 640);
        }
        if (string.Equals(
                key,
                "Macro Plan",
                StringComparison.OrdinalIgnoreCase))
        {
            return new Size(1440, 1080);
        }
        if (!string.Equals(
                key,
                "Placement Setup",
                StringComparison.OrdinalIgnoreCase))
        {
            return standard;
        }

        Size wide = new(1660, 1040);
        if (string.Equals(
                Environment.GetEnvironmentVariable("CI"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return standard;
        }
        Rect workArea = SystemParameters.WorkArea;
        return workArea.Width >= wide.Width &&
               workArea.Height >= wide.Height
            ? wide
            : standard;
    }

    private static void SetSnapshotRailState(
        MainWindow window,
        string file)
    {
        window.SetNavigationRailCollapsedForSnapshot(
            file.Contains(
                "navigation-collapsed",
                StringComparison.OrdinalIgnoreCase) ||
            file.Contains(
                "both-rails-collapsed",
                StringComparison.OrdinalIgnoreCase));
        window.SetPlacementCatalogCollapsedForSnapshot(
            file.Contains(
                "catalog-collapsed",
                StringComparison.OrdinalIgnoreCase) ||
            file.Contains(
                "both-rails-collapsed",
                StringComparison.OrdinalIgnoreCase));
    }

    private static void PrepareCompactPlacementSnapshot(
        FrameworkElement root,
        string file)
    {
        if (!file.Contains(
                "placement-setup",
                StringComparison.OrdinalIgnoreCase) ||
            (!(file.Contains(
                   "-small",
                   StringComparison.OrdinalIgnoreCase) ||
               file.Contains(
                   "-medium",
                   StringComparison.OrdinalIgnoreCase) ||
               file.Contains(
                   "-collapsed",
                   StringComparison.OrdinalIgnoreCase)) &&
             !string.Equals(
                 file,
                 "placement-setup-timing",
                 StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        PlacementFastEditorView? editor =
            FindVisualChild<PlacementFastEditorView>(
                root);
        if (editor is null)
        {
            throw new InvalidOperationException(
                "The Placement Setup snapshot did not contain its editor.");
        }
        if (string.Equals(
                file,
                "placement-setup-timing",
                StringComparison.OrdinalIgnoreCase))
        {
            editor.SetSnapshotSettings(
                placementIntervalMilliseconds: 900,
                placementAttempts: 4,
                defaultAfterStartDelayMilliseconds: 30_000,
                impossibilityThresholdMinutes: 0,
                recordingMode: false);
            root.UpdateLayout();
            return;
        }
        if (file.Contains(
                "recording",
                StringComparison.OrdinalIgnoreCase))
        {
            editor.ClearSnapshotSettings();
        }
        editor.SetCompactSnapshotViewport(
            file.Contains(
                "steps",
                StringComparison.OrdinalIgnoreCase));
        root.UpdateLayout();
    }

    private static void PrepareDashboardSnapshot(
        FrameworkElement root,
        string file)
    {
        bool showConfiguredQuickPlacement =
            string.Equals(
                file,
                "dashboard-controls-configured",
                StringComparison.OrdinalIgnoreCase);
        bool showCurrentRun = file.Contains(
            "dashboard-small",
            StringComparison.OrdinalIgnoreCase);
        bool showLongRunLog = file.Contains(
            "dashboard-run-log",
            StringComparison.OrdinalIgnoreCase);
        if (!showConfiguredQuickPlacement &&
            !showCurrentRun &&
            !showLongRunLog)
        {
            return;
        }

        if (showConfiguredQuickPlacement)
        {
            SettingsKeyBindingsPanel? panel =
                FindVisualChild<
                    SettingsKeyBindingsPanel>(root);
            if (panel is null)
            {
                throw new InvalidOperationException(
                    "The configured Controls snapshot did not contain its key-binding panel.");
            }
            panel.ShowConfiguredQuickPlacementForSnapshot();
        }

        MacroPage? page =
            FindVisualChild<MacroPage>(root);
        if (page is null)
        {
            throw new InvalidOperationException(
                "The Dashboard snapshot did not contain its page.");
        }
        if (showLongRunLog)
        {
            page.ShowBoundedRunLogForSnapshot();
        }
        if (showCurrentRun)
        {
            page.ShowCurrentRunForSnapshot();
        }
        root.UpdateLayout();
    }

    private static RenderTargetBitmap
        RenderLoopSettingsPopup(
        FrameworkElement root,
        RenderTargetBitmap pageBitmap,
        Size pageSize)
    {
        MacroPlanLoopEditor? editor =
            FindVisualChild<
                MacroPlanLoopEditor>(root);
        if (editor is null ||
            !editor.TryGetLoopSettingsSnapshotVisual(
                root,
                out FrameworkElement popup,
                out Point origin))
        {
            throw new InvalidOperationException(
                "The loop settings popup was not open for its UI snapshot.");
        }
        Size popupSize = new(
            popup.ActualWidth,
            popup.ActualHeight);
        popup.Measure(popupSize);
        popup.Arrange(new Rect(popupSize));
        popup.UpdateLayout();
        RenderTargetBitmap popupBitmap = new(
            (int)Math.Ceiling(popupSize.Width),
            (int)Math.Ceiling(popupSize.Height),
            96,
            96,
            PixelFormats.Pbgra32);
        popupBitmap.Render(popup);

        DrawingVisual composite = new();
        using (DrawingContext drawing =
               composite.RenderOpen())
        {
            drawing.DrawImage(
                pageBitmap,
                new Rect(pageSize));
            drawing.DrawImage(
                popupBitmap,
                new Rect(origin, popupSize));
        }
        RenderTargetBitmap result = new(
            (int)pageSize.Width,
            (int)pageSize.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        result.Render(composite);
        return result;
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        for (int index = 0;
             index < VisualTreeHelper.GetChildrenCount(
                 parent);
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);
            if (child is T match)
            {
                return match;
            }
            T? nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }
        return null;
    }

    private static void VerifyBundledFont(MainWindow window)
    {
        if (!window.FontFamily.FamilyNames.Values.Any(name => string.Equals(name, "Fredoka", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("The main window did not inherit the embedded Fredoka font. WPF would silently use a fallback typeface.");
        }
    }

    private static void EnsureVisiblePixels(RenderTargetBitmap bitmap, string page, AppTheme theme)
    {
        int stride = checked(bitmap.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        int visible = 0;
        for (int index = 3; index < pixels.Length; index += 4)
        {
            if (pixels[index] != 0) visible++;
        }
        if (visible < bitmap.PixelWidth * bitmap.PixelHeight / 2)
        {
            throw new InvalidOperationException($"The {page} {theme.ToString().ToLowerInvariant()} UI snapshot rendered mostly transparent.");
        }
    }
}
