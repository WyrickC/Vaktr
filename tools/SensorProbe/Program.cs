using LibreHardwareMonitor.Hardware;

Console.WriteLine($"PawnIO installed: {PawnIo.IsInstalled}");
Console.WriteLine($"PawnIO version: {PawnIo.Version}");
Console.WriteLine();

var computer = new Computer
{
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsMotherboardEnabled = true,
};

computer.Open();

try
{
    Console.WriteLine("=== All temperature sensors ===");
    foreach (var hardware in computer.Hardware)
    {
        hardware.Update();
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                Console.WriteLine($"  {hardware.HardwareType} / {hardware.Name} / {sensor.Name}: {sensor.Value} C");
            }
        }

        foreach (var sub in hardware.SubHardware)
        {
            sub.Update();
            foreach (var sensor in sub.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature)
                {
                    Console.WriteLine($"  {sub.HardwareType} / {sub.Name} / {sensor.Name}: {sensor.Value} C");
                }
            }
        }
    }
}
finally
{
    computer.Close();
}

public class PawnIo
{
    public static bool IsInstalled
    {
        get
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
                return key?.GetValue("DisplayVersion") is not null;
            }
            catch { return false; }
        }
    }

    public static string? Version
    {
        get
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
                return key?.GetValue("DisplayVersion") as string;
            }
            catch { return null; }
        }
    }
}
