namespace ExpeditionsMacro.Automation.Stages;

internal sealed class BountyWaveCompletionTracker
{
    private readonly int _safeExitWave;
    private int _exactStable;
    private int? _lastFallbackWave;
    private int _increasingFallbacks;

    public BountyWaveCompletionTracker(
        StageWaveObjective objective)
    {
        objective.Validate();
        _safeExitWave = objective.SafeExitWave;
    }

    public bool Observe(int? wave)
    {
        if (wave == _safeExitWave)
        {
            _exactStable++;
            return _exactStable >= 2;
        }
        _exactStable = 0;
        if (wave is null ||
            wave < _safeExitWave + 1)
        {
            ResetFallback();
            return false;
        }
        if (_lastFallbackWave is int previous &&
            wave < previous)
        {
            ResetFallback();
        }
        if (_lastFallbackWave is null ||
            wave > _lastFallbackWave)
        {
            _increasingFallbacks++;
            _lastFallbackWave = wave;
        }
        return _increasingFallbacks >= 3;
    }

    private void ResetFallback()
    {
        _lastFallbackWave = null;
        _increasingFallbacks = 0;
    }
}
