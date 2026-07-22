using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Automation.Camera;

public sealed class CameraAlignmentEngine
{
    private const int MaximumRuntimeAlignmentAttempts = 3;
    private const double StableSceneSimilarity = 0.96;
    private const int StableSceneFrames = 5;
    private const int MaximumStableSceneSamples = 24;
    private const double PoseClampSimilarity = 0.975;
    private const int MaximumPoseClampProbes = 4;
    private const int FinalVerificationFrames = 3;

    private readonly IRobloxAutomation _automation;
    private readonly ICameraModelRepository _models;

    private readonly record struct FullTurnRefinement(int BestOffset, ImageFrame Frame, double Score);

    private readonly record struct VerifiedFullTurnCandidate(int Step, double RefinedScore);

    private readonly record struct FineYawReference(int Offset, ImageFrame Thumbnail);

    private readonly record struct FineYawMatch(int Offset, double Score);

    private readonly record struct AlignmentObservation(double DirectScore, FineYawMatch FineMatch);

    private readonly record struct AlignmentAttemptPlan(CameraYawDirection ScanDirection, int ScanPhasePixels);

    private readonly record struct AtlasMatch(int Index, double Score);

    public CameraAlignmentEngine(IRobloxAutomation automation, ICameraModelRepository models)
    {
        _automation = automation;
        _models = models;
    }

    public async Task<CameraModel> CalibrateAsync(
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        settings.Validate();
        RobloxWindow window = RequireWindow();
        Focus(window);
        ClientBounds initialClient = _automation.GetClientBounds(window);

        bool shiftLockEnabled = false;
        try
        {
            if (initialClient.Width != RobloxClientProfile.Width || initialClient.Height != RobloxClientProfile.Height)
            {
                progress?.Report(new MacroProgress("Camera setup", 1, $"Resizing Roblox to {RobloxClientProfile.Width} × {RobloxClientProfile.Height}."));
                await _automation.ResizeClientAsync(window, RobloxClientProfile.Width, RobloxClientProfile.Height, cancellationToken).ConfigureAwait(false);
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
            ClientBounds client = _automation.GetClientBounds(window);
            if (client.Width != RobloxClientProfile.Width || client.Height != RobloxClientProfile.Height)
            {
                throw new InvalidOperationException($"Roblox did not accept the standard {RobloxClientProfile.Width} × {RobloxClientProfile.Height} client size.");
            }
            await ClampZoomAsync(
                window,
                settings.ZoomTicks,
                settings.SettleMilliseconds,
                regions: null,
                "Camera setup",
                2,
                progress,
                cancellationToken).ConfigureAwait(false);
            await EnableShiftLockAsync(window, "Camera setup", 3, progress, cancellationToken).ConfigureAwait(false);
            shiftLockEnabled = true;
            await ClampPitchAsync(
                window,
                settings.PitchDragPixels,
                settings.SettleMilliseconds,
                regions: null,
                "Camera setup",
                4,
                progress,
                cancellationToken).ConfigureAwait(false);

            List<ImageFrame> goalFrames = [];
            int interval = settings.CaptureCount <= 1
                ? 0
                : (int)Math.Round(settings.CaptureDuration.TotalMilliseconds / (settings.CaptureCount - 1));
            for (int index = 0; index < settings.CaptureCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                goalFrames.Add(_automation.CaptureClient(window));
                progress?.Report(new MacroProgress(
                    "Camera setup",
                    6 + (int)Math.Round(10d * (index + 1) / settings.CaptureCount),
                    $"Capturing goal example {index + 1} of {settings.CaptureCount}"));
                if (index + 1 < settings.CaptureCount) await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(new MacroProgress("Camera setup", 17, "Choosing stable map regions automatically."));
            IReadOnlyList<ScreenRegion> regions = CameraRegionAnalyzer.SelectStableRegions(goalFrames);
            ImageFrame[] composites = goalFrames.Select(frame => CameraRegionAnalyzer.BuildComposite(frame, regions)).ToArray();
            ImageFrame reference = VisionScorer.Median(composites);
            double baseline = Median(composites.Select(frame => VisionScorer.RobustSimilarity(reference, frame)));
            progress?.Report(new MacroProgress(
                "Camera setup",
                21,
                $"Selected {regions.Count} stable regions. Baseline confidence: {baseline:P0}",
                Confidence: baseline));

            IReadOnlyList<FineYawReference> goalNeighborhood = await LearnFineYawNeighborhoodAsync(
                window,
                regions,
                reference,
                settings,
                progress,
                cancellationToken).ConfigureAwait(false);

            (int fullYawSteps, IReadOnlyList<double> scanScores, IReadOnlyList<ImageFrame> atlas) = await LearnFullTurnAsync(
                window,
                regions,
                reference,
                baseline,
                goalNeighborhood,
                settings,
                progress,
                cancellationToken).ConfigureAwait(false);
            double threshold = VisionScorer.ChooseSuccessThreshold(baseline, scanScores);
            string id = ModelId.FromName(settings.Name);
            CameraModelManifest manifest = new()
            {
                Id = id,
                Name = settings.Name.Trim(),
                Regions = regions,
                ClientWidth = client.Width,
                ClientHeight = client.Height,
                BaselineScore = baseline,
                SuccessThreshold = threshold,
                ArrowHoldMilliseconds = settings.ArrowHoldMilliseconds,
                FineStepPixels = settings.FineStepPixels,
                FineSearchPixels = settings.FineSearchPixels,
                FineYawOffsets = goalNeighborhood.Select(item => item.Offset).ToArray(),
                FullYawSteps = fullYawSteps,
                SettleMilliseconds = settings.SettleMilliseconds,
                ZoomTicks = settings.ZoomTicks,
                PitchDragPixels = settings.PitchDragPixels,
                AtlasSampleCount = atlas.Count,
                ScanScores = scanScores,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            ImageFrame goalOverlay = CameraRegionAnalyzer.AnnotateGoal(goalFrames[0], regions);
            CameraModel model = new(
                manifest,
                reference,
                goalOverlay,
                goalNeighborhood.Select(item => item.Thumbnail).ToArray(),
                atlas);
            await _models.SaveAsync(model, cancellationToken).ConfigureAwait(false);
            progress?.Report(new MacroProgress(
                "Camera setup",
                100,
                $"Setup complete. '{manifest.Name}' learned {fullYawSteps} arrow steps per turn across {regions.Count} regions."));
            return model;
        }
        finally
        {
            if (shiftLockEnabled) await DisableShiftLockAsync(window).ConfigureAwait(false);
        }
    }

    public async Task<double> AlignAsync(
        CameraModel model,
        RobloxWindow? existingWindow = null,
        bool manageShiftLock = true,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (manageShiftLock)
        {
            return await PrepareAndAlignAsync(
                model,
                existingWindow,
                model.Manifest.ZoomTicks,
                model.Manifest.PitchDragPixels,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        model.Manifest.Validate();
        RobloxWindow window = existingWindow ?? RequireWindow();
        Focus(window);
        ClientBounds currentClient = _automation.GetClientBounds(window);
        bool shiftLockEnabled = false;
        try
        {
            progress?.Report(new MacroProgress("Camera alignment", 2, "Starting camera alignment."));
            if (currentClient.Width != model.Manifest.ClientWidth || currentClient.Height != model.Manifest.ClientHeight)
            {
                progress?.Report(new MacroProgress("Camera alignment", 4, $"Resizing Roblox to {model.Manifest.ClientWidth} × {model.Manifest.ClientHeight}."));
                await _automation.ResizeClientAsync(window, model.Manifest.ClientWidth, model.Manifest.ClientHeight, cancellationToken).ConfigureAwait(false);
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
            ClientBounds client = _automation.GetClientBounds(window);
            if (client.Width != model.Manifest.ClientWidth || client.Height != model.Manifest.ClientHeight)
            {
                throw new InvalidOperationException("Roblox does not match the client size stored by the camera model.");
            }
            if (manageShiftLock)
            {
                await EnableShiftLockAsync(window, "Camera alignment", 6, progress, cancellationToken).ConfigureAwait(false);
                shiftLockEnabled = true;
            }

            int phase = Math.Max(model.Manifest.FineStepPixels, model.Manifest.FineSearchPixels / 2);
            AlignmentAttemptPlan[] plans =
            [
                new(CameraYawDirection.Right, 0),
                new(CameraYawDirection.Left, phase),
                new(CameraYawDirection.Right, -phase),
            ];
            double bestConfidence = 0;
            for (int attempt = 0; attempt < plans.Length; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new MacroProgress(
                    "Camera alignment",
                    8,
                    $"Alignment attempt {attempt + 1}/{MaximumRuntimeAlignmentAttempts}: waiting for the rendered map to stabilize."));
                await WaitForStableSceneAsync(window, model, attempt + 1, progress, cancellationToken).ConfigureAwait(false);
                double refined = await AlignAttemptAsync(
                    window,
                    model,
                    plans[attempt],
                    attempt + 1,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                bestConfidence = Math.Max(bestConfidence, refined);
                if (refined >= model.Manifest.SuccessThreshold)
                {
                    double verified = await VerifyAlignmentAsync(model, window, progress, cancellationToken).ConfigureAwait(false);
                    bestConfidence = Math.Max(bestConfidence, verified);
                    if (verified >= model.Manifest.SuccessThreshold)
                    {
                        progress?.Report(new MacroProgress(
                            "Camera alignment",
                            100,
                            $"Aligned with {verified:P0} confidence on attempt {attempt + 1}/{MaximumRuntimeAlignmentAttempts}; multi-frame verification passed.",
                            Confidence: verified));
                        return verified;
                    }
                }

                if (attempt + 1 < plans.Length)
                {
                    progress?.Report(new MacroProgress(
                        "Camera alignment",
                        96,
                        $"Attempt {attempt + 1}/{MaximumRuntimeAlignmentAttempts} reached {refined:P0}; retrying from a fresh observation with a different scan direction and phase.",
                        Confidence: refined));
                    await Task.Delay(Math.Max(350, model.Manifest.SettleMilliseconds * 3), cancellationToken).ConfigureAwait(false);
                }
            }

            string failure = $"Camera alignment failed after {MaximumRuntimeAlignmentAttempts} attempts. Best confidence was {bestConfidence:P0}; the model requires {model.Manifest.SuccessThreshold:P0}. Unit placement was not started.";
            progress?.Report(new MacroProgress("Camera alignment", 100, failure, Confidence: bestConfidence));
            throw new CameraAlignmentException(failure, bestConfidence, MaximumRuntimeAlignmentAttempts);
        }
        finally
        {
            if (shiftLockEnabled) await DisableShiftLockAsync(window).ConfigureAwait(false);
        }
    }

    public async Task<double> PrepareAndAlignAsync(
        CameraModel model,
        RobloxWindow? existingWindow,
        int zoomTicks,
        int pitchDragPixels,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        model.Manifest.Validate();
        RobloxWindow window = existingWindow ?? RequireWindow();
        Focus(window);
        await EnsureClientSizeAsync(window, model.Manifest, progress, cancellationToken).ConfigureAwait(false);
        await _automation.MoveCursorToClientCenterAsync(window, cancellationToken).ConfigureAwait(false);
        await ClampZoomAsync(
            window,
            zoomTicks,
            model.Manifest.SettleMilliseconds,
            model.Manifest.Regions,
            "Camera preparation",
            3,
            progress,
            cancellationToken).ConfigureAwait(false);

        // Camera models are captured with shift lock enabled, and the setup flow
        // requires users to begin with it disabled. Enable it before any right-drag
        // so Roblox captures relative motion instead of moving the visible pointer
        // across the hotbar. Always return to the documented disabled state.
        bool shiftLockEnabled = false;
        try
        {
            await EnableShiftLockAsync(window, "Camera preparation", 6, progress, cancellationToken).ConfigureAwait(false);
            shiftLockEnabled = true;
            await ClampPitchAsync(
                window,
                pitchDragPixels,
                model.Manifest.SettleMilliseconds,
                model.Manifest.Regions,
                "Camera preparation",
                7,
                progress,
                cancellationToken).ConfigureAwait(false);
            return await AlignAsync(
                model,
                window,
                manageShiftLock: false,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (shiftLockEnabled) await DisableShiftLockAsync(window).ConfigureAwait(false);
        }
    }

    private async Task<double> AlignAttemptAsync(
        RobloxWindow window,
        CameraModel model,
        AlignmentAttemptPlan plan,
        int attempt,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        ImageFrame currentThumbnail = await CurrentThumbnailAsync(model, window, model.YawAtlas[0].Width, 3, cancellationToken).ConfigureAwait(false);
        AtlasMatch atlasMatch = BestAtlasMatch(model.YawAtlas, currentThumbnail);
        int bestIndex = atlasMatch.Index;
        int fullTurn = model.Manifest.FullYawSteps;
        CameraYawDirection direction;
        int coarseSteps;
        if (bestIndex <= fullTurn / 2)
        {
            direction = CameraYawDirection.Left;
            coarseSteps = bestIndex;
        }
        else
        {
            direction = CameraYawDirection.Right;
            coarseSteps = fullTurn - bestIndex;
        }
        progress?.Report(new MacroProgress(
            "Camera alignment",
            30,
            $"Attempt {attempt}/{MaximumRuntimeAlignmentAttempts} registered yaw-atlas match: {atlasMatch.Score:P0}. Coarse correction: {coarseSteps} {DirectionLabel(direction)} arrow step{(coarseSteps == 1 ? string.Empty : "s")}.",
            Confidence: atlasMatch.Score));
        if (coarseSteps > 0)
        {
            await PulseYawAsync(window, direction, coarseSteps, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        double coarse = await StableScoreAsync(model, window, 3, cancellationToken).ConfigureAwait(false);
        if (coarse < model.Manifest.SuccessThreshold)
        {
            coarse = await RefineWithArrowsAsync(window, model, coarse, cancellationToken).ConfigureAwait(false);
        }
        double refined = await RefineWithMouseAsync(window, model, coarse, 66, "Refining with the saved fine-yaw neighborhood and micro mouse drags.", progress, cancellationToken).ConfigureAwait(false);
        if (refined >= model.Manifest.SuccessThreshold) return refined;

        progress?.Report(new MacroProgress(
            "Camera alignment",
            72,
            $"Attempt {attempt}/{MaximumRuntimeAlignmentAttempts} fast alignment reached only {refined:P0} (target {model.Manifest.SuccessThreshold:P0}). Scanning one full yaw turn {DirectionLabel(plan.ScanDirection)} with a {plan.ScanPhasePixels:+#;-#;0}-px sampling phase.",
            Confidence: refined));
        return await ScanFullTurnAsync(window, model, refined, plan, attempt, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForStableSceneAsync(
        RobloxWindow window,
        CameraModel model,
        int attempt,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        ImageFrame previous = VisionScorer.MakeThumbnail(CaptureComposite(window, model.Manifest.Regions));
        int stable = 0;
        double similarity = 0;
        int delay = Math.Max(75, model.Manifest.SettleMilliseconds);
        for (int sample = 1; sample <= MaximumStableSceneSamples; sample++)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            ImageFrame current = VisionScorer.MakeThumbnail(CaptureComposite(window, model.Manifest.Regions), previous.Width);
            similarity = VisionScorer.RobustSimilarity(previous, current);
            stable = similarity >= StableSceneSimilarity ? stable + 1 : 0;
            previous = current;
            if (stable >= StableSceneFrames)
            {
                progress?.Report(new MacroProgress(
                    "Camera alignment",
                    12,
                    $"Attempt {attempt}/{MaximumRuntimeAlignmentAttempts}: rendered map is stable ({similarity:P0}).",
                    Confidence: similarity));
                return;
            }
        }

        progress?.Report(new MacroProgress(
            "Camera alignment",
            12,
            $"Attempt {attempt}/{MaximumRuntimeAlignmentAttempts}: the scene remained animated ({similarity:P0}); continuing with median observations.",
            Confidence: similarity));
    }

    private async Task EnableShiftLockAsync(
        RobloxWindow window,
        string operation,
        int percent,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new MacroProgress(operation, percent, "Enabling shift lock for stable camera movement."));
        await _automation.MoveCursorToClientCenterAsync(window, cancellationToken).ConfigureAwait(false);
        await Task.Delay(120, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await _automation.TapLeftControlAsync(window, CancellationToken.None).ConfigureAwait(false);
        await Task.Delay(250, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task DisableShiftLockAsync(RobloxWindow window)
    {
        Focus(window);
        await _automation.TapLeftControlAsync(window, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<(int FullYawSteps, IReadOnlyList<double> Scores, IReadOnlyList<ImageFrame> Atlas)> LearnFullTurnAsync(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        ImageFrame reference,
        double baseline,
        IReadOnlyList<FineYawReference> goalNeighborhood,
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<double> scores = [baseline];
        List<double> returnScores = [1];
        List<ImageFrame> atlas = [VisionScorer.MakeThumbnail(reference)];
        bool departed = false;
        double directReturnLevel = Math.Max(0.68, baseline - 0.10);
        double provisionalReturnLevel = Math.Max(0.66, baseline - 0.28);
        double strongRefinedReturnLevel = Math.Max(0.68, directReturnLevel - 0.02);
        double nearExactReturnLevel = Math.Max(directReturnLevel, baseline - 0.025);
        VerifiedFullTurnCandidate? bestVerifiedCandidate = null;
        int scanned = 0;
        progress?.Report(new MacroProgress(
            "Camera setup",
            31,
            $"Learning one full yaw turn against {goalNeighborhood.Count} fine goal views with Right-arrow pulses."));
        for (int step = 1; step <= settings.MaximumSamples; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PulseYawAsync(window, CameraYawDirection.Right, 1, settings.ArrowHoldMilliseconds, settings.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
            scanned = step;
            ImageFrame prepared = CaptureComposite(window, regions);
            double score = VisionScorer.RobustSimilarity(reference, prepared);
            ImageFrame thumbnail = VisionScorer.MakeThumbnail(prepared);
            FineYawMatch returnMatch = BestFineYawMatch(goalNeighborhood, thumbnail);
            atlas.Add(thumbnail);
            scores.Add(score);
            returnScores.Add(returnMatch.Score);
            progress?.Report(new MacroProgress(
                "Camera setup",
                31 + (int)Math.Round(61d * step / settings.MaximumSamples),
                $"Learning yaw sample {step}. Goal: {score:P0}; neighborhood: {returnMatch.Score:P0} ({returnMatch.Offset:+#;-#;0} px).",
                Confidence: returnMatch.Score));
            if (returnMatch.Score < baseline - 0.09) departed = true;
            const int minimumSteps = 12;
            if (departed && step >= minimumSteps && returnMatch.Score >= nearExactReturnLevel)
            {
                FullTurnRefinement refinement = await RefineFullTurnReturnAsync(
                    window,
                    regions,
                    reference,
                    step,
                    score,
                    settings,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                if (refinement.Score >= strongRefinedReturnLevel)
                {
                    return CompleteFullTurn(step, refinement, scores, atlas);
                }
                await MoveMouseAsync(window, -refinement.BestOffset, settings.SettleMilliseconds, settings.FineStepPixels, cancellationToken).ConfigureAwait(false);
            }
            if (departed && returnScores.Count >= 4 && step - 1 >= minimumSteps)
            {
                double previous = returnScores[^2];
                bool localPeak = previous >= returnScores[^3] && previous > returnMatch.Score;
                bool verifiedCandidate = localPeak && previous >= strongRefinedReturnLevel;
                bool continuationVerified = false;
                if (localPeak && !verifiedCandidate && previous >= provisionalReturnLevel)
                {
                    double continuation = VisionScorer.RobustSimilarity(atlas[1], atlas[^1]);
                    double continuationLevel = Math.Max(0.66, Math.Min(0.90, returnMatch.Score + 0.04));
                    if (continuation >= continuationLevel)
                    {
                        verifiedCandidate = true;
                        continuationVerified = true;
                        progress?.Report(new MacroProgress(
                            "Camera setup",
                            23 + (int)Math.Round(70d * step / settings.MaximumSamples),
                            $"Verified full-turn return at {previous:P0} confidence ({continuation:P0} continuation match).",
                            Confidence: previous));
                    }
                }
                if (verifiedCandidate)
                {
                    int candidateStep = step - 1;
                    await PulseYawAsync(window, CameraYawDirection.Left, 1, settings.ArrowHoldMilliseconds, settings.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
                    FullTurnRefinement refinement = await RefineFullTurnReturnAsync(
                        window,
                        regions,
                        reference,
                        candidateStep,
                        previous,
                        settings,
                        progress,
                        cancellationToken).ConfigureAwait(false);
                    if (refinement.Score >= strongRefinedReturnLevel)
                    {
                        return CompleteFullTurn(candidateStep, refinement, scores, atlas);
                    }

                    if (continuationVerified && (bestVerifiedCandidate is null || refinement.Score > bestVerifiedCandidate.Value.RefinedScore))
                    {
                        bestVerifiedCandidate = new VerifiedFullTurnCandidate(candidateStep, refinement.Score);
                    }
                    progress?.Report(new MacroProgress(
                        "Camera setup",
                        23 + (int)Math.Round(70d * step / settings.MaximumSamples),
                        $"Full-turn candidate refined to {refinement.Score:P0}, below the required {strongRefinedReturnLevel:P0}; continuing the scan.",
                        Confidence: refinement.Score));
                    await MoveMouseAsync(window, -refinement.BestOffset, settings.SettleMilliseconds, settings.FineStepPixels, cancellationToken).ConfigureAwait(false);
                    await PulseYawAsync(window, CameraYawDirection.Right, 1, settings.ArrowHoldMilliseconds, settings.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (bestVerifiedCandidate is VerifiedFullTurnCandidate retained)
        {
            progress?.Report(new MacroProgress(
                "Camera setup",
                94,
                $"Sample limit reached. Rechecking the best verified full-turn candidate near arrow step {retained.Step}.",
                Confidence: retained.RefinedScore));
            await PulseYawAsync(
                window,
                CameraYawDirection.Left,
                scanned - retained.Step,
                settings.ArrowHoldMilliseconds,
                settings.SettleMilliseconds,
                cancellationToken).ConfigureAwait(false);
            FullTurnRefinement refinement = await RefineFullTurnReturnAsync(
                window,
                regions,
                reference,
                retained.Step,
                retained.RefinedScore,
                settings,
                progress,
                cancellationToken).ConfigureAwait(false);
            if (refinement.Score >= strongRefinedReturnLevel)
            {
                return CompleteFullTurn(retained.Step, refinement, scores, atlas);
            }
            await MoveMouseAsync(window, -refinement.BestOffset, settings.SettleMilliseconds, settings.FineStepPixels, cancellationToken).ConfigureAwait(false);
            await PulseYawAsync(
                window,
                CameraYawDirection.Left,
                retained.Step,
                settings.ArrowHoldMilliseconds,
                settings.SettleMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            progress?.Report(new MacroProgress("Camera setup", 94, "A complete turn was not recognized. Returning toward the goal."));
            if (scanned > 0)
            {
                await PulseYawAsync(window, CameraYawDirection.Left, scanned, settings.ArrowHoldMilliseconds, settings.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException("Could not recognize a full yaw turn. Reduce Arrow hold, increase maximum arrow samples, or increase settle time, then retry.");
    }

    private async Task<IReadOnlyList<FineYawReference>> LearnFineYawNeighborhoodAsync(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        ImageFrame reference,
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        int fineStep = settings.FineStepPixels;
        int sampleStride = fineStep;
        int radius = Math.Max(fineStep * 2, settings.FineSearchPixels);
        radius = (int)Math.Ceiling((double)radius / sampleStride) * sampleStride;
        int sampleCount = radius * 2 / sampleStride + 1;
        Dictionary<int, FineYawReference> references = new()
        {
            [0] = new FineYawReference(0, VisionScorer.MakeThumbnail(reference)),
        };
        int currentOffset = 0;
        progress?.Report(new MacroProgress(
            "Camera setup",
            22,
            $"Learning a fine goal neighborhood from {-radius} to +{radius} mouse pixels."));

        try
        {
            await MoveMouseAsync(window, -radius, settings.SettleMilliseconds, fineStep, cancellationToken).ConfigureAwait(false);
            currentOffset = -radius;
            for (int index = 0; index < sampleCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index > 0)
                {
                    await MoveMouseAsync(window, sampleStride, settings.SettleMilliseconds, fineStep, cancellationToken).ConfigureAwait(false);
                    currentOffset += sampleStride;
                }
                (ImageFrame frame, double score) = await StablePreparedScoreAsync(reference, window, regions, 1, cancellationToken).ConfigureAwait(false);
                if (currentOffset != 0)
                {
                    references[currentOffset] = new FineYawReference(currentOffset, VisionScorer.MakeThumbnail(frame));
                }
                progress?.Report(new MacroProgress(
                    "Camera setup",
                    22 + (int)Math.Round(7d * (index + 1) / sampleCount),
                    $"Fine goal view {currentOffset:+#;-#;0} px. Confidence: {score:P0}",
                    Confidence: score));
            }
        }
        finally
        {
            if (currentOffset != 0)
            {
                await MoveMouseAsync(window, -currentOffset, settings.SettleMilliseconds, fineStep, CancellationToken.None).ConfigureAwait(false);
            }
        }

        FineYawReference[] result = references.Values.OrderBy(item => item.Offset).ToArray();
        progress?.Report(new MacroProgress(
            "Camera setup",
            30,
            $"Fine goal neighborhood ready with {result.Length} distinct yaw views; starting the coarse turn."));
        return result;
    }

    private static FineYawMatch BestFineYawMatch(IReadOnlyList<FineYawReference> references, ImageFrame currentThumbnail)
    {
        FineYawReference best = references[0];
        double bestScore = double.NegativeInfinity;
        foreach (FineYawReference candidate in references)
        {
            double score = VisionScorer.RobustSimilarity(candidate.Thumbnail, currentThumbnail);
            if (score <= bestScore) continue;
            best = candidate;
            bestScore = score;
        }
        return new FineYawMatch(best.Offset, bestScore);
    }

    private async Task<FullTurnRefinement> RefineFullTurnReturnAsync(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        ImageFrame reference,
        int coarseYawSteps,
        double coarseScore,
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        int fineStep = settings.FineStepPixels;
        int radius = Math.Max(fineStep * 2, settings.FineSearchPixels);
        int stepsPerSide = (int)Math.Ceiling((double)radius / fineStep);
        radius = stepsPerSide * fineStep;
        int sampleCount = stepsPerSide * 2 + 1;
        progress?.Report(new MacroProgress(
            "Camera setup",
            94,
            $"Full turn found near arrow step {coarseYawSteps}. Refining with {fineStep}-px mouse drags.",
            Confidence: coarseScore));

        await MoveMouseAsync(window, -radius, settings.SettleMilliseconds, fineStep, cancellationToken).ConfigureAwait(false);
        int bestOffset = 0;
        double bestScore = double.NegativeInfinity;
        ImageFrame? bestFrame = null;
        for (int index = 0; index < sampleCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index > 0)
            {
                await MoveMouseAsync(window, fineStep, settings.SettleMilliseconds, fineStep, cancellationToken).ConfigureAwait(false);
            }
            int offset = -radius + index * fineStep;
            (ImageFrame frame, double score) = await StablePreparedScoreAsync(reference, window, regions, 2, cancellationToken).ConfigureAwait(false);
            if (score > bestScore)
            {
                bestScore = score;
                bestOffset = offset;
                bestFrame = frame;
            }
            progress?.Report(new MacroProgress(
                "Camera setup",
                94 + (int)Math.Round(3d * (index + 1) / sampleCount),
                $"Fine yaw {offset:+#;-#;0} px. Best confidence: {bestScore:P0}",
                Confidence: bestScore));
        }

        int currentOffset = radius;
        if (bestOffset != currentOffset)
        {
            await MoveMouseAsync(window, bestOffset - currentOffset, settings.SettleMilliseconds, fineStep, cancellationToken).ConfigureAwait(false);
        }
        (ImageFrame refinedFrame, double refinedScore) = await StablePreparedScoreAsync(reference, window, regions, 3, cancellationToken).ConfigureAwait(false);
        if (bestFrame is not null && bestScore > refinedScore)
        {
            refinedFrame = bestFrame;
            refinedScore = bestScore;
        }
        progress?.Report(new MacroProgress(
            "Camera setup",
            98,
            $"Refined the full-turn return by {bestOffset:+#;-#;0} mouse pixels at {refinedScore:P0} confidence.",
            Confidence: refinedScore));
        return new FullTurnRefinement(bestOffset, refinedFrame, refinedScore);
    }

    private static (int FullYawSteps, IReadOnlyList<double> Scores, IReadOnlyList<ImageFrame> Atlas) CompleteFullTurn(
        int candidateStep,
        FullTurnRefinement refinement,
        List<double> scores,
        List<ImageFrame> atlas)
    {
        int retainedCount = candidateStep + 1;
        if (scores.Count > retainedCount) scores.RemoveRange(retainedCount, scores.Count - retainedCount);
        if (atlas.Count > retainedCount) atlas.RemoveRange(retainedCount, atlas.Count - retainedCount);
        scores[candidateStep] = refinement.Score;
        atlas[candidateStep] = VisionScorer.MakeThumbnail(refinement.Frame);
        return (candidateStep, scores, atlas);
    }

    private async Task<double> ScanFullTurnAsync(
        RobloxWindow window,
        CameraModel model,
        double startingScore,
        AlignmentAttemptPlan plan,
        int attempt,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken,
        bool allowNeighborhoodShortcut = true)
    {
        int fullTurn = model.Manifest.FullYawSteps;
        int bestOffset = 0;
        double bestEvidence = startingScore;
        double earlyExitThreshold = Math.Max(model.Manifest.SuccessThreshold, model.Manifest.BaselineScore - 0.025);
        double neighborhoodTestThreshold = Math.Clamp(model.Manifest.SuccessThreshold + 0.08, 0.80, 0.92);

        if (plan.ScanPhasePixels != 0)
        {
            await MoveMouseAsync(
                window,
                plan.ScanPhasePixels,
                model.Manifest.SettleMilliseconds,
                model.Manifest.FineStepPixels,
                cancellationToken).ConfigureAwait(false);
        }
        AlignmentObservation origin = await StableObservationAsync(model, window, 3, cancellationToken).ConfigureAwait(false);
        double originEvidence = Math.Max(origin.DirectScore, origin.FineMatch.Score);
        if (originEvidence > bestEvidence)
        {
            bestEvidence = originEvidence;
        }

        for (int travelled = 1; travelled <= fullTurn; travelled++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PulseYawAsync(window, plan.ScanDirection, 1, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
            AlignmentObservation observation = await StableObservationAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
            double score = observation.DirectScore;
            double evidence = Math.Max(score, observation.FineMatch.Score);
            int normalizedOffset = travelled == fullTurn ? 0 : travelled;
            if (evidence > bestEvidence)
            {
                bestEvidence = evidence;
                bestOffset = normalizedOffset;
            }

            progress?.Report(new MacroProgress(
                "Camera alignment",
                76 + (int)Math.Round(16d * travelled / fullTurn),
                $"Attempt {attempt}/{MaximumRuntimeAlignmentAttempts} full-turn scan {travelled}/{fullTurn} {DirectionLabel(plan.ScanDirection)} steps. Best goal/neighborhood evidence: {bestEvidence:P0}.",
                Confidence: bestEvidence));

            if (score >= earlyExitThreshold)
            {
                double verified = await StableScoreAsync(model, window, 3, cancellationToken).ConfigureAwait(false);
                if (verified >= earlyExitThreshold)
                {
                    progress?.Report(new MacroProgress("Camera alignment", 94, $"Stable goal found during full-turn scan at {verified:P0} confidence.", Confidence: verified));
                    return await RefineWithMouseAsync(window, model, verified, 96, "Refining the full-turn match with micro mouse drags.", progress, cancellationToken).ConfigureAwait(false);
                }
            }

            // A fine-atlas hit is a candidate, not a success score: registered
            // matching can confuse a different coarse yaw with a nearby goal view.
            // Testing one strong fine-atlas candidate is safe even when session
            // rendering lowers it below the calibration baseline: the corrected
            // pose must still pass the unchanged direct goal threshold. A false
            // candidate consumes this shortcut and restarts one complete scan.
            if (allowNeighborhoodShortcut && observation.FineMatch.Score >= neighborhoodTestThreshold)
            {
                progress?.Report(new MacroProgress(
                    "Camera alignment",
                    94,
                    $"Strong saved fine-yaw neighborhood found after {travelled} {DirectionLabel(plan.ScanDirection)} steps ({observation.FineMatch.Score:P0}). Testing it before continuing the full turn.",
                    Confidence: observation.FineMatch.Score));
                double neighborhood = await RefineSavedNeighborhoodCandidateAsync(
                    window,
                    model,
                    observation.FineMatch,
                    96,
                    "Refining the saved fine-yaw neighborhood before continuing the full-turn scan.",
                    progress,
                    cancellationToken).ConfigureAwait(false);
                if (neighborhood >= model.Manifest.SuccessThreshold)
                {
                    progress?.Report(new MacroProgress(
                        "Camera alignment",
                        97,
                        $"Saved fine-yaw neighborhood resolved the goal at {neighborhood:P0}; skipped the remaining full-turn scan.",
                        Confidence: neighborhood));
                    return neighborhood;
                }

                progress?.Report(new MacroProgress(
                    "Camera alignment",
                    76,
                    $"Saved fine-yaw neighborhood reached only {neighborhood:P0}; restarting a complete scan from the current pose.",
                    Confidence: neighborhood));
                // Candidate refinement already consumed this attempt's sampling
                // phase. A restarted turn begins at the resulting pose and must
                // not apply that mouse offset a second time.
                return await ScanFullTurnAsync(
                    window,
                    model,
                    neighborhood,
                    plan with { ScanPhasePixels = 0 },
                    attempt,
                    progress,
                    cancellationToken,
                    allowNeighborhoodShortcut: false).ConfigureAwait(false);
            }
        }

        CameraYawDirection direction = bestOffset <= fullTurn / 2 ? plan.ScanDirection : Opposite(plan.ScanDirection);
        int correction = bestOffset <= fullTurn / 2 ? bestOffset : fullTurn - bestOffset;
        progress?.Report(new MacroProgress(
            "Camera alignment",
            94,
            $"Attempt {attempt}/{MaximumRuntimeAlignmentAttempts} full-turn scan complete. Returning {correction} {DirectionLabel(direction)} arrow step{(correction == 1 ? string.Empty : "s")} to the best saved-neighborhood candidate ({bestEvidence:P0}).",
            Confidence: bestEvidence));
        if (correction > 0)
        {
            await PulseYawAsync(window, direction, correction, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        AlignmentObservation returnedCandidate = await StableObservationAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
        progress?.Report(new MacroProgress(
            "Camera alignment",
            95,
            $"Re-observed the returned full-turn candidate: goal {returnedCandidate.DirectScore:P0}; fine neighborhood {returnedCandidate.FineMatch.Score:P0} at {returnedCandidate.FineMatch.Offset:+#;-#;0} px.",
            Confidence: Math.Max(returnedCandidate.DirectScore, returnedCandidate.FineMatch.Score)));
        return await RefineSavedNeighborhoodCandidateAsync(
            window,
            model,
            returnedCandidate.FineMatch,
            96,
            "Refining the best full-turn candidate with micro mouse drags.",
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<double> RefineSavedNeighborhoodCandidateAsync(
        RobloxWindow window,
        CameraModel model,
        FineYawMatch fineMatch,
        int progressPercent,
        string progressMessage,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (fineMatch.Score >= 0.52 && fineMatch.Offset != 0)
        {
            await MoveMouseAsync(
                window,
                -fineMatch.Offset,
                model.Manifest.SettleMilliseconds,
                model.Manifest.FineStepPixels,
                cancellationToken).ConfigureAwait(false);
        }
        double candidate = await StableScoreAsync(model, window, 3, cancellationToken).ConfigureAwait(false);
        if (candidate < model.Manifest.SuccessThreshold)
        {
            candidate = await RefineWithArrowsAsync(window, model, candidate, cancellationToken).ConfigureAwait(false);
        }
        return await RefineWithMouseAsync(
            window,
            model,
            candidate,
            progressPercent,
            progressMessage,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<AlignmentObservation> StableObservationAsync(
        CameraModel model,
        RobloxWindow window,
        int count,
        CancellationToken cancellationToken)
    {
        List<ImageFrame> frames = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(CaptureComposite(window, model.Manifest.Regions));
            if (index + 1 < count) await Task.Delay(60, cancellationToken).ConfigureAwait(false);
        }
        ImageFrame stable = VisionScorer.Median(frames);
        ImageFrame thumbnail = VisionScorer.MakeThumbnail(stable, model.FineYawAtlas[0].Width);
        return new AlignmentObservation(
            CameraRegisteredScorer.ScoreComposite(model.Reference, stable).Score,
            BestFineYawMatch(model, thumbnail));
    }

    private async Task<double> RefineWithArrowsAsync(
        RobloxWindow window,
        CameraModel model,
        double startingScore,
        CancellationToken cancellationToken)
    {
        double best = startingScore;
        await PulseYawAsync(window, CameraYawDirection.Left, 1, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
        double left = await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
        await PulseYawAsync(window, CameraYawDirection.Right, 1, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
        await PulseYawAsync(window, CameraYawDirection.Right, 1, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
        double right = await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
        await PulseYawAsync(window, CameraYawDirection.Left, 1, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);

        // Keyboard pulse duration is calibrated, but the number of Roblox render
        // frames that consume a pulse can still vary. Re-observe the actual pose
        // after the nominal Left/Right round trip instead of trusting the score
        // cached before those probes.
        best = await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);

        CameraYawDirection? direction = null;
        double neighbor = best;
        if (left > neighbor + 0.006) { direction = CameraYawDirection.Left; neighbor = left; }
        if (right > neighbor + 0.006) { direction = CameraYawDirection.Right; neighbor = right; }
        if (direction is null) return best;

        await PulseYawAsync(window, direction.Value, 1, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
        double placed = await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
        if (placed <= best + 0.003)
        {
            await PulseYawAsync(window, Opposite(direction.Value), 1, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
            return await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
        }
        best = placed;
        for (int index = 0; index < 3; index++)
        {
            await PulseYawAsync(window, direction.Value, 1, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
            double score = await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
            if (score <= best + 0.003)
            {
                await PulseYawAsync(window, Opposite(direction.Value), 1, model.Manifest.ArrowHoldMilliseconds, model.Manifest.SettleMilliseconds, cancellationToken).ConfigureAwait(false);
                return await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
            }
            best = score;
        }
        return best;
    }

    private async Task<double> RefineWithMouseAsync(
        RobloxWindow window,
        CameraModel model,
        double startingScore,
        int progressPercent,
        string progressMessage,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new MacroProgress("Camera alignment", progressPercent, progressMessage));
        int fine = Math.Max(1, model.Manifest.FineStepPixels);
        double best = await ApplyPersistedFineYawAsync(
            window,
            model,
            startingScore,
            progressPercent,
            progress,
            cancellationToken).ConfigureAwait(false);
        await MoveMouseAsync(window, -fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        double left = Score(model, window);
        await MoveMouseAsync(window, fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        await MoveMouseAsync(window, fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        double right = Score(model, window);
        await MoveMouseAsync(window, -fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        best = await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
        int direction = 0;
        double neighbor = best;
        if (left > neighbor + 0.003) { direction = -1; neighbor = left; }
        if (right > neighbor + 0.003) { direction = 1; neighbor = right; }
        if (direction == 0) return best;
        await MoveMouseAsync(window, direction * fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        double placed = await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
        if (placed <= best + 0.003)
        {
            await MoveMouseAsync(window, -direction * fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
            return await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
        }
        best = placed;
        int maximum = Math.Max(8, model.Manifest.FineSearchPixels / fine);
        for (int index = 0; index < maximum; index++)
        {
            await MoveMouseAsync(window, direction * fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
            double score = Score(model, window);
            if (score <= best + 0.0015)
            {
                await MoveMouseAsync(window, -direction * fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
                return await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
            }
            best = score;
        }
        return best;
    }

    private async Task<double> ApplyPersistedFineYawAsync(
        RobloxWindow window,
        CameraModel model,
        double startingScore,
        int progressPercent,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        ImageFrame currentThumbnail = await CurrentThumbnailAsync(
            model,
            window,
            model.FineYawAtlas[0].Width,
            2,
            cancellationToken).ConfigureAwait(false);
        FineYawMatch match = BestFineYawMatch(model, currentThumbnail);
        if (match.Offset == 0 || match.Score < 0.52) return startingScore;

        progress?.Report(new MacroProgress(
            "Camera alignment",
            progressPercent,
            $"Fine atlas match: {match.Score:P0} at {match.Offset:+#;-#;0} px. Applying the saved correction.",
            Confidence: match.Score));
        await MoveMouseAsync(
            window,
            -match.Offset,
            model.Manifest.SettleMilliseconds,
            model.Manifest.FineStepPixels,
            cancellationToken).ConfigureAwait(false);
        double corrected = await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
        if (corrected + 0.004 >= startingScore) return corrected;

        await MoveMouseAsync(
            window,
            match.Offset,
            model.Manifest.SettleMilliseconds,
            model.Manifest.FineStepPixels,
            cancellationToken).ConfigureAwait(false);
        return await StableScoreAsync(model, window, 2, cancellationToken).ConfigureAwait(false);
    }

    private static FineYawMatch BestFineYawMatch(CameraModel model, ImageFrame currentThumbnail)
    {
        int bestIndex = 0;
        double bestScore = double.NegativeInfinity;
        for (int index = 0; index < model.FineYawAtlas.Count; index++)
        {
            double score = CameraRegisteredScorer.Score(model.FineYawAtlas[index], currentThumbnail).Score;
            if (score <= bestScore) continue;
            bestIndex = index;
            bestScore = score;
        }
        return new FineYawMatch(model.Manifest.FineYawOffsets[bestIndex], bestScore);
    }

    private async Task<ImageFrame> CurrentThumbnailAsync(
        CameraModel model,
        RobloxWindow window,
        int width,
        int count,
        CancellationToken cancellationToken)
    {
        List<ImageFrame> frames = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(CaptureComposite(window, model.Manifest.Regions));
            if (index + 1 < count) await Task.Delay(60, cancellationToken).ConfigureAwait(false);
        }
        return VisionScorer.MakeThumbnail(VisionScorer.Median(frames), width);
    }

    private async Task<double> StableScoreAsync(CameraModel model, RobloxWindow window, int count, CancellationToken cancellationToken)
    {
        List<double> scores = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scores.Add(Score(model, window));
            if (index + 1 < count) await Task.Delay(60, cancellationToken).ConfigureAwait(false);
        }
        double[] sorted = scores.Order().ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    private async Task<(ImageFrame Frame, double Score)> StablePreparedScoreAsync(
        ImageFrame reference,
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        int count,
        CancellationToken cancellationToken)
    {
        List<ImageFrame> frames = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(CaptureComposite(window, regions));
            if (index + 1 < count) await Task.Delay(60, cancellationToken).ConfigureAwait(false);
        }
        ImageFrame stable = VisionScorer.Median(frames);
        return (stable, VisionScorer.RobustSimilarity(reference, stable));
    }

    private ImageFrame CaptureComposite(RobloxWindow window, IReadOnlyList<ScreenRegion> regions) =>
        CameraRegionAnalyzer.BuildComposite(_automation.CaptureClient(window), regions);

    private double Score(CameraModel model, RobloxWindow window) =>
        GoalEvidence(model, _automation.CaptureClient(window));

    private async Task EnsureClientSizeAsync(
        RobloxWindow window,
        CameraModelManifest manifest,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        ClientBounds current = _automation.GetClientBounds(window);
        if (current.Width != manifest.ClientWidth || current.Height != manifest.ClientHeight)
        {
            progress?.Report(new MacroProgress("Camera alignment", 1, $"Resizing Roblox to {manifest.ClientWidth} × {manifest.ClientHeight}."));
            await _automation.ResizeClientAsync(window, manifest.ClientWidth, manifest.ClientHeight, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        ClientBounds resized = _automation.GetClientBounds(window);
        if (resized.Width != manifest.ClientWidth || resized.Height != manifest.ClientHeight)
        {
            throw new InvalidOperationException("Roblox does not match the client size stored by the camera model.");
        }
    }

    private async Task ClampZoomAsync(
        RobloxWindow window,
        int zoomTicks,
        int settleMilliseconds,
        IReadOnlyList<ScreenRegion>? regions,
        string operation,
        int percent,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        int batch = Math.Clamp(zoomTicks, 5, 80);
        progress?.Report(new MacroProgress(operation, percent, "Zooming out until the rendered view stops changing."));
        Focus(window);
        await _automation.ZoomOutFullyAsync(window, batch, cancellationToken).ConfigureAwait(false);
        await Task.Delay(Math.Max(75, settleMilliseconds), cancellationToken).ConfigureAwait(false);
        ImageFrame previous = CapturePoseThumbnail(window, regions);
        double similarity = 0;
        for (int probe = 1; probe <= MaximumPoseClampProbes; probe++)
        {
            await _automation.ZoomOutFullyAsync(window, batch, cancellationToken).ConfigureAwait(false);
            await Task.Delay(Math.Max(75, settleMilliseconds), cancellationToken).ConfigureAwait(false);
            ImageFrame current = CapturePoseThumbnail(window, regions);
            similarity = CameraRegisteredScorer.Score(previous, current, maximumTranslation: 2).Score;
            if (similarity >= PoseClampSimilarity)
            {
                progress?.Report(new MacroProgress(operation, percent, $"Zoom clamp verified at {similarity:P0} frame agreement.", Confidence: similarity));
                return;
            }
            previous = current;
        }
        progress?.Report(new MacroProgress(operation, percent, $"Zoom received the maximum extra zoom passes; the scene remained animated ({similarity:P0}).", Confidence: similarity));
    }

    private async Task ClampPitchAsync(
        RobloxWindow window,
        int pitchDragPixels,
        int settleMilliseconds,
        IReadOnlyList<ScreenRegion>? regions,
        string operation,
        int percent,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        int initial = Math.Clamp(pitchDragPixels, 300, 5000);
        int probePixels = Math.Clamp(initial / 3, 450, 900);
        progress?.Report(new MacroProgress(operation, percent, "Dragging downward until the top-down pitch stops changing."));
        Focus(window);
        await _automation.DragCameraAsync(window, 0, initial, 90, cancellationToken).ConfigureAwait(false);
        await Task.Delay(Math.Max(75, settleMilliseconds), cancellationToken).ConfigureAwait(false);
        ImageFrame previous = CapturePoseThumbnail(window, regions);
        double similarity = 0;
        for (int probe = 1; probe <= MaximumPoseClampProbes; probe++)
        {
            await _automation.DragCameraAsync(window, 0, probePixels, 90, cancellationToken).ConfigureAwait(false);
            await Task.Delay(Math.Max(75, settleMilliseconds), cancellationToken).ConfigureAwait(false);
            ImageFrame current = CapturePoseThumbnail(window, regions);
            similarity = CameraRegisteredScorer.Score(previous, current, maximumTranslation: 2).Score;
            if (similarity >= PoseClampSimilarity)
            {
                progress?.Report(new MacroProgress(operation, percent, $"Top-down pitch clamp verified at {similarity:P0} frame agreement.", Confidence: similarity));
                return;
            }
            previous = current;
        }
        progress?.Report(new MacroProgress(operation, percent, $"Pitch received the maximum extra downward drags; the scene remained animated ({similarity:P0}).", Confidence: similarity));
    }

    private ImageFrame CapturePoseThumbnail(RobloxWindow window, IReadOnlyList<ScreenRegion>? regions)
    {
        ImageFrame frame = _automation.CaptureClient(window);
        if (regions is null) return VisionScorer.PrepareGray(frame, 160, 101);
        return VisionScorer.MakeThumbnail(CameraRegionAnalyzer.BuildComposite(frame, regions), 160);
    }

    private static AtlasMatch BestAtlasMatch(IReadOnlyList<ImageFrame> atlas, ImageFrame current)
    {
        if (atlas.Count == 0) throw new ArgumentException("The yaw atlas is empty.", nameof(atlas));
        double[] raw = atlas.Select(frame => VisionScorer.RobustSimilarity(frame, current)).ToArray();
        int[] candidates = raw
            .Select((score, index) => (Score: score, Index: index))
            .OrderByDescending(item => item.Score)
            .Take(Math.Min(8, atlas.Count))
            .Select(item => item.Index)
            .ToArray();
        AtlasMatch best = new(candidates[0], double.NegativeInfinity);
        foreach (int index in candidates)
        {
            double score = CameraRegisteredScorer.Score(atlas[index], current).Score;
            if (score > best.Score) best = new AtlasMatch(index, score);
        }
        return best;
    }

    private static double GoalEvidence(CameraModel model, ImageFrame fullClient)
    {
        ImageFrame gray = CameraRegionAnalyzer.BuildComposite(fullClient, model.Manifest.Regions);
        double registered = CameraRegisteredScorer.ScoreComposite(model.Reference, gray).Score;
        ImageFrame goalColor = CameraRegionAnalyzer.BuildColorComposite(model.GoalOverlay, model.Manifest.Regions, inset: 4);
        ImageFrame currentColor = CameraRegionAnalyzer.BuildColorComposite(fullClient, model.Manifest.Regions, inset: 4);
        double hue = CameraRegisteredScorer.HueSimilarity(goalColor, currentColor);
        // Hue is deliberately a weak reranker. Geometry remains authoritative,
        // while consistent map colors can separate close registered candidates.
        return Math.Clamp(registered + 0.04 * (hue - 0.5), 0, 1);
    }

    private async Task<double> VerifyAlignmentAsync(
        CameraModel model,
        RobloxWindow window,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new MacroProgress("Camera alignment", 98, "Verifying the final yaw across three independently rendered frames."));
        List<double> scores = [];
        for (int frame = 0; frame < FinalVerificationFrames; frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scores.Add(Score(model, window));
            if (frame + 1 < FinalVerificationFrames)
            {
                await Task.Delay(Math.Max(100, model.Manifest.SettleMilliseconds), cancellationToken).ConfigureAwait(false);
            }
        }
        double verified = Median(scores);
        int passing = scores.Count(score => score >= model.Manifest.SuccessThreshold);
        progress?.Report(new MacroProgress(
            "Camera alignment",
            99,
            $"Final verification: {passing}/{FinalVerificationFrames} frames passed; median confidence {verified:P0}.",
            Confidence: verified));
        return passing >= 2
            ? verified
            : Math.Min(verified, Math.Max(0, model.Manifest.SuccessThreshold - 0.001));
    }

    private async Task PulseYawAsync(
        RobloxWindow window,
        CameraYawDirection direction,
        int count,
        int holdMilliseconds,
        int settleMilliseconds,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _automation.PulseCameraYawAsync(window, direction, holdMilliseconds, cancellationToken).ConfigureAwait(false);
            if (index + 1 < count) await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }
        if (count > 0) await Task.Delay(settleMilliseconds, cancellationToken).ConfigureAwait(false);
    }

    private async Task MoveMouseAsync(
        RobloxWindow window,
        int horizontalPixels,
        int settleMilliseconds,
        int chunkPixels,
        CancellationToken cancellationToken)
    {
        await _automation.DragCameraAsync(window, horizontalPixels, 0, chunkPixels, cancellationToken).ConfigureAwait(false);
        await Task.Delay(settleMilliseconds, cancellationToken).ConfigureAwait(false);
    }

    private RobloxWindow RequireWindow() => _automation.FindWindow() ?? throw new InvalidOperationException("No visible Roblox window was found.");

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new InvalidOperationException($"Found '{window.Title}', but Windows could not focus it. Restore Roblox and try again.");
    }

    private static string DirectionLabel(CameraYawDirection direction) => direction == CameraYawDirection.Left ? "left" : "right";

    private static CameraYawDirection Opposite(CameraYawDirection direction) =>
        direction == CameraYawDirection.Left ? CameraYawDirection.Right : CameraYawDirection.Left;

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.Order().ToArray();
        if (sorted.Length == 0) throw new ArgumentException("At least one score is required.", nameof(values));
        return sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }
}
