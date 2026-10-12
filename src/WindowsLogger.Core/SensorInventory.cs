using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsLogger.Core;

public sealed record SensorInventoryReport(
    string SchemaVersion,
    DateTimeOffset CapturedAtUtc,
    string OperatingSystem,
    string Architecture,
    string Provider,
    IReadOnlyList<HardwareReport> Hardware,
    IReadOnlyList<CollectionError> Errors,
    string Notes);

public sealed record HardwareReport(
    string Identifier,
    string Name,
    string Type,
    IReadOnlyList<SensorReport> Sensors);

public sealed record SensorReport(
    string Identifier,
    string Name,
    string Type,
    float? Value,
    string Unit,
    SensorAvailability Availability,
    string Provider,
    DateTimeOffset TimestampUtc,
    float? Minimum,
    float? Maximum);

public enum SensorAvailability
{
    Available,
    Unavailable,
    Invalid,
    Error
}

public sealed record CollectionError(
    string Provider,
    string? Hardware,
    string ErrorType,
    string Message,
    DateTimeOffset TimestampUtc);

public static class SensorInventoryJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(SensorInventoryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, Options);
    }

    public static SensorInventoryReport Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<SensorInventoryReport>(json, Options)
            ?? throw new JsonException("The sensor inventory document was empty.");
    }
}
