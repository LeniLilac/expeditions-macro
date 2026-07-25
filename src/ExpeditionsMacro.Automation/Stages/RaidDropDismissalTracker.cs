using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Stages;

internal sealed class RaidDropDismissalTracker
{
    public const int ActionX = 783;
    public const int ActionY = 586;
    private static readonly TimeSpan MinimumClickInterval =
        TimeSpan.FromSeconds(1);
    private readonly bool _enabled;
    private int _visibleFrames;
    private int _missingFrames;
    private bool _gameplayEstablished;
    private DateTimeOffset _nextClickUtc = DateTimeOffset.MinValue;

    public RaidDropDismissalTracker(RaidPreset? raid)
    {
        _enabled = raid?.Act is RaidAct.Act2 or RaidAct.Act3;
    }

    public bool Enabled => _enabled;

    public bool Observe(
        bool afterStartPlacementComplete,
        bool gameplayHudVisible,
        bool terminalCandidateVisible,
        DateTimeOffset now)
    {
        if (!_enabled || !afterStartPlacementComplete)
        {
            ResetObservation();
            return false;
        }

        if (terminalCandidateVisible)
        {
            _missingFrames = 0;
            return false;
        }

        if (gameplayHudVisible)
        {
            _missingFrames = 0;
            _visibleFrames++;
            if (_visibleFrames >= 2) _gameplayEstablished = true;
            return false;
        }

        _visibleFrames = 0;
        if (!_gameplayEstablished) return false;
        _missingFrames++;
        if (_missingFrames < 2 || now < _nextClickUtc) return false;

        _missingFrames = 0;
        _nextClickUtc = now + MinimumClickInterval;
        return true;
    }

    private void ResetObservation()
    {
        _visibleFrames = 0;
        _missingFrames = 0;
        _gameplayEstablished = false;
        _nextClickUtc = DateTimeOffset.MinValue;
    }
}
