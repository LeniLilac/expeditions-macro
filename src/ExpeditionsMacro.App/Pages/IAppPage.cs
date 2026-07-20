namespace ExpeditionsMacro.App.Pages;

public interface IAppPage
{
    Task OnShownAsync();

    Func<Task>? IdleHotkeyAction { get; }
}
