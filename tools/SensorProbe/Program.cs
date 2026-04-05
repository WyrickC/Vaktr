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
    Console.WriteLine("=== LibreHardwareMonitor: ALL sensors on CPU ===");
    foreach (var hardware in computer.Hardware)
    {
        DumpAllSensors(hardware, "");
    }

    Console.WriteLine();
    Console.WriteLine("=== WMI Win32_TemperatureProbe ===");
    TryWmiQuery(@"root\CIMV2", "SELECT * FROM Win32_TemperatureProbe");

    Console.WriteLine();
    Console.WriteLine("=== WMI MSAcpi_ThermalZoneTemperature ===");
    TryWmiQuery(@"root\WMI", "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

    Console.WriteLine();
    Console.WriteLine("=== WMI Win32_PerfFormattedData_Counters_ThermalZoneInformation ===");
    TryWmiQuery(@"root\CIMV2", "SELECT Name, Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");
}
finally
{
    computer.Close();
}

static void DumpAllSensors(IHardware hardware, string indent)
{
    hardware.Update();
    Console.WriteLine($"{indent}HW {hardware.HardwareType}: {hardware.Name}");

    foreach (var sensor in hardware.Sensors)
    {
        Console.WriteLine($"{indent}  [{sensor.SensorType}] {sensor.Name}: {sensor.Value} (id={sensor.Identifier})");
    }

    foreach (var subHardware in hardware.SubHardware)
    {
        DumpAllSensors(subHardware, indent + "  ");
    }
}

static void TryWmiQuery(string scope, string query)
{
    try
    {
        using var searcher = new ManagementObjectSearcher(scope, query);
        using var results = searcher.Get();
        var count = 0;
        foreach (ManagementObject result in results)
        {
            count++;
            Console.Write($"  [{count}] ");
            foreach (var prop in result.Properties)
            {
                if (prop.Value is not null)
                {
                    Console.Write($"{prop.Name}={prop.Value}  ");
                }
            }
            Console.WriteLine();
        }
        if (count == 0)
        {
            Console.WriteLine("  (no results)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Failed: {ex.GetType().Name}: {ex.Message}");
    }
}
