using System.Text.Json;
using WindowsLogger.Core;
using Xunit;

namespace WindowsLogger.Core.Tests;

public sealed class SensorInventoryJsonTests
{
    [Fact]
    public void RoundTrip_PreservesMeasuredZeroSeparatelyFromUnavailableNull()
    {
        var captured = new DateTimeOffset(2026, 10, 12, 7, 12, 55, TimeSpan.Zero);
        var report = Report(captured,
        [
            new SensorReport("/gpu/0/load/0", "GPU Load", "Load", 0, "%", SensorAvailability.Available, "LHM", captured, 0, 100),
            new SensorReport("/gpu/0/temperature/1", "GPU Hotspot", "Temperature", null, "°C", SensorAvailability.Unavailable, "LHM", captured, null, null)
        ]);

        var restored = SensorInventoryJson.Deserialize(SensorInventoryJson.Serialize(report));

        Assert.Equal(0f, restored.Hardware[0].Sensors[0].Value.GetValueOrDefault());
        Assert.Equal(SensorAvailability.Available, restored.Hardware[0].Sensors[0].Availability);
        Assert.Null(restored.Hardware[0].Sensors[1].Value);
        Assert.Equal(SensorAvailability.Unavailable, restored.Hardware[0].Sensors[1].Availability);
    }

    [Fact]
    public void Serialization_PreservesStableIdentifiersAndUtcOffsets()
    {
        var captured = new DateTimeOffset(2026, 10, 12, 7, 12, 55, TimeSpan.Zero);
        var original = Report(captured,
        [new SensorReport("/gpu/0/clock/1", "Core Clock", "Clock", 1800, "MHz", SensorAvailability.Available, "LHM", captured, null, null)]);

        var serialized = SensorInventoryJson.Serialize(original);
        var restored = SensorInventoryJson.Deserialize(serialized);

        Assert.Contains("2026-10-12T07:12:55+00:00", serialized);
        Assert.Equal("/gpu/0/clock/1", restored.Hardware[0].Sensors[0].Identifier);
        Assert.Equal(TimeSpan.Zero, restored.CapturedAtUtc.Offset);
    }

    [Fact]
    public void Serialization_PreservesCollectionErrorsAsSeparateEvidence()
    {
        var captured = DateTimeOffset.UtcNow;
        var error = new CollectionError("LHM", "GPU", "IOException", "Provider read failed", captured);
        var original = Report(captured, [], [error]);

        var restored = SensorInventoryJson.Deserialize(SensorInventoryJson.Serialize(original));

        Assert.Single(restored.Errors);
        Assert.Equal("IOException", restored.Errors[0].ErrorType);
        Assert.Equal("Provider read failed", restored.Errors[0].Message);
    }

    [Fact]
    public void Deserialize_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => SensorInventoryJson.Deserialize(" "));
        Assert.Throws<JsonException>(() => SensorInventoryJson.Deserialize("null"));
    }

    private static SensorInventoryReport Report(DateTimeOffset captured, IReadOnlyList<SensorReport> sensors, IReadOnlyList<CollectionError>? errors = null) =>
        new("1.0", captured, "test OS", "x64", "test provider",
            [new HardwareReport("/gpu/0", "GPU", "GpuNvidia", sensors)],
            errors ?? [], "Test data only; not a hardware measurement.");
}
