using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpeditionsMacro.Core.Models;

[JsonConverter(typeof(UnitAutoUpgradePriorityJsonConverter))]
public enum UnitAutoUpgradePriority
{
    Off = 0,
    Priority1 = 1,
    Priority2 = 2,
    Priority3 = 3,
    Priority4 = 4,
    Priority5 = 5,
    Priority6 = 6,
}

public sealed class UnitAutoUpgradePriorityJsonConverter :
    JsonConverter<UnitAutoUpgradePriority>
{
    public override UnitAutoUpgradePriority Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.False =>
                UnitAutoUpgradePriority.Off,
            JsonTokenType.True =>
                UnitAutoUpgradePriority.Priority1,
            JsonTokenType.String =>
                Parse(reader.GetString()),
            _ => throw new JsonException(
                "Auto Upgrade priority must be Off or Priority 1 through Priority 6."),
        };

    public override void Write(
        Utf8JsonWriter writer,
        UnitAutoUpgradePriority value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(
            value switch
            {
                UnitAutoUpgradePriority.Off =>
                    "off",
                UnitAutoUpgradePriority.Priority1 =>
                    "priority_1",
                UnitAutoUpgradePriority.Priority2 =>
                    "priority_2",
                UnitAutoUpgradePriority.Priority3 =>
                    "priority_3",
                UnitAutoUpgradePriority.Priority4 =>
                    "priority_4",
                UnitAutoUpgradePriority.Priority5 =>
                    "priority_5",
                UnitAutoUpgradePriority.Priority6 =>
                    "priority_6",
                _ => throw new JsonException(
                    "Auto Upgrade priority is invalid."),
            });

    private static UnitAutoUpgradePriority Parse(
        string? value) =>
        value switch
        {
            "off" =>
                UnitAutoUpgradePriority.Off,
            "priority_1" =>
                UnitAutoUpgradePriority.Priority1,
            "priority_2" =>
                UnitAutoUpgradePriority.Priority2,
            "priority_3" =>
                UnitAutoUpgradePriority.Priority3,
            "priority_4" =>
                UnitAutoUpgradePriority.Priority4,
            "priority_5" =>
                UnitAutoUpgradePriority.Priority5,
            "priority_6" =>
                UnitAutoUpgradePriority.Priority6,
            _ => throw new JsonException(
                "Auto Upgrade priority must be Off or Priority 1 through Priority 6."),
        };
}
