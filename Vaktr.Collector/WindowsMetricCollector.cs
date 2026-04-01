using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Net.NetworkInformation;
using Vaktr.Collector.Interop;
using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;

namespace Vaktr.Collector;

public sealed class WindowsMetricCollector : IMetricCollector
{
    private const double BytesPerMegabyte = 1024d * 1024d;
    private const double BitsPerMegabit = 1_000_000d;

    private readonly nint _query;
    private readonly nint _cpuTotalCounter;
    private readonly nint _cpuPerCoreCounter;
    private readonly nint _cpuFrequencyCounter;
    private readonly nint _diskReadCounter;
    private readonly nint _diskWriteCounter;
    private readonly nint _networkReceiveCounter;
    private readonly nint _networkSendCounter;
    private readonly bool _hasAnyPdhCounters;
    private readonly Dictionary<string, NetworkBaseline> _networkBaselines = new(StringComparer.OrdinalIgnoreCase);

    private ulong _previousIdleTime;
    private ulong _previousKernelTime;
    private ulong _previousUserTime;
    private bool _cpuFallbackInitialized;

    public WindowsMetricCollector()
    {
        ThrowIfFailed(PdhNative.PdhOpenQuery(null, nint.Zero, out _query), "Failed to open the PDH query.");

        _cpuTotalCounter = TryAddCounter(@"\Processor(_Total)\% Processor Time");
        _cpuPerCoreCounter = TryAddCounter(@"\Processor(*)\% Processor Time");
        _cpuFrequencyCounter = TryAddCounter(@"\Processor Information(_Total)\Processor Frequency");
        _diskReadCounter = TryAddCounter(@"\LogicalDisk(*)\Disk Read Bytes/sec");
        _diskWriteCounter = TryAddCounter(@"\LogicalDisk(*)\Disk Write Bytes/sec");
        _networkReceiveCounter = TryAddCounter(@"\Network Interface(*)\Bytes Received/sec");
        _networkSendCounter = TryAddCounter(@"\Network Interface(*)\Bytes Sent/sec");

        _hasAnyPdhCounters =
            _cpuTotalCounter != nint.Zero ||
            _cpuPerCoreCounter != nint.Zero ||
            _cpuFrequencyCounter != nint.Zero ||
            _diskReadCounter != nint.Zero ||
            _diskWriteCounter != nint.Zero ||
            _networkReceiveCounter != nint.Zero ||
            _networkSendCounter != nint.Zero;

        if (_hasAnyPdhCounters)
        {
            _ = PdhNative.PdhCollectQueryData(_query);
        }
    }

    public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var timestamp = DateTimeOffset.UtcNow;
        var samples = new List<MetricSample>();
        var pdhAvailable = TryCollectPdhData();

        if (pdhAvailable)
        {
            AddCpuUsage(samples, timestamp);
            AddCpuFrequency(samples, timestamp);
        }
        else
        {
            AddCpuUsageFallback(samples, timestamp);
        }

        AddMemory(samples, timestamp);
        AddDriveUsage(samples, timestamp);

        if (pdhAvailable)
        {
            AddDisk(samples, timestamp);
            AddNetwork(samples, timestamp);
        }
        else
        {
            AddNetworkFallback(samples, timestamp);
        }

        return Task.FromResult(new MetricSnapshot(timestamp, samples));
    }

    public ValueTask DisposeAsync()
    {
        if (_query != nint.Zero)
        {
            _ = PdhNative.PdhCloseQuery(_query);
        }

        return ValueTask.CompletedTask;
    }

    private void AddCpuUsage(List<MetricSample> samples, DateTimeOffset timestamp)
    {
        var totalValue = TryGetSingleValue(_cpuTotalCounter);
        if (totalValue.HasValue)
        {
            samples.Add(new MetricSample(
                "cpu-total",
                "CPU Total",
                "usage",
                "Usage",
                MetricCategory.Cpu,
                MetricUnit.Percent,
                Math.Clamp(totalValue.Value, 0d, 100d),
                timestamp));
        }
        else
        {
            AddCpuUsageFallback(samples, timestamp);
        }

        var cores = TryGetArrayValues(_cpuPerCoreCounter)
            .Where(entry => int.TryParse(entry.Key, out _))
            .OrderBy(entry => int.Parse(entry.Key, System.Globalization.CultureInfo.InvariantCulture));

        foreach (var (instance, value) in cores)
        {
            samples.Add(new MetricSample(
                "cpu-cores",
                "CPU Cores",
                $"core-{instance}",
                $"Core {instance}",
                MetricCategory.Cpu,
                MetricUnit.Percent,
                Math.Clamp(value, 0d, 100d),
                timestamp));
        }
    }

    private void AddCpuUsageFallback(List<MetricSample> samples, DateTimeOffset timestamp)
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return;
        }

        var idle = idleTime.ToUInt64();
        var kernel = kernelTime.ToUInt64();
        var user = userTime.ToUInt64();

        double usage;
        if (!_cpuFallbackInitialized)
        {
            usage = 0d;
            _cpuFallbackInitialized = true;
        }
        else
        {
            var idleDelta = idle - _previousIdleTime;
            var kernelDelta = kernel - _previousKernelTime;
            var userDelta = user - _previousUserTime;
            var totalDelta = kernelDelta + userDelta;
            usage = totalDelta == 0
                ? 0d
                : Math.Clamp(((totalDelta - idleDelta) / (double)totalDelta) * 100d, 0d, 100d);
        }

        _previousIdleTime = idle;
        _previousKernelTime = kernel;
        _previousUserTime = user;

        samples.Add(new MetricSample(
            "cpu-total",
            "CPU Total",
            "usage",
            "Usage",
            MetricCategory.Cpu,
            MetricUnit.Percent,
            usage,
            timestamp));
    }

    private void AddCpuFrequency(List<MetricSample> samples, DateTimeOffset timestamp)
    {
        var value = TryGetSingleValue(_cpuFrequencyCounter);
        if (!value.HasValue || value.Value <= 0d)
        {
            return;
        }

        samples.Add(new MetricSample(
            "cpu-frequency",
            "CPU Clock",
            "clock",
            "Clock",
            MetricCategory.Cpu,
            MetricUnit.Megahertz,
            value.Value,
            timestamp));
    }

    private static void AddMemory(List<MetricSample> samples, DateTimeOffset timestamp)
    {
        var memoryStatus = MemoryStatusEx.Create();
        if (!GlobalMemoryStatusEx(ref memoryStatus))
        {
            return;
        }

        var totalGb = memoryStatus.TotalPhys / 1024d / 1024d / 1024d;
        var availableGb = memoryStatus.AvailPhys / 1024d / 1024d / 1024d;
        var usedGb = Math.Max(0d, totalGb - availableGb);

        samples.Add(new MetricSample(
            "memory",
            "Memory",
            "used-gb",
            "Used",
            MetricCategory.Memory,
            MetricUnit.Gigabytes,
            usedGb,
            timestamp));

        samples.Add(new MetricSample(
            "memory",
            "Memory",
            "available-gb",
            "Available",
            MetricCategory.Memory,
            MetricUnit.Gigabytes,
            availableGb,
            timestamp));
    }

    private void AddDisk(List<MetricSample> samples, DateTimeOffset timestamp)
    {
        var reads = TryGetArrayValues(_diskReadCounter);
        var writes = TryGetArrayValues(_diskWriteCounter);

        var instances = reads.Keys.Concat(writes.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in instances)
        {
            if (!IsLogicalDriveInstance(instance))
            {
                continue;
            }

            var panelKey = $"disk-{Sanitize(instance)}";
            var panelTitle = $"Disk {instance}\\";

            samples.Add(new MetricSample(
                panelKey,
                panelTitle,
                "read",
                "Read",
                MetricCategory.Disk,
                MetricUnit.MegabytesPerSecond,
                reads.GetValueOrDefault(instance) / BytesPerMegabyte,
                timestamp));

            samples.Add(new MetricSample(
                panelKey,
                panelTitle,
                "write",
                "Write",
                MetricCategory.Disk,
                MetricUnit.MegabytesPerSecond,
                writes.GetValueOrDefault(instance) / BytesPerMegabyte,
                timestamp));
        }
    }

    private static void AddDriveUsage(List<MetricSample> samples, DateTimeOffset timestamp)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed || string.IsNullOrWhiteSpace(drive.Name))
                {
                    continue;
                }

                var totalBytes = drive.TotalSize;
                if (totalBytes <= 0)
                {
                    continue;
                }

                var totalGb = totalBytes / 1024d / 1024d / 1024d;
                var usedGb = Math.Max(0d, totalGb - (drive.AvailableFreeSpace / 1024d / 1024d / 1024d));
                var usedPercent = Math.Clamp((1d - (drive.AvailableFreeSpace / (double)totalBytes)) * 100d, 0d, 100d);
                var driveLabel = drive.Name.TrimEnd('\\');
                samples.Add(new MetricSample(
                    $"volume-{Sanitize(driveLabel)}",
                    $"Drive {driveLabel} Usage",
                    "used-percent",
                    "Used",
                    MetricCategory.Disk,
                    MetricUnit.Percent,
                    usedPercent,
                    timestamp));

                samples.Add(new MetricSample(
                    $"volume-{Sanitize(driveLabel)}",
                    $"Drive {driveLabel} Usage",
                    "used-gb",
                    "Used GiB",
                    MetricCategory.Disk,
                    MetricUnit.Gigabytes,
                    usedGb,
                    timestamp));

                samples.Add(new MetricSample(
                    $"volume-{Sanitize(driveLabel)}",
                    $"Drive {driveLabel} Usage",
                    "total-gb",
                    "Total GiB",
                    MetricCategory.Disk,
                    MetricUnit.Gigabytes,
                    totalGb,
                    timestamp));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void AddNetwork(List<MetricSample> samples, DateTimeOffset timestamp)
    {
        var receives = TryGetArrayValues(_networkReceiveCounter);
        var sends = TryGetArrayValues(_networkSendCounter);
        if (receives.Count == 0 && sends.Count == 0)
        {
            AddNetworkFallback(samples, timestamp);
            return;
        }

        var instances = receives.Keys.Concat(sends.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in instances)
        {
            if (string.IsNullOrWhiteSpace(instance) ||
                instance.Contains("loopback", StringComparison.OrdinalIgnoreCase) ||
                instance.Contains("isatap", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var panelKey = $"net-{Sanitize(instance)}";
            var panelTitle = $"Network {Beautify(instance)}";

            samples.Add(new MetricSample(
                panelKey,
                panelTitle,
                "download",
                "Down",
                MetricCategory.Network,
                MetricUnit.MegabitsPerSecond,
                receives.GetValueOrDefault(instance) * 8d / BitsPerMegabit,
                timestamp));

            samples.Add(new MetricSample(
                panelKey,
                panelTitle,
                "upload",
                "Up",
                MetricCategory.Network,
                MetricUnit.MegabitsPerSecond,
                sends.GetValueOrDefault(instance) * 8d / BitsPerMegabit,
                timestamp));
        }
    }

    private void AddNetworkFallback(List<MetricSample> samples, DateTimeOffset timestamp)
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel ||
                    networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var stats = networkInterface.GetIPv4Statistics();
                var panelKey = $"net-{Sanitize(networkInterface.Name)}";
                var panelTitle = $"Network {Beautify(networkInterface.Name)}";
                var receiveMbps = 0d;
                var sendMbps = 0d;

                if (_networkBaselines.TryGetValue(panelKey, out var baseline))
                {
                    var seconds = Math.Max(0.25d, (timestamp - baseline.Timestamp).TotalSeconds);
                    var receiveDelta = Math.Max(0L, stats.BytesReceived - baseline.BytesReceived);
                    var sendDelta = Math.Max(0L, stats.BytesSent - baseline.BytesSent);
                    receiveMbps = (receiveDelta * 8d) / seconds / BitsPerMegabit;
                    sendMbps = (sendDelta * 8d) / seconds / BitsPerMegabit;
                    _networkBaselines[panelKey] = new NetworkBaseline(stats.BytesReceived, stats.BytesSent, timestamp);
                }
                else
                {
                    _networkBaselines.Add(panelKey, new NetworkBaseline(stats.BytesReceived, stats.BytesSent, timestamp));
                }

                samples.Add(new MetricSample(
                    panelKey,
                    panelTitle,
                    "download",
                    "Down",
                    MetricCategory.Network,
                    MetricUnit.MegabitsPerSecond,
                    receiveMbps,
                    timestamp));

                samples.Add(new MetricSample(
                    panelKey,
                    panelTitle,
                    "upload",
                    "Up",
                    MetricCategory.Network,
                    MetricUnit.MegabitsPerSecond,
                    sendMbps,
                    timestamp));
            }
            catch (NetworkInformationException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }
        }
    }

    private nint TryAddCounter(string counterPath)
    {
        var status = PdhNative.PdhAddEnglishCounter(_query, counterPath, nint.Zero, out var counter);
        return status == PdhNative.ErrorSuccess ? counter : nint.Zero;
    }

    private bool TryCollectPdhData()
    {
        if (!_hasAnyPdhCounters)
        {
            return false;
        }

        var status = PdhNative.PdhCollectQueryData(_query);
        return status == PdhNative.ErrorSuccess;
    }

    private static double? TryGetSingleValue(nint counter)
    {
        if (counter == nint.Zero)
        {
            return null;
        }

        var status = PdhNative.PdhGetFormattedCounterValue(
            counter,
            PdhNative.PdhFmtDouble | PdhNative.PdhFmtNoCap100,
            out _,
            out var value);

        return status == PdhNative.ErrorSuccess && value.CStatus == PdhNative.ErrorSuccess
            ? value.DoubleValue
            : null;
    }

    private static Dictionary<string, double> TryGetArrayValues(nint counter)
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (counter == nint.Zero)
        {
            return values;
        }

        uint bufferSize = 0;
        uint itemCount = 0;
        var format = PdhNative.PdhFmtDouble | PdhNative.PdhFmtNoCap100;
        var status = PdhNative.PdhGetFormattedCounterArray(counter, format, ref bufferSize, ref itemCount, nint.Zero);
        if (status != PdhNative.PdhMoreData || bufferSize == 0 || itemCount == 0)
        {
            return values;
        }

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            status = PdhNative.PdhGetFormattedCounterArray(counter, format, ref bufferSize, ref itemCount, buffer);
            if (status != PdhNative.ErrorSuccess)
            {
                return values;
            }

            var itemSize = Marshal.SizeOf<PdhNative.PdhFmtCounterValueItemDouble>();
            for (var index = 0; index < itemCount; index++)
            {
                var itemPointer = buffer + (index * itemSize);
                var item = Marshal.PtrToStructure<PdhNative.PdhFmtCounterValueItemDouble>(itemPointer);
                if (item.FmtValue.CStatus != PdhNative.ErrorSuccess)
                {
                    continue;
                }

                var instanceName = Marshal.PtrToStringUni(item.SzName);
                if (string.IsNullOrWhiteSpace(instanceName))
                {
                    continue;
                }

                values[instanceName] = item.FmtValue.DoubleValue;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return values;
    }

    private static bool IsLogicalDriveInstance(string instance) =>
        !string.IsNullOrWhiteSpace(instance) &&
        !instance.Equals("_Total", StringComparison.OrdinalIgnoreCase) &&
        instance.EndsWith(":", StringComparison.Ordinal);

    private static string Beautify(string instance) =>
        instance.Replace('_', ' ').Replace('#', ' ').Trim();

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static void ThrowIfFailed(uint status, string message)
    {
        if (status != PdhNative.ErrorSuccess)
        {
            throw new InvalidOperationException($"{message} PDH status: 0x{status:X8}.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;

        public static MemoryStatusEx Create()
        {
            return new MemoryStatusEx
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>(),
            };
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;

        public ulong ToUInt64() => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    private readonly record struct NetworkBaseline(long BytesReceived, long BytesSent, DateTimeOffset Timestamp);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);
}
