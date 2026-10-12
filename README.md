# Windows Logger — Phase 1: sensor discovery

This repository contains a Windows sensor-discovery utility plus a portable core for inventory data and JSON serialization. Continuous logging, Windows event collection, incidents, and a dashboard are still pending. It never invents readings: the report contains only hardware and sensors returned by LibreHardwareMonitor on the machine where it runs.

## Architecture and boundaries

- `src/SensorDiscovery`: Windows-targeted .NET console executable. It opens LibreHardwareMonitor, enables CPU/GPU/memory/motherboard/controller/storage/network discovery, updates each device, and writes a timestamped JSON inventory.
- `src/WindowsLogger.Core`: portable .NET library containing the sensor inventory schema and JSON serialization, with no Windows-only APIs.
- `tests/WindowsLogger.Core.Tests`: portable unit tests for null-versus-zero readings, identifiers, timestamps, and separately recorded collection errors.
- `.github/workflows`: macOS portable tests and Windows solution build/test jobs.
- `docs/sensor-inventory.schema.json`: machine-readable JSON Schema for the inventory report.
- The JSON records stable provider identifiers, hardware/sensor names and types, nullable values, units, availability, capture timestamps, min/max when supplied, and per-device/provider errors.
- Machine-dependent sensor discovery stays in the provider adapter. The report schema is ordinary JSON and can be inspected on macOS.
- A WPF/MVVM dashboard, logger, SQLite, Windows Event Log, NVIDIA-specific NVAPI integration, and incident correlation are deliberately pending.

## Environment checked for this phase

Development host: macOS 26.6, Apple Silicon (arm64). Git 2.55.0 is available. The .NET SDK is not installed, so restore/build/tests could not be run in this environment. Windows hardware discovery cannot be validated on macOS. The project targets .NET 10 LTS and pins LibreHardwareMonitorLib 0.9.6 (stable package) rather than a prerelease.

## First test on the Windows laptop

Install the .NET 10 SDK on the Windows 10/11 test machine. In PowerShell, from this repository directory:

```powershell
dotnet --info
dotnet restore .\WindowsLogger.sln
dotnet build .\WindowsLogger.sln -c Release
dotnet run --project .\src\SensorDiscovery\SensorDiscovery.csproj -c Release -- --output .\sensor-reports\rtx-5050-inventory.json
```

Open `sensor-reports\rtx-5050-inventory.json` and search the hardware list for the NVIDIA GPU. The exact sensors depend on laptop firmware, GPU/driver support, and LibreHardwareMonitor's support. A missing hotspot, power, fan, or VRAM sensor means it was not exposed in that run; it is not represented as zero. Keep the report with machine model, BIOS version, NVIDIA driver version, and whether the program was elevated. If access-denied errors appear, rerun once from an elevated PowerShell only to compare access; do not make elevation the default. This app does not claim to identify the RTX 5050's capabilities without that actual report.

Run portable schema tests with `dotnet test .\tests\WindowsLogger.Core.Tests\WindowsLogger.Core.Tests.csproj -c Release`. The tests exercise serialization behavior only; they do not simulate or claim hardware readings. On Windows, the commands above verify restore/build and produce the genuine hardware report. Do not count Phase 1 as verified until the tests pass and the generated report is inspected.

## macOS development

Install the .NET 10 SDK, then run `dotnet test tests/WindowsLogger.Core.Tests/WindowsLogger.Core.Tests.csproj -c Release`. The portable core and its tests can run on macOS. The Windows-targeted project sets `EnableWindowsTargeting` to allow cross-targeted compilation, but the discovery executable requires Windows and cannot be run on macOS. Do not use mocked readings to substitute for hardware results.

## Publish on Windows

Framework-dependent x64 publish (requires .NET 10 Desktop Runtime/host runtime availability):

```powershell
dotnet publish .\src\SensorDiscovery\SensorDiscovery.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\sensor-discovery-win-x64
```

Self-contained single-file x64 publish:

```powershell
dotnet publish .\src\SensorDiscovery\SensorDiscovery.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\artifacts\sensor-discovery-win-x64
```

## Dependencies and licensing

- .NET 10 SDK/runtime: Microsoft .NET, MIT license.
- LibreHardwareMonitorLib 0.9.6: open-source LibreHardwareMonitor project; Mozilla Public License 2.0 (MPL-2.0). Review the package and transitive notices before redistribution.
- Portable test dependencies: Microsoft.NET.Test.Sdk 17.14.1, xUnit 2.9.3, and xunit.runner.visualstudio 3.1.5. Review their package licenses and transitive notices before redistribution.
- No NVIDIA NVAPI dependency is included in this phase. The Windows report is the required first evidence for deciding whether additional supported NVIDIA interfaces are needed.

## Data, privacy, and limitations

The report is local and contains hardware names, sensor labels/readings, machine OS version, and collection errors. It does not include user content, screenshots, keystrokes, or network uploads. Reports are retained until manually deleted. Some low-level readings may require administrator privileges or may be unavailable due to firmware/driver/provider limitations. A single snapshot does not establish a fault and does not prove what occurred before a freeze.

## Phase checklist

- [x] Inspect repository and development environment.
- [x] Initialize Git and create the Phase 1 solution structure and instructions.
- [x] Implement Windows sensor inventory collection/report generation using LibreHardwareMonitor.
- [x] Separate the portable report schema/JSON layer and add unit tests plus macOS/Windows CI workflows.
- [ ] Restore/build/test on .NET 10 (SDK unavailable on this Mac).
- [ ] Run on Windows and inspect genuine RTX 5050 Laptop GPU sensor exposure.
- [ ] Phase 2 continuous logging, persistence, rolling history, and event correlation.
- [ ] Later phases: workload tracking, incidents/exports, WPF dashboard, optional tracing.
