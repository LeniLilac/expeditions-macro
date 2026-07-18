namespace ExpeditionsMacro.App.Pages;

public interface IAppPage
{
    Task OnShownAsync();

    Func<Task>? IdleF6Action { get; }
}
