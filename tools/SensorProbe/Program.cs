using System.Management;
using LibreHardwareMonitor.Hardware;

var computer = new Computer
{
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsMotherboardEnabled = true,
};

computer.Open();

try
{
    Console.WriteLine("LibreHardwareMonitor probe");
    foreach (var hardware in computer.Hardware)
    {
        DumpHardware(hardware, "");
    }

    Console.WriteLine();
    Console.WriteLine("WMI thermal zone probe");
    TryDumpThermalZones();
}
finally
{
    computer.Close();
}

static void DumpHardware(IHardware hardware, string indent)
{
    hardware.Update();
    Console.WriteLine($"{indent}HW {hardware.HardwareType}: {hardware.Name}");

    foreach (var sensor in hardware.Sensors.Where(sensor => sensor.SensorType == SensorType.Temperature))
    {
        Console.WriteLine($"{indent}  TEMP {sensor.Name}: {sensor.Value}");
    }

    foreach (var subHardware in hardware.SubHardware)
    {
        DumpHardware(subHardware, indent + "  ");
    }
}

static void TryDumpThermalZones()
{
    try
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\WMI",
            "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
        using var results = searcher.Get();
        var foundAny = false;
        foreach (ManagementObject result in results)
        {
            foundAny = true;
            var instanceName = Convert.ToString(result["InstanceName"]) ?? "(unknown)";
            var rawValue = result["CurrentTemperature"];
            var celsius = rawValue is null
                ? double.NaN
                : (Convert.ToDouble(rawValue, System.Globalization.CultureInfo.InvariantCulture) / 10d) - 273.15d;
            Console.WriteLine($"  ZONE {instanceName}: {celsius:0.0} C");
        }

        if (!foundAny)
        {
            Console.WriteLine("  No thermal zones returned.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Thermal zone query failed: {ex.GetType().Name}: {ex.Message}");
    }
}
