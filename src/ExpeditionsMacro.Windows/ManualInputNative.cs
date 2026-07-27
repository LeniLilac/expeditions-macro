using System.Runtime.InteropServices;

namespace ExpeditionsMacro.Windows;

internal static partial class ManualInputNative
{
    internal const int SmXVirtualScreen = 76;
    internal const int SmYVirtualScreen = 77;
    internal const int SmCxVirtualScreen = 78;
    internal const int SmCyVirtualScreen = 79;

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int index);
}
