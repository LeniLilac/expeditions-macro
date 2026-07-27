namespace ExpeditionsMacro.Windows;

internal readonly record struct VirtualDesktop(
    int X,
    int Y,
    int Width,
    int Height)
{
    public static VirtualDesktop Read()
    {
        VirtualDesktop desktop = new(
            ManualInputNative.GetSystemMetrics(
                ManualInputNative.SmXVirtualScreen),
            ManualInputNative.GetSystemMetrics(
                ManualInputNative.SmYVirtualScreen),
            ManualInputNative.GetSystemMetrics(
                ManualInputNative.SmCxVirtualScreen),
            ManualInputNative.GetSystemMetrics(
                ManualInputNative.SmCyVirtualScreen));
        if (desktop.Width <= 1 ||
            desktop.Height <= 1)
        {
            throw new InvalidOperationException(
                "Windows did not report a usable virtual desktop.");
        }
        return desktop;
    }

    public int NormalizeX(int screenX) =>
        Normalize(screenX, X, Width);

    public int NormalizeY(int screenY) =>
        Normalize(screenY, Y, Height);

    private static int Normalize(
        int coordinate,
        int origin,
        int extent)
    {
        long relative = Math.Clamp(
            (long)coordinate - origin,
            0,
            extent - 1);
        return checked(
            (int)Math.Round(
                relative * 65_535d /
                (extent - 1)));
    }
}
