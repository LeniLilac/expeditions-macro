using System.Text.Json.Serialization;

namespace ExpeditionsMacro.Core.Geometry;

public readonly record struct ScreenRegion
{
    [JsonConstructor]
    public ScreenRegion(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "A screen region must have positive dimensions.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    [JsonIgnore]
    public int Right => checked(X + Width);

    [JsonIgnore]
    public int Bottom => checked(Y + Height);

    public ScreenRegion Translate(int deltaX, int deltaY) =>
        new(checked(X + deltaX), checked(Y + deltaY), Width, Height);

    public bool FitsWithin(int width, int height) =>
        X >= 0 && Y >= 0 && Right <= width && Bottom <= height;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}
