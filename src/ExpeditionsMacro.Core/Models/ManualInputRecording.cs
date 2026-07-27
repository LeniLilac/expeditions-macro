using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Core.Models;

public enum ManualInputEventKind
{
    Unknown = 0,
    KeyDown,
    KeyUp,
    MouseMove,
    MouseButtonDown,
    MouseButtonUp,
    MouseWheel,
    MouseHorizontalWheel,
}

public enum ManualMouseButton
{
    None = 0,
    Left,
    Right,
    Middle,
    X1,
    X2,
}

public sealed record ManualInputEvent
{
    public required long OffsetMicroseconds { get; init; }

    public required ManualInputEventKind Kind { get; init; }

    public int? ClientX { get; init; }

    public int? ClientY { get; init; }

    public int VirtualKey { get; init; }

    public int ScanCode { get; init; }

    public bool ExtendedKey { get; init; }

    public ManualMouseButton MouseButton { get; init; }

    public int WheelDelta { get; init; }

    internal void Validate(int clientWidth, int clientHeight)
    {
        if (OffsetMicroseconds < 0)
        {
            throw new InvalidDataException(
                "Recorded input offsets cannot be negative.");
        }
        if (!Enum.IsDefined(MouseButton))
        {
            throw new InvalidDataException(
                "Recorded input contains an invalid mouse button.");
        }

        switch (Kind)
        {
            case ManualInputEventKind.KeyDown:
            case ManualInputEventKind.KeyUp:
                ValidateKeyboard();
                break;
            case ManualInputEventKind.MouseMove:
                ValidateMousePosition(clientWidth, clientHeight);
                RequireNoButtonOrWheel();
                break;
            case ManualInputEventKind.MouseButtonDown:
            case ManualInputEventKind.MouseButtonUp:
                ValidateMousePosition(clientWidth, clientHeight);
                if (MouseButton == ManualMouseButton.None)
                {
                    throw new InvalidDataException(
                        "Recorded mouse-button input is missing its button.");
                }
                if (WheelDelta != 0)
                {
                    throw new InvalidDataException(
                        "Recorded mouse-button input cannot contain a wheel delta.");
                }
                break;
            case ManualInputEventKind.MouseWheel:
            case ManualInputEventKind.MouseHorizontalWheel:
                ValidateMousePosition(clientWidth, clientHeight);
                if (WheelDelta == 0)
                {
                    throw new InvalidDataException(
                        "Recorded wheel input must contain a non-zero delta.");
                }
                if (MouseButton != ManualMouseButton.None)
                {
                    throw new InvalidDataException(
                        "Recorded wheel input cannot contain a mouse button.");
                }
                break;
            default:
                throw new InvalidDataException(
                    "The recording contains an unsupported input event.");
        }
    }

    private void ValidateKeyboard()
    {
        if (VirtualKey is < 1 or > 0xFF)
        {
            throw new InvalidDataException(
                "Recorded keyboard input has an invalid virtual key.");
        }
        if (ScanCode is < 1 or > 0x1FF)
        {
            throw new InvalidDataException(
                "Recorded keyboard input has an invalid scan code.");
        }
        if (ClientX is not null ||
            ClientY is not null ||
            MouseButton != ManualMouseButton.None ||
            WheelDelta != 0)
        {
            throw new InvalidDataException(
                "Recorded keyboard input contains mouse data.");
        }
    }

    private void ValidateMousePosition(
        int clientWidth,
        int clientHeight)
    {
        if (ClientX is null ||
            ClientY is null ||
            ClientX < 0 ||
            ClientY < 0 ||
            ClientX >= clientWidth ||
            ClientY >= clientHeight)
        {
            throw new InvalidDataException(
                "Recorded mouse input falls outside the Roblox client.");
        }
        if (VirtualKey != 0 ||
            ScanCode != 0 ||
            ExtendedKey)
        {
            throw new InvalidDataException(
                "Recorded mouse input contains keyboard data.");
        }
    }

    private void RequireNoButtonOrWheel()
    {
        if (MouseButton != ManualMouseButton.None ||
            WheelDelta != 0)
        {
            throw new InvalidDataException(
                "Recorded mouse movement contains button or wheel data.");
        }
    }
}

public sealed record ManualInputRecording
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public int ClientWidth { get; init; } = RobloxClientProfile.Width;

    public int ClientHeight { get; init; } = RobloxClientProfile.Height;

    public required int InitialClientX { get; init; }

    public required int InitialClientY { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } =
        DateTimeOffset.UtcNow;

    public required long DurationMicroseconds { get; init; }

    public IReadOnlyList<ManualInputEvent> Events { get; init; } =
        [];

    public ManualInputRecording CreateImmutableSnapshot()
    {
        Validate();
        ManualInputRecording snapshot =
            this with
            {
                Events = Array.AsReadOnly(
                    Events
                        .Select(input => input with { })
                        .ToArray()),
            };
        snapshot.Validate();
        return snapshot;
    }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported manual recording schema {SchemaVersion}.");
        }
        try
        {
            ValidateId(Id);
        }
        catch (ArgumentException error)
        {
            throw new InvalidDataException(
                "Manual recording id is invalid.",
                error);
        }
        if (string.IsNullOrWhiteSpace(Name) ||
            Name.Trim().Length > 120)
        {
            throw new InvalidDataException(
                "Manual recording name must be between 1 and 120 characters.");
        }
        if (ClientWidth != RobloxClientProfile.Width ||
            ClientHeight != RobloxClientProfile.Height)
        {
            throw new InvalidDataException(
                $"Manual recordings require the canonical {RobloxClientProfile.Width} by {RobloxClientProfile.Height} Roblox client.");
        }
        if (InitialClientX < 0 ||
            InitialClientY < 0 ||
            InitialClientX >= ClientWidth ||
            InitialClientY >= ClientHeight)
        {
            throw new InvalidDataException(
                "Manual recording initial pointer falls outside the Roblox client.");
        }
        if (DurationMicroseconds < 0)
        {
            throw new InvalidDataException(
                "Manual recording duration cannot be negative.");
        }
        if (Events is null ||
            Events.Count == 0)
        {
            throw new InvalidDataException(
                "Manual recording must contain at least one input event.");
        }
        if (CreatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                "Manual recording creation time must be UTC.");
        }

        long previousOffset = -1;
        foreach (ManualInputEvent? input in Events)
        {
            if (input is null)
            {
                throw new InvalidDataException(
                    "Manual recording contains an empty input event.");
            }
            input.Validate(ClientWidth, ClientHeight);
            if (input.OffsetMicroseconds < previousOffset)
            {
                throw new InvalidDataException(
                    "Manual recording events must be ordered by offset.");
            }
            if (input.OffsetMicroseconds > DurationMicroseconds)
            {
                throw new InvalidDataException(
                    "Manual recording event exceeds the saved duration.");
            }
            previousOffset = input.OffsetMicroseconds;
        }
    }

    public static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) ||
            name != id ||
            id is "." or "..")
        {
            throw new ArgumentException(
                "Invalid manual recording id.",
                nameof(id));
        }
        return id;
    }
}
