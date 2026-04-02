namespace Vaktr.Core.Models;

public enum MetricCategory
{
    Cpu = 0,
    Memory = 1,
    Disk = 2,
    Network = 3,
    System = 4,
}

public enum MetricUnit
{
    Percent = 0,
    Gigabytes = 1,
    MegabytesPerSecond = 2,
    MegabitsPerSecond = 3,
    Megahertz = 4,
    Count = 5,
}

public enum ThemeMode
{
    Dark = 0,
    Light = 1,
}

public enum TimeRangePreset
{
    OneMinute = 1,
    FiveMinutes = 5,
    FifteenMinutes = 15,
    OneHour = 60,
}

public enum RetentionPreset
{
    Unlimited = 0,
    OneDay = 1,
    SevenDays = 7,
    ThirtyDays = 30,
    NinetyDays = 90,
}
