using System.Text.Json;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using WindowsLogger.Core;

namespace WindowsLogger.SensorDiscovery;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var outputPath = ParseOutputPath(args);
            var report = Discover();
            var json = SensorInventoryJson.Serialize(report);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            File.WriteAllText(outputPath, json);

            Console.WriteLine($"Sensor inventory: {report.Hardware.Count} hardware device(s), {report.Hardware.Sum(h => h.Sensors.Count)} sensor(s).");
            Console.WriteLine($"Report: {Path.GetFullPath(outputPath)}");
            foreach (var hardware in report.Hardware)
            {
                Console.WriteLine($"- {hardware.Name} ({hardware.Type}): {hardware.Sensors.Count} sensor(s)");
            }
            if (report.Errors.Count > 0)
                Console.Error.WriteLine($"Collection reported {report.Errors.Count} error(s). See report.");

            return report.Hardware.Count == 0 ? 2 : 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Usage: WindowsLogger.SensorDiscovery.exe [--output <report.json>]");
            return 64;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not write sensor inventory: {ex.Message}");
            return 1;
        }
    }

    private static SensorInventoryReport Discover()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hardwareReports = new List<HardwareReport>();
        var errors = new List<CollectionError>();
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true
        };

        try
        {
            computer.Open();
            foreach (var hardware in computer.Hardware)
                Visit(hardware, timestamp, hardwareReports, errors);
        }
        catch (Exception ex)
        {
            errors.Add(new CollectionError("LibreHardwareMonitor", null, ex.GetType().Name, ex.Message, DateTimeOffset.UtcNow));
        }
        finally
        {
            try { computer.Close(); }
            catch (Exception ex)
            {
                errors.Add(new CollectionError("LibreHardwareMonitor", null, ex.GetType().Name, ex.Message, DateTimeOffset.UtcNow));
            }
        }

        return new SensorInventoryReport(
            "1.0",
            timestamp,
            Environment.OSVersion.VersionString,
            RuntimeInformation.OSArchitecture.ToString(),
            "LibreHardwareMonitor",
            hardwareReports,
            errors,
            "Only sensors exposed by LibreHardwareMonitor on this machine are listed. A null value means the provider exposed the sensor but no current reading was available. An absent sensor is not evidence of a zero reading or hardware failure.");
    }

    private static void Visit(IHardware hardware, DateTimeOffset timestamp, List<HardwareReport> hardwareReports, List<CollectionError> errors)
    {
        try
        {
            hardware.Update();
        }
        catch (Exception ex)
        {
            errors.Add(new CollectionError("LibreHardwareMonitor", hardware.Name, ex.GetType().Name, ex.Message, DateTimeOffset.UtcNow));
        }

        var sensors = new List<SensorReport>();
        try
        {
            foreach (var sensor in hardware.Sensors)
            {
                float? value = null;
                var status = SensorAvailability.Unavailable;
                var identifier = "unknown";
                var name = "unknown";
                var type = "unknown";
                var unit = "provider-defined";
                float? minimum = null;
                float? maximum = null;
                try
                {
                    identifier = sensor.Identifier.ToString();
                    name = sensor.Name;
                    type = sensor.SensorType.ToString();
                    unit = GetUnit(sensor.SensorType);
                    var current = sensor.Value;
                    if (current.HasValue && float.IsFinite(current.Value))
                    {
                        value = current.Value;
                        status = SensorAvailability.Available;
                    }
                    else if (current.HasValue)
                    {
                        status = SensorAvailability.Invalid;
                        errors.Add(new CollectionError("LibreHardwareMonitor", hardware.Name, "InvalidReading", $"Sensor '{name}' returned a non-finite value.", DateTimeOffset.UtcNow));
                    }
                }
                catch (Exception ex)
                {
                    status = SensorAvailability.Error;
                    errors.Add(new CollectionError("LibreHardwareMonitor", hardware.Name, ex.GetType().Name, $"Sensor '{name}': {ex.Message}", DateTimeOffset.UtcNow));
                }

                try
                {
                    var sensorMinimum = sensor.Min;
                    minimum = sensorMinimum.HasValue && float.IsFinite(sensorMinimum.Value) ? sensorMinimum : null;
                }
                catch (Exception ex)
                {
                    errors.Add(new CollectionError("LibreHardwareMonitor", hardware.Name, ex.GetType().Name, $"Minimum read for sensor '{name}': {ex.Message}", DateTimeOffset.UtcNow));
                }

                try
                {
                    var sensorMaximum = sensor.Max;
                    maximum = sensorMaximum.HasValue && float.IsFinite(sensorMaximum.Value) ? sensorMaximum : null;
                }
                catch (Exception ex)
                {
                    errors.Add(new CollectionError("LibreHardwareMonitor", hardware.Name, ex.GetType().Name, $"Maximum read for sensor '{name}': {ex.Message}", DateTimeOffset.UtcNow));
                }

                sensors.Add(new SensorReport(identifier, name, type, value, unit, status, "LibreHardwareMonitor", timestamp, minimum, maximum));
            }
        }
        catch (Exception ex)
        {
            errors.Add(new CollectionError("LibreHardwareMonitor", hardware.Name, ex.GetType().Name, ex.Message, DateTimeOffset.UtcNow));
        }

        hardwareReports.Add(new HardwareReport(hardware.Identifier.ToString(), hardware.Name, hardware.HardwareType.ToString(), sensors));
        try
        {
            foreach (var child in hardware.SubHardware)
                Visit(child, timestamp, hardwareReports, errors);
        }
        catch (Exception ex)
        {
            errors.Add(new CollectionError("LibreHardwareMonitor", hardware.Name, ex.GetType().Name, ex.Message, DateTimeOffset.UtcNow));
        }
    }

    private static string GetUnit(SensorType type) => type switch
    {
        SensorType.Temperature => "°C",
        SensorType.Clock => "MHz",
        SensorType.Frequency => "Hz",
        SensorType.Load => "%",
        SensorType.Fan => "RPM",
        SensorType.Voltage => "V",
        SensorType.Current => "A",
        SensorType.Power => "W",
        SensorType.Data => "B",
        SensorType.Throughput => "B/s",
        SensorType.TimeSpan => "s",
        SensorType.Timing => "ns",
        SensorType.Energy => "mWh",
        SensorType.Level or SensorType.Control or SensorType.Humidity => "%",
        SensorType.Flow => "L/h",
        SensorType.Noise => "dBA",
        SensorType.Conductivity => "µS/cm",
        SensorType.Factor => "1",
        _ => "provider-defined"
    };

    private static string ParseOutputPath(string[] args)
    {
        if (args.Length == 0)
            return Path.Combine("sensor-reports", $"sensor-inventory-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}.json");
        if (args.Length == 2 && args[0] == "--output" && !string.IsNullOrWhiteSpace(args[1]))
            return args[1];
        throw new ArgumentException("Invalid arguments.");
    }
}
