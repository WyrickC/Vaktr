namespace Vaktr.Tests;

public sealed class SqliteMetricStoreTests : IAsyncDisposable
{
    private readonly string _tempDir;
    private readonly Vaktr.Store.Persistence.SqliteMetricStore _store;

    public SqliteMetricStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vaktr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new Vaktr.Store.Persistence.SqliteMetricStore();
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }

    private VaktrConfig CreateConfig() => new VaktrConfig
    {
        StorageDirectory = _tempDir,
        MaxRetentionHours = 24,
    }.Normalize();

    [Fact]
    public async Task InitializeAsync_Creates_Database_File()
    {
        var config = CreateConfig();
        await _store.InitializeAsync(config, CancellationToken.None);
        Assert.True(File.Exists(config.GetDatabasePath()));
    }

    [Fact]
    public async Task AppendSnapshot_And_LoadHistory_Roundtrip()
    {
        var config = CreateConfig();
        await _store.InitializeAsync(config, CancellationToken.None);

        var timestamp = DateTimeOffset.UtcNow;
        var snapshot = new MetricSnapshot(timestamp,
        [
            new MetricSample("cpu-total", "CPU Total", "usage", "Usage",
                MetricCategory.Cpu, MetricUnit.Percent, 45.5, timestamp),
        ]);

        await _store.AppendSnapshotAsync(snapshot, CancellationToken.None);

        var history = await _store.LoadHistoryAsync(timestamp.AddMinutes(-5), CancellationToken.None);
        Assert.NotEmpty(history);
        var cpuPanel = history.First(h => h.PanelKey == "cpu-total");
        Assert.Single(cpuPanel.Series);
        Assert.Single(cpuPanel.Series[0].Points);
        Assert.Equal(45.5, cpuPanel.Series[0].Points[0].Value, 1);
    }

    [Fact]
    public async Task AppendSnapshot_Empty_Samples_Does_Not_Throw()
    {
        var config = CreateConfig();
        await _store.InitializeAsync(config, CancellationToken.None);

        var snapshot = new MetricSnapshot(DateTimeOffset.UtcNow, []);
        await _store.AppendSnapshotAsync(snapshot, CancellationToken.None);
    }

    [Fact]
    public async Task LoadHistory_Empty_Database_Returns_Empty()
    {
        var config = CreateConfig();
        await _store.InitializeAsync(config, CancellationToken.None);

        var history = await _store.LoadHistoryAsync(DateTimeOffset.UtcNow.AddHours(-1), CancellationToken.None);
        Assert.Empty(history);
    }

    [Fact]
    public async Task PruneAsync_Removes_Old_Data()
    {
        var config = CreateConfig();
        await _store.InitializeAsync(config, CancellationToken.None);

        var oldTimestamp = DateTimeOffset.UtcNow.AddDays(-30);
        var snapshot = new MetricSnapshot(oldTimestamp,
        [
            new MetricSample("test", "Test", "value", "Value",
                MetricCategory.System, MetricUnit.Count, 1.0, oldTimestamp),
        ]);
        await _store.AppendSnapshotAsync(snapshot, CancellationToken.None);

        var pruneConfig = new VaktrConfig
        {
            StorageDirectory = _tempDir,
            MaxRetentionHours = 1,
        }.Normalize();
        await _store.PruneAsync(pruneConfig, CancellationToken.None);

        var history = await _store.LoadHistoryAsync(oldTimestamp.AddMinutes(-1), CancellationToken.None);
        Assert.Empty(history);
    }
}
