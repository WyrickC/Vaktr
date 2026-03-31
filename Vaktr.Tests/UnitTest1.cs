namespace Vaktr.Tests;

public sealed class VaktrConfigTests
{
    [Fact]
    public void Normalize_Resets_Out_Of_Range_Values()
    {
        var config = new VaktrConfig
        {
            ScrapeIntervalSeconds = 0,
            GraphWindowMinutes = 999,
            StorageDirectory = string.Empty,
        };

        config.Normalize();

        Assert.Equal(2, config.ScrapeIntervalSeconds);
        Assert.Equal(15, config.GraphWindowMinutes);
        Assert.False(string.IsNullOrWhiteSpace(config.StorageDirectory));
    }
}
