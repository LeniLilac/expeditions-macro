using System.Windows;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.DatasetBuilder;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && e.Args[0].Equals("--build", StringComparison.OrdinalIgnoreCase))
        {
            if (e.Args.Length != 4)
            {
                Console.Error.WriteLine("Usage: ExpeditionsMacro.DatasetBuilder --build <datasets> <output> <version>");
                Shutdown(2);
                return;
            }
            try
            {
                DetectorPackBuilder builder = new();
                string result = await builder.BuildAsync(e.Args[1], e.Args[2], e.Args[3], new Progress<string>(Console.WriteLine));
                Console.WriteLine(result);
                Shutdown(0);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                Shutdown(1);
            }
            return;
        }
        new MainWindow().Show();
    }
}
