using System.Runtime.CompilerServices;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

public sealed partial class CompiledDetectorPack
{
    private sealed class FrameStateScores
    {
        public Dictionary<string, double> Configured { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, double> Adaptive { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly ConditionalWeakTable<ImageFrame, FrameStateScores>
        _frameStateScores = new();

    public IReadOnlyDictionary<string, double> ScoreStates(
        ImageFrame clientImage,
        IReadOnlyCollection<string> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        ValidateClient(clientImage);
        string[] requested = states
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requested.Any(state =>
                state.Equals(
                    "lobby",
                    StringComparison.OrdinalIgnoreCase) ||
                state.Equals(
                    "play",
                    StringComparison.OrdinalIgnoreCase)))
        {
            IReadOnlyDictionary<string, double> all =
                ScoreStates(clientImage);
            return requested
                .Where(all.ContainsKey)
                .ToDictionary(
                    state => state,
                    state => all[state],
                    StringComparer.OrdinalIgnoreCase);
        }

        bool specialized = Manifest.PackId.Equals(
            AnimeExpeditionsDetectorSpec.PackId,
            StringComparison.OrdinalIgnoreCase);
        Dictionary<string, double> scores =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in requested)
        {
            if (specialized &&
                name.Equals(
                    "afk",
                    StringComparison.OrdinalIgnoreCase))
            {
                scores[name] =
                    AfkChamberDetector.Score(clientImage);
                continue;
            }
            if (!_states.TryGetValue(
                    name,
                    out StateRuntime? runtime))
            {
                continue;
            }
            string canonicalName =
                runtime.Definition.Name;
            double score =
                ScoreConfiguredStateCached(
                    canonicalName,
                    runtime,
                    clientImage,
                    specialized);
            if (specialized &&
                canonicalName.Equals(
                    "map_select",
                    StringComparison.OrdinalIgnoreCase))
            {
                score = Math.Max(
                    score,
                    ExpeditionSelectorDetector.Score(
                        clientImage));
            }
            scores[name] = score;
        }
        return scores;
    }

    public string? RootRecoveryState(
        ImageFrame clientImage)
    {
        ValidateClient(clientImage);
        bool specialized =
            Manifest.PackId.Equals(
                AnimeExpeditionsDetectorSpec.PackId,
                StringComparison.OrdinalIgnoreCase);
        if (specialized &&
            AfkChamberDetector.Score(clientImage) >=
                AfkChamberDetector.Threshold)
        {
            return "afk";
        }
        // RecoveryState gives the Expedition selector priority over root
        // recovery. Preserve that boundary so a selector/lobby collision does
        // not authorize a restart.
        if (specialized &&
            ExpeditionSelectorDetector.Score(clientImage) >=
                0.90)
        {
            return null;
        }

        foreach (string name in RootRecoveryStates)
        {
            if (!_states.TryGetValue(
                    name,
                    out StateRuntime? state))
            {
                continue;
            }
            double score = ScoreConfiguredStateCached(
                name,
                state,
                clientImage,
                specialized);
            if (score >= state.Definition.Threshold)
            {
                return name;
            }
        }

        if (specialized &&
            _states.TryGetValue(
                "lobby",
                out StateRuntime? lobby) &&
            ScoreAdaptiveStateCached(
                "lobby",
                lobby,
                clientImage) >= lobby.Definition.Threshold)
        {
            return "lobby";
        }
        return null;
    }

    private double ScoreConfiguredStateCached(
        string name,
        StateRuntime runtime,
        ImageFrame image,
        bool useSpecializedDetectors)
    {
        FrameStateScores frame =
            _frameStateScores.GetOrCreateValue(image);
        lock (frame)
        {
            if (frame.Configured.TryGetValue(
                    name,
                    out double score))
            {
                return score;
            }
            score = ScoreConfiguredState(
                name,
                runtime,
                image,
                useSpecializedDetectors);
            frame.Configured[name] = score;
            return score;
        }
    }

    private double ScoreAdaptiveStateCached(
        string name,
        StateRuntime runtime,
        ImageFrame image)
    {
        FrameStateScores frame =
            _frameStateScores.GetOrCreateValue(image);
        lock (frame)
        {
            if (frame.Adaptive.TryGetValue(
                    name,
                    out double score))
            {
                return score;
            }
            score = ScoreAdaptiveState(
                name,
                runtime,
                image);
            frame.Adaptive[name] = score;
            return score;
        }
    }
}
