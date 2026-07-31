using ExpeditionsMacro.Core.Abstractions;

namespace ExpeditionsMacro.Automation.Teams;

public sealed class TeamOperationSession
{
    private RobloxWindow? _window;
    private int? _teamSlot;

    public bool IsLoaded(
        RobloxWindow window,
        int teamSlot)
    {
        ValidateTeamSlot(teamSlot);
        if (teamSlot == 0)
        {
            return true;
        }
        if (_window is not RobloxWindow loadedWindow ||
            !SameProcess(loadedWindow, window))
        {
            Invalidate();
            return false;
        }
        return _teamSlot == teamSlot;
    }

    public void MarkLoaded(
        RobloxWindow window,
        int teamSlot)
    {
        ValidateTeamSlot(teamSlot);
        if (teamSlot == 0)
        {
            return;
        }
        _window = window;
        _teamSlot = teamSlot;
    }

    public void BeginSelection(int teamSlot)
    {
        ValidateTeamSlot(teamSlot);
        if (teamSlot > 0)
        {
            Invalidate();
        }
    }

    public void Invalidate()
    {
        _window = null;
        _teamSlot = null;
    }

    private static bool SameProcess(
        RobloxWindow left,
        RobloxWindow right) =>
        left.ProcessId > 0 &&
        right.ProcessId > 0
            ? left.ProcessId == right.ProcessId
            : left.Handle == right.Handle;

    private static void ValidateTeamSlot(
        int teamSlot)
    {
        if (teamSlot is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(teamSlot));
        }
    }
}
