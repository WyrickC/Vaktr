using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using Vaktr.Core.Models;

namespace Vaktr.Collector;

public static class TemperatureBridge
{
    private const string HelperModeArgument = "--temperature-helper";
    private const string ParentPidArgument = "--parent-pid";
    private const string CachePathArgument = "--cache-path";
    private static readonly TimeSpan SnapshotFreshness = TimeSpan.FromSeconds(18);
    private static readonly TimeSpan HelperLaunchCooldown = TimeSpan.FromMinutes(2);
    private static readonly object LaunchGate = new();
    private static DateTimeOffset _lastLaunchAttemptUtc = DateTimeOffset.MinValue;

    public static string GetDefaultCachePath() =>
        Path.Combine(VaktrConfig.DefaultStorageDirectory, "temperature-bridge.json");

    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryHandleHelperMode(string[] args)
    {
        if (!args.Any(argument => string.Equals(argument, HelperModeArgument, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        RunHelperLoop(args);
        return true;
    }

    public static bool TryReadSnapshot(out TemperatureBridgeSnapshot snapshot, string? cachePath = null)
    {
        snapshot = default!;
        var path = string.IsNullOrWhiteSpace(cachePath) ? GetDefaultCachePath() : cachePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<TemperatureBridgeSnapshot>(json);
            if (parsed is null || DateTimeOffset.UtcNow - parsed.TimestampUtc > SnapshotFreshness)
            {
                return false;
            }

            snapshot = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void EnsureHelperRunning(string? cachePath = null)
    {
        if (IsElevated())
        {
            return;
        }

        lock (LaunchGate)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastLaunchAttemptUtc < HelperLaunchCooldown)
            {
                return;
            }

            _lastLaunchAttemptUtc = now;
        }

        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return;
            }

            var resolvedCachePath = string.IsNullOrWhiteSpace(cachePath) ? GetDefaultCachePath() : cachePath;
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory,
                Arguments = $"{HelperModeArgument} {ParentPidArgument} {Environment.ProcessId} {CachePathArgument} \"{resolvedCachePath}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            _ = Process.Start(startInfo);
        }
        catch
        {
            // If the user denies elevation or Windows blocks the helper, Vaktr falls back to the local path.
        }
    }

    private static void RunHelperLoop(string[] args)
    {
        var parentPid = TryParseIntArgument(args, ParentPidArgument);
        var cachePath = TryParseStringArgument(args, CachePathArgument);
        if (parentPid <= 0 || string.IsNullOrWhiteSpace(cachePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var reader = new TemperatureSensorReader();
        while (IsProcessAlive(parentPid.Value))
        {
            try
            {
                var reading = reader.Read();
                var snapshot = new TemperatureBridgeSnapshot(
                    DateTimeOffset.UtcNow,
                    reading.CpuTemperatureCelsius,
                    reading.GpuTemperatureCelsius,
                    reading.Sensors);
                WriteSnapshot(snapshot, cachePath);
            }
            catch
            {
            }

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }
    }

    private static void WriteSnapshot(TemperatureBridgeSnapshot snapshot, string cachePath)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshot);
            var tempPath = cachePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, cachePath, true);
        }
        catch
        {
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static int? TryParseIntArgument(string[] args, string name)
    {
        var value = TryParseStringArgument(args, name);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? TryParseStringArgument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}

public sealed record TemperatureBridgeSnapshot(
    DateTimeOffset TimestampUtc,
    double? CpuTemperatureCelsius,
    double? GpuTemperatureCelsius,
    string[] Sensors);
