using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private sealed class RefuelTaskStateSession
    {
        private readonly Dictionary<string, ResourceRefuelTarget>
            _completed;

        public RefuelTaskStateSession(MacroPlan plan)
        {
            _completed = plan.Tasks
                .Where(task =>
                    task.Kind == MacroTaskKind.Utility)
                .ToDictionary(
                    task => task.Id,
                    task => plan.ProgressFor(task.Id)
                        .RefuelCompletedTargets,
                    StringComparer.OrdinalIgnoreCase);
        }

        public ResourceRefuelTarget Pending(
            MacroTaskDefinition task) =>
            task.RefuelTarget & ~Completed(task.Id);

        public bool TryRecordPartial(
            MacroTaskDefinition task,
            ResourceRefuelTarget completedTarget,
            out ResourceRefuelTarget completed)
        {
            completed = Completed(task.Id) |
                completedTarget;
            if (completed == task.RefuelTarget)
            {
                return false;
            }
            _completed[task.Id] = completed;
            return true;
        }

        public void Complete(string taskId) =>
            _completed.Remove(taskId);

        private ResourceRefuelTarget Completed(
            string taskId) =>
            _completed.GetValueOrDefault(
                taskId,
                ResourceRefuelTarget.None);
    }
}
