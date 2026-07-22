using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ExpeditionsMacro.DeepDebugViewer.Controls;
using ExpeditionsMacro.DeepDebugViewer.Services;
using Microsoft.Win32;

namespace ExpeditionsMacro.DeepDebugViewer;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<NearbyEventItem> _nearbyEvents = [];
    private readonly PlaybackSpeedOption[] _speeds =
    [
        new(0.25, "0.25×"),
        new(0.5, "0.5×"),
        new(1, "1×"),
        new(2, "2×"),
        new(4, "4×"),
    ];
    private readonly CacheBudgetOption[] _cacheBudgets =
    [
        new(2L * 1024 * 1024 * 1024, "2 GB"),
        new(5L * 1024 * 1024 * 1024, "5 GB"),
        new(FrameBitmapCache.DefaultBudgetBytes, "10 GB"),
        new(20L * 1024 * 1024 * 1024, "20 GB"),
    ];

    private DeepDebugArchive? _archive;
    private FrameBitmapCache? _frameCache;
    private CancellationTokenSource? _openCancellation;
    private CancellationTokenSource? _displayCancellation;
    private CancellationTokenSource? _sliderCancellation;
    private CancellationTokenSource? _playbackCancellation;
    private CancellationTokenSource? _prefetchCancellation;
    private int _currentFrameIndex;
    private bool _settingSlider;
    private bool _isBusy;
    private bool _isPlaying;
    private double _playbackSpeed = 1;

    public MainWindow()
    {
        InitializeComponent();
        EventList.ItemsSource = _nearbyEvents;
        SpeedCombo.ItemsSource = _speeds;
        SpeedCombo.SelectedItem = _speeds.First(option => option.Multiplier == 1);
        CacheBudgetCombo.ItemsSource = _cacheBudgets;
        CacheBudgetCombo.SelectedItem = _cacheBudgets.First(option => option.Bytes == FrameBitmapCache.DefaultBudgetBytes);
        Closed += (_, _) => DisposeArchive();
    }

    public async void OpenArchiveFromCommandLine(string path)
    {
        await OpenArchiveAsync(path);
    }

    private async void OpenArchive_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Open Deep Debug archive",
            Filter = "Deep Debug ZIP (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = DefaultDiagnosticsDirectory(),
        };
        if (dialog.ShowDialog(this) == true) await OpenArchiveAsync(dialog.FileName);
    }

    private async Task OpenArchiveAsync(string path)
    {
        if (_isBusy) _openCancellation?.Cancel();
        StopPlayback();
        StopPrefetch();
        _displayCancellation?.Cancel();
        _sliderCancellation?.Cancel();
        _openCancellation?.Cancel();
        _openCancellation?.Dispose();
        _openCancellation = new CancellationTokenSource();
        CancellationToken token = _openCancellation.Token;
        SetBusy(true, "Opening archive...");

        try
        {
            Progress<string> progress = new(message => StatusText.Text = message);
            DeepDebugArchive opened = await DeepDebugArchive.OpenAsync(path, progress, token);
            if (opened.Frames.Count == 0)
            {
                opened.Dispose();
                throw new InvalidDataException("The archive does not contain any captured PNG frames.");
            }

            _archive?.Dispose();
            _archive = opened;
            long cacheBudget = (CacheBudgetCombo.SelectedItem as CacheBudgetOption)?.Bytes ?? FrameBitmapCache.DefaultBudgetBytes;
            _frameCache = new FrameBitmapCache(opened, cacheBudget);
            _currentFrameIndex = 0;
            ArchivePathText.Text = opened.Path;
            OperationText.Text = opened.Manifest.Operation;
            OutcomeText.Text = $"Outcome: {FriendlyOutcome(opened.Manifest.Outcome)}";
            RuntimeText.Text = $"Runtime: {FormatRuntime(opened.Manifest.Runtime)}";
            ArchiveCountText.Text = $"{opened.Frames.Count:N0} frames  ·  {opened.Events.Count:N0} events";
            TimelineSlider.Maximum = Math.Max(0, opened.Frames.Count - 1);
            SetBusy(false, opened.MalformedEventLines == 0
                ? $"Indexed {opened.Frames.Count:N0} frames. Read-ahead is ready."
                : $"Indexed {opened.Frames.Count:N0} frames; skipped {opened.MalformedEventLines:N0} malformed event lines.");
            UpdateCacheStatus();
            await ShowFrameAsync(0);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            SetBusy(false, "Archive loading canceled.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            SetBusy(false, "The archive could not be opened.");
            ShowPreviewMessage(error.Message, ViewerIconKind.CircleAlert, error: true);
            MessageBox.Show(
                this,
                error.Message,
                "Deep Debug archive could not be opened",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task ShowFrameAsync(int frameIndex)
    {
        DeepDebugArchive? archive = _archive;
        FrameBitmapCache? cache = _frameCache;
        if (archive is null || cache is null || archive.Frames.Count == 0) return;
        int target = Math.Clamp(frameIndex, 0, archive.Frames.Count - 1);
        _currentFrameIndex = target;
        DeepDebugFrameRecord frame = archive.Frames[target];
        UpdateFrameMetadata(archive, frame);
        StopPrefetch();

        _displayCancellation?.Cancel();
        _displayCancellation?.Dispose();
        _displayCancellation = new CancellationTokenSource();
        CancellationToken token = _displayCancellation.Token;
        try
        {
            ShowPreviewMessage("Loading frame...", ViewerIconKind.FileArchive);
            BitmapSource image = await cache.GetAsync(target, token);
            if (token.IsCancellationRequested || !ReferenceEquals(archive, _archive) || target != _currentFrameIndex) return;
            FrameImage.Source = image;
            PreviewMessageBorder.Visibility = Visibility.Collapsed;
            StatusText.Text = frame.EntryExists
                ? $"Frame {target + 1:N0} loaded."
                : "The timeline references this frame, but the PNG is missing.";
            UpdateCacheStatus();
            StartPrefetch(target, _isPlaying ? 48 : 10);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is IOException or InvalidDataException or NotSupportedException)
        {
            FrameImage.Source = null;
            ShowPreviewMessage(error.Message, ViewerIconKind.CircleAlert, error: true);
            StatusText.Text = $"Frame {target + 1:N0} is unavailable. Playback can continue.";
        }
    }

    private void UpdateFrameMetadata(DeepDebugArchive archive, DeepDebugFrameRecord frame)
    {
        _settingSlider = true;
        TimelineSlider.Value = frame.Index;
        _settingSlider = false;
        FramePathText.Text = frame.Path;
        FramePositionText.Text = $"{frame.Index + 1:N0} / {archive.Frames.Count:N0}";
        TimestampText.Text = frame.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);

        _nearbyEvents.Clear();
        foreach (DeepDebugTimelineEvent item in archive.GetNearbyEvents(frame.Index))
        {
            _nearbyEvents.Add(new NearbyEventItem(
                item.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
                $"{FriendlyToken(item.Category)} · {FriendlyToken(item.Action)}",
                string.IsNullOrWhiteSpace(item.Details) ? $"Sequence {item.Sequence:N0}" : item.Details,
                string.Equals(item.FramePath, frame.Path, StringComparison.OrdinalIgnoreCase)));
        }
        EventContextText.Text = $"Sequence {frame.Sequence:N0}. Showing {_nearbyEvents.Count:N0} nearby records.";
        EventList.ScrollIntoView(_nearbyEvents.FirstOrDefault(item => item.IsCurrentFrame));
    }

    private async void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settingSlider || _archive is null) return;
        _sliderCancellation?.Cancel();
        _sliderCancellation?.Dispose();
        _sliderCancellation = new CancellationTokenSource();
        CancellationToken token = _sliderCancellation.Token;
        try
        {
            await Task.Delay(65, token);
            await ShowFrameAsync((int)Math.Round(e.NewValue));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        await ShowFrameAsync(_currentFrameIndex - 1);
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        await ShowFrameAsync(_currentFrameIndex + 1);
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying) StopPlayback();
        else StartPlayback();
    }

    private void StartPlayback()
    {
        if (_archive is null || _archive.Frames.Count == 0 || _isPlaying) return;
        if (_currentFrameIndex >= _archive.Frames.Count - 1) _currentFrameIndex = 0;
        _isPlaying = true;
        PlayPauseButton.Content = "Pause";
        ViewerIcon.SetIcon(PlayPauseButton, ViewerIconKind.Pause);
        _playbackCancellation?.Dispose();
        _playbackCancellation = new CancellationTokenSource();
        _ = PlaybackLoopAsync(_playbackCancellation.Token);
    }

    private void StopPlayback()
    {
        _playbackCancellation?.Cancel();
        _isPlaying = false;
        if (PlayPauseButton is null) return;
        PlayPauseButton.Content = "Play";
        ViewerIcon.SetIcon(PlayPauseButton, ViewerIconKind.Play);
    }

    private async Task PlaybackLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_archive is { } archive && _currentFrameIndex < archive.Frames.Count - 1)
            {
                TimeSpan delay = PlaybackDelay(archive.Frames[_currentFrameIndex], archive.Frames[_currentFrameIndex + 1]);
                await Task.Delay(delay, cancellationToken);
                await ShowFrameAsync(_currentFrameIndex + 1);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            StatusText.Text = $"Playback stopped: {error.Message}";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested || _playbackCancellation?.Token == cancellationToken)
            {
                StopPlayback();
            }
        }
    }

    private TimeSpan PlaybackDelay(DeepDebugFrameRecord current, DeepDebugFrameRecord next)
    {
        double actualMilliseconds = (next.TimestampUtc - current.TimestampUtc).TotalMilliseconds;
        double bounded = Math.Clamp(actualMilliseconds <= 0 ? 100 : actualMilliseconds, 30, 2000);
        return TimeSpan.FromMilliseconds(Math.Clamp(bounded / _playbackSpeed, 15, 2000));
    }

    private void SpeedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpeedCombo.SelectedItem is PlaybackSpeedOption speed) _playbackSpeed = speed.Multiplier;
    }

    private void CacheBudgetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CacheBudgetCombo.SelectedItem is not CacheBudgetOption option || _frameCache is null) return;
        _frameCache.SetBudget(option.Bytes);
        UpdateCacheStatus();
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        StopPrefetch();
        _displayCancellation?.Cancel();
        _frameCache?.Clear();
        UpdateCacheStatus();
        StatusText.Text = "Decoded frame cache cleared. The displayed frame remains visible.";
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is ComboBox) return;
        switch (e.Key)
        {
            case Key.Space when _archive is not null:
                if (_isPlaying) StopPlayback(); else StartPlayback();
                e.Handled = true;
                break;
            case Key.Left when _archive is not null:
                StopPlayback();
                await ShowFrameAsync(_currentFrameIndex - 1);
                e.Handled = true;
                break;
            case Key.Right when _archive is not null:
                StopPlayback();
                await ShowFrameAsync(_currentFrameIndex + 1);
                e.Handled = true;
                break;
            case Key.O when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                OpenArchive_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryDroppedZip(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (TryDroppedZip(e.Data, out string? path)) await OpenArchiveAsync(path);
    }

    private static bool TryDroppedZip(IDataObject data, out string path)
    {
        path = string.Empty;
        if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return false;
        path = files[0];
        return path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private void SetBusy(bool busy, string message)
    {
        _isBusy = busy;
        bool ready = !busy && _archive is not null && _archive.Frames.Count > 0;
        TimelineSlider.IsEnabled = ready;
        PreviousButton.IsEnabled = ready;
        NextButton.IsEnabled = ready;
        PlayPauseButton.IsEnabled = ready;
        SpeedCombo.IsEnabled = ready;
        CacheBudgetCombo.IsEnabled = ready;
        ClearCacheButton.IsEnabled = ready;
        StatusText.Text = message;
    }

    private void ShowPreviewMessage(string message, ViewerIconKind icon, bool error = false)
    {
        PreviewMessageText.Text = message;
        PreviewMessageIcon.Icon = icon;
        PreviewMessageIcon.Foreground = (System.Windows.Media.Brush)FindResource(error ? "ErrorBrush" : "MutedBrush");
        PreviewMessageBorder.Visibility = Visibility.Visible;
    }

    private void DisposeArchive()
    {
        StopPlayback();
        StopPrefetch();
        _openCancellation?.Cancel();
        _displayCancellation?.Cancel();
        _sliderCancellation?.Cancel();
        _archive?.Dispose();
        _archive = null;
    }

    private void StartPrefetch(int center, int radius)
    {
        DeepDebugArchive? archive = _archive;
        FrameBitmapCache? cache = _frameCache;
        if (archive is null || cache is null || radius <= 0) return;
        _prefetchCancellation = new CancellationTokenSource();
        _ = PrefetchAroundAsync(archive, cache, center, radius, _prefetchCancellation.Token);
    }

    private async Task PrefetchAroundAsync(
        DeepDebugArchive archive,
        FrameBitmapCache cache,
        int center,
        int radius,
        CancellationToken cancellationToken)
    {
        int completed = 0;
        try
        {
            for (int offset = 1; offset <= radius; offset++)
            {
                foreach (int index in new[] { center + offset, center - offset })
                {
                    if (index < 0 || index >= archive.Frames.Count) continue;
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        await cache.GetAsync(index, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception error) when (error is IOException or InvalidDataException or NotSupportedException or ObjectDisposedException)
                    {
                        // A broken frame remains inspectable in the timeline and must not stop read-ahead.
                    }
                    completed++;
                    if (completed % 4 == 0)
                    {
                        _ = Dispatcher.BeginInvoke(() =>
                        {
                            if (ReferenceEquals(cache, _frameCache)) UpdateCacheStatus();
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                if (ReferenceEquals(cache, _frameCache)) StatusText.Text = $"Read-ahead stopped: {error.Message}";
            });
        }
        finally
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                if (ReferenceEquals(cache, _frameCache)) UpdateCacheStatus();
            });
        }
    }

    private void StopPrefetch()
    {
        _prefetchCancellation?.Cancel();
        _prefetchCancellation?.Dispose();
        _prefetchCancellation = null;
    }

    private void UpdateCacheStatus()
    {
        if (_frameCache is not { } cache)
        {
            CacheUsageText.Text = "Cache 0 B / 10 GB";
            return;
        }
        CacheUsageText.Text = $"Cache {FormatBytes(cache.CurrentBytes)} / {FormatBytes(cache.BudgetBytes)} · {cache.Count:N0} frames";
    }

    private static string DefaultDiagnosticsDirectory()
    {
        string path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ExpeditionsMacro",
            "diagnostics");
        return Directory.Exists(path) ? path : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static string FriendlyOutcome(string value) => value.ToLowerInvariant() switch
    {
        "success" => "Success",
        "error" => "Error",
        "canceled" => "Canceled",
        _ => "Unknown",
    };

    private static string FormatRuntime(TimeSpan? runtime) => runtime is null
        ? "unknown"
        : runtime.Value.TotalHours >= 1
            ? runtime.Value.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)
            : runtime.Value.ToString(@"mm\:ss\.fff", System.Globalization.CultureInfo.InvariantCulture);

    private static string FriendlyToken(string value)
    {
        string spaced = value.Replace('_', ' ').Replace('.', ' ');
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
    }

    private static string FormatBytes(long bytes)
    {
        const double gibibyte = 1024d * 1024 * 1024;
        const double mebibyte = 1024d * 1024;
        return bytes >= gibibyte
            ? $"{bytes / gibibyte:0.##} GB"
            : bytes >= mebibyte
                ? $"{bytes / mebibyte:0.#} MB"
                : $"{bytes / 1024d:0.#} KB";
    }

    private sealed record NearbyEventItem(string Time, string Heading, string Details, bool IsCurrentFrame);

    private sealed record PlaybackSpeedOption(double Multiplier, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record CacheBudgetOption(long Bytes, string Label)
    {
        public override string ToString() => Label;
    }
}
