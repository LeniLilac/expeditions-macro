using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Diagnostics;

public sealed class DeepDebugDetectorPack : IDetectorPack
{
    private readonly IDetectorPack _inner;
    private readonly DeepDebugSessionService _debug;

    public DeepDebugDetectorPack(IDetectorPack inner, DeepDebugSessionService debug)
    {
        _inner = inner;
        _debug = debug;
    }

    public DetectorPackManifest Manifest => _inner.Manifest;

    public bool SupportsChallengeMaps => _inner.SupportsChallengeMaps;

    public IReadOnlyDictionary<string, double> ScoreStates(ImageFrame clientImage)
    {
        IReadOnlyDictionary<string, double> scores = _inner.ScoreStates(clientImage);
        _debug.RecordEvent("detector", "state_scores", new { Scores = scores });
        return scores;
    }

    public string? Classify(IReadOnlyDictionary<string, double> scores)
    {
        string? state = _inner.Classify(scores);
        _debug.RecordEvent("detector", "state_classified", new { State = state, Scores = scores });
        return state;
    }

    public string? RecoveryState(ImageFrame clientImage)
    {
        string? state = _inner.RecoveryState(clientImage);
        _debug.RecordEvent("detector", "recovery_state", new { State = state });
        return state;
    }

    public string? CurrentNodeType(ImageFrame clientImage)
    {
        string? state = _inner.CurrentNodeType(clientImage);
        _debug.RecordEvent("detector", "node_type", new { State = state });
        return state;
    }

    public int? SelectedMap(ImageFrame clientImage)
    {
        int? map = _inner.SelectedMap(clientImage);
        _debug.RecordEvent("detector", "selected_map", new { Map = map });
        return map;
    }

    public int? SelectedDifficulty(ImageFrame clientImage)
    {
        int? difficulty = _inner.SelectedDifficulty(clientImage);
        _debug.RecordEvent("detector", "selected_difficulty", new { Difficulty = difficulty });
        return difficulty;
    }

    public IReadOnlyList<int> RemainingUnitKeys(ImageFrame clientImage, IReadOnlySet<int> unitKeys)
    {
        IReadOnlyList<int> remaining = _inner.RemainingUnitKeys(clientImage, unitKeys);
        _debug.RecordEvent("detector", "remaining_unit_keys", new { Requested = unitKeys, Remaining = remaining });
        return remaining;
    }

    public ChallengeMapId? ChallengeMapForType(ImageFrame clientImage, ChallengeType type)
    {
        ChallengeMapId? map = _inner.ChallengeMapForType(clientImage, type);
        _debug.RecordEvent("detector", "challenge_map", new { Type = type, Map = map });
        return map;
    }

    public (int X, int Y) ActionFor(string state, ImageFrame? clientImage = null)
    {
        (int X, int Y) action = _inner.ActionFor(state, clientImage);
        _debug.RecordEvent("detector", "action_for_state", new { State = state, action.X, action.Y });
        return action;
    }
}
