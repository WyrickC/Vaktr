namespace Vaktr.Tests;

public sealed class VaktrConfigTests
{
    [Fact]
    public void Normalize_Resets_Out_Of_Range_Values()
    {
        var config = new VaktrConfig
        {
            ScrapeIntervalSeconds = 0,
            GraphWindowMinutes = -1,
            StorageDirectory = string.Empty,
        };

        config.Normalize();

        Assert.Equal(2, config.ScrapeIntervalSeconds);
        Assert.Equal(VaktrConfig.DefaultGraphWindowMinutes, config.GraphWindowMinutes);
        Assert.False(string.IsNullOrWhiteSpace(config.StorageDirectory));
    }

    [Fact]
    public void Normalize_Clamps_High_ScrapeInterval()
    {
        var config = new VaktrConfig { ScrapeIntervalSeconds = 999 };
        config.Normalize();
        Assert.Equal(VaktrConfig.DefaultScrapeIntervalSeconds, config.ScrapeIntervalSeconds);
    }

    [Fact]
    public void Normalize_Keeps_Valid_ScrapeInterval()
    {
        var config = new VaktrConfig { ScrapeIntervalSeconds = 10 };
        config.Normalize();
        Assert.Equal(10, config.ScrapeIntervalSeconds);
    }

    [Fact]
    public void Normalize_Clamps_High_GraphWindowMinutes()
    {
        var config = new VaktrConfig { GraphWindowMinutes = int.MaxValue };
        config.Normalize();
        Assert.Equal(VaktrConfig.DefaultGraphWindowMinutes, config.GraphWindowMinutes);
    }

    [Theory]
    [InlineData("5m", 5)]
    [InlineData("24h", 24 * 60)]
    [InlineData("7d", 7 * 24 * 60)]
    [InlineData("1d", 24 * 60)]
    [InlineData("90d", 90 * 24 * 60)]
    public void TryParseRetentionWindow_Parses_Valid_Formats(string input, int expectedMinutes)
    {
        var result = VaktrConfig.TryParseRetentionWindow(input, out var window, out var normalized);
        Assert.True(result);
        Assert.Equal(expectedMinutes, (int)window.TotalMinutes);
        Assert.False(string.IsNullOrWhiteSpace(normalized));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("x")]
    [InlineData("abc")]
    [InlineData("0h")]
    [InlineData("-5d")]
    public void TryParseRetentionWindow_Rejects_Invalid_Formats(string? input)
    {
        var result = VaktrConfig.TryParseRetentionWindow(input, out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void Normalize_Sets_RetentionInputText_From_MaxRetentionHours()
    {
        var config = new VaktrConfig { RetentionInputText = "", MaxRetentionHours = 48 };
        config.Normalize();
        Assert.Equal("2d", config.RetentionInputText);
    }

    [Fact]
    public void Normalize_Preserves_Valid_RetentionInputText()
    {
        var config = new VaktrConfig { RetentionInputText = "7d", MaxRetentionHours = 24 };
        config.Normalize();
        Assert.Equal("7d", config.RetentionInputText);
    }

    [Fact]
    public void GetRetentionWindow_Uses_RetentionInputText_When_Set()
    {
        var config = new VaktrConfig { RetentionInputText = "7d", MaxRetentionHours = 1 };
        var window = config.GetRetentionWindow();
        Assert.Equal(TimeSpan.FromDays(7), window);
    }

    [Fact]
    public void GetRetentionWindow_Falls_Back_To_MaxRetentionHours()
    {
        var config = new VaktrConfig { RetentionInputText = "", MaxRetentionHours = 48 };
        var window = config.GetRetentionWindow();
        Assert.Equal(TimeSpan.FromHours(48), window);
    }

    [Fact]
    public void CreateDefault_Returns_Valid_Config()
    {
        var config = VaktrConfig.CreateDefault();
        Assert.Equal(VaktrConfig.DefaultStorageDirectory, config.StorageDirectory);
        Assert.Equal(VaktrConfig.DefaultMaxRetentionHours, config.MaxRetentionHours);
    }

    [Fact]
    public void Normalize_Resolves_StorageDirectory_To_Absolute_Path()
    {
        var config = new VaktrConfig { StorageDirectory = "relative\\path" };
        config.Normalize();
        Assert.True(Path.IsPathRooted(config.StorageDirectory));
    }

    [Fact]
    public void Normalize_Resets_Invalid_StorageDirectory()
    {
        var config = new VaktrConfig { StorageDirectory = "" };
        config.Normalize();
        Assert.Equal(VaktrConfig.DefaultStorageDirectory, config.StorageDirectory);
    }

    [Fact]
    public void Normalize_Cleans_PanelOrder_Duplicates()
    {
        var config = new VaktrConfig { PanelOrder = ["cpu-total", "memory", "cpu-total", ""] };
        config.Normalize();
        Assert.Equal(2, config.PanelOrder.Count);
        Assert.Contains("cpu-total", config.PanelOrder);
        Assert.Contains("memory", config.PanelOrder);
    }

    [Fact]
    public void Normalize_Initializes_Null_Collections()
    {
        var config = new VaktrConfig { PanelVisibility = null!, PanelOrder = null! };
        config.Normalize();
        Assert.NotNull(config.PanelVisibility);
        Assert.NotNull(config.PanelOrder);
    }
}
