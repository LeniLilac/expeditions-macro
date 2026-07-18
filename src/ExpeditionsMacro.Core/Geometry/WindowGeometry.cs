namespace ExpeditionsMacro.Core.Geometry;

public readonly record struct WindowBounds(int X, int Y, int Width, int Height)
{
    public ScreenRegion AsRegion() => new(X, Y, Width, Height);
}

public readonly record struct ClientBounds(int X, int Y, int Width, int Height)
{
    public ScreenRegion AsRegion() => new(X, Y, Width, Height);

    public ScreenRegion ToScreen(ScreenRegion relativeRegion) =>
        relativeRegion.Translate(X, Y);

    public (int X, int Y)? ToRelative(int screenX, int screenY)
    {
        int x = screenX - X;
        int y = screenY - Y;
        return x >= 0 && y >= 0 && x < Width && y < Height ? (x, y) : null;
    }
}
