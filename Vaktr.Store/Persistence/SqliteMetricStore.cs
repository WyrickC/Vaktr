using Microsoft.Data.Sqlite;
using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;

namespace Vaktr.Store.Persistence;

public sealed class SqliteMetricStore : IMetricStore
{
    private static readonly TimeSpan RawResolutionWindow = TimeSpan.FromHours(6);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(15);
    private const long MinuteBucketMilliseconds = 60_000;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _connectionString;
    private VaktrConfig? _config;
    private DateTimeOffset _nextMaintenanceUtc = DateTimeOffset.MinValue;

    public async Task InitializeAsync(VaktrConfig config, CancellationToken cancellationToken)
    {
        config = config.Normalize();
        Directory.CreateDirectory(config.StorageDirectory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = config.GetDatabasePath(),
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        _config = config;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA temp_store = MEMORY;
            PRAGMA auto_vacuum = INCREMENTAL;

            CREATE TABLE IF NOT EXISTS metric_samples (
                panel_key TEXT NOT NULL,
                panel_title TEXT NOT NULL,
                series_key TEXT NOT NULL,
                series_name TEXT NOT NULL,
                category INTEGER NOT NULL,
                unit INTEGER NOT NULL,
                timestamp_ms INTEGER NOT NULL,
                value REAL NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_metric_samples_timestamp
                ON metric_samples(timestamp_ms);

            CREATE INDEX IF NOT EXISTS idx_metric_samples_panel_series_time
                ON metric_samples(panel_key, series_key, timestamp_ms);

            CREATE TABLE IF NOT EXISTS metric_rollups_1m (
                panel_key TEXT NOT NULL,
                panel_title TEXT NOT NULL,
                series_key TEXT NOT NULL,
                series_name TEXT NOT NULL,
                category INTEGER NOT NULL,
                unit INTEGER NOT NULL,
                timestamp_ms INTEGER NOT NULL,
                value REAL NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_metric_rollups_identity
                ON metric_rollups_1m(panel_key, series_key, timestamp_ms);

            CREATE INDEX IF NOT EXISTS idx_metric_rollups_timestamp
                ON metric_rollups_1m(timestamp_ms);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendSnapshotAsync(MetricSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.Samples.Count == 0)
        {
            return;
        }

        var connectionString = EnsureInitialized();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO metric_samples (
                    panel_key,
                    panel_title,
                    series_key,
                    series_name,
                    category,
                    unit,
                    timestamp_ms,
                    value
                )
                VALUES (
                    $panelKey,
                    $panelTitle,
                    $seriesKey,
                    $seriesName,
                    $category,
                    $unit,
                    $timestampMs,
                    $value
                );
                """;

            var panelKey = command.CreateParameter();
            panelKey.ParameterName = "$panelKey";
            command.Parameters.Add(panelKey);

            var panelTitle = command.CreateParameter();
            panelTitle.ParameterName = "$panelTitle";
            command.Parameters.Add(panelTitle);

            var seriesKey = command.CreateParameter();
            seriesKey.ParameterName = "$seriesKey";
            command.Parameters.Add(seriesKey);

            var seriesName = command.CreateParameter();
            seriesName.ParameterName = "$seriesName";
            command.Parameters.Add(seriesName);

            var category = command.CreateParameter();
            category.ParameterName = "$category";
            command.Parameters.Add(category);

            var unit = command.CreateParameter();
            unit.ParameterName = "$unit";
            command.Parameters.Add(unit);

            var timestampMs = command.CreateParameter();
            timestampMs.ParameterName = "$timestampMs";
            command.Parameters.Add(timestampMs);

            var value = command.CreateParameter();
            value.ParameterName = "$value";
            command.Parameters.Add(value);

            foreach (var sample in snapshot.Samples)
            {
                panelKey.Value = sample.PanelKey;
                panelTitle.Value = sample.PanelTitle;
                seriesKey.Value = sample.SeriesKey;
                seriesName.Value = sample.SeriesName;
                category.Value = (int)sample.Category;
                unit.Value = (int)sample.Unit;
                timestampMs.Value = sample.Timestamp.ToUnixTimeMilliseconds();
                value.Value = sample.Value;

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await MaybeMaintainAsync(connection, snapshot.Timestamp, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<MetricSeriesHistory>> LoadHistoryAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken)
    {
        var connectionString = EnsureInitialized();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                panel_key,
                panel_title,
                series_key,
                series_name,
                category,
                unit,
                timestamp_ms,
                value
            FROM (
                SELECT
                    panel_key,
                    panel_title,
                    series_key,
                    series_name,
                    category,
                    unit,
                    timestamp_ms,
                    value
                FROM metric_rollups_1m
                WHERE timestamp_ms >= $fromTimestamp

                UNION ALL

                SELECT
                    panel_key,
                    panel_title,
                    series_key,
                    series_name,
                    category,
                    unit,
                    timestamp_ms,
                    value
                FROM metric_samples
                WHERE timestamp_ms >= $fromTimestamp
            )
            ORDER BY panel_key, series_key, timestamp_ms;
            """;
        command.Parameters.AddWithValue("$fromTimestamp", fromUtc.ToUnixTimeMilliseconds());

        var panelMap = new Dictionary<string, PanelAccumulator>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var panelKey = reader.GetString(0);
            if (!panelMap.TryGetValue(panelKey, out var panel))
            {
                panel = new PanelAccumulator(
                    panelKey,
                    reader.GetString(1),
                    (MetricCategory)reader.GetInt32(4),
                    (MetricUnit)reader.GetInt32(5));
                panelMap.Add(panelKey, panel);
            }

            var point = new MetricPoint(
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6)),
                reader.GetDouble(7));

            panel.AddPoint(reader.GetString(2), reader.GetString(3), point);
        }

        return panelMap.Values
            .Select(panel => panel.ToHistory())
            .OrderBy(panel => panel.PanelTitle, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task PruneAsync(VaktrConfig config, CancellationToken cancellationToken)
    {
        var connectionString = EnsureInitialized();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _config = config.Normalize();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CompactAndPruneAsync(connection, _config, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            _nextMaintenanceUtc = DateTimeOffset.UtcNow.Add(MaintenanceInterval);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    private string EnsureInitialized() =>
        _connectionString ?? throw new InvalidOperationException("The SQLite store has not been initialized.");

    private async Task MaybeMaintainAsync(SqliteConnection connection, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        if (_config is null || timestamp < _nextMaintenanceUtc)
        {
            return;
        }

        await CompactAndPruneAsync(connection, _config, timestamp, cancellationToken).ConfigureAwait(false);
        _nextMaintenanceUtc = timestamp.Add(MaintenanceInterval);
    }

    private static async Task CompactAndPruneAsync(
        SqliteConnection connection,
        VaktrConfig config,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var retentionCutoffMs = now.AddHours(-config.MaxRetentionHours).ToUnixTimeMilliseconds();
        var rawCutoffMs = now.Subtract(RawResolutionWindow).ToUnixTimeMilliseconds();

        if (retentionCutoffMs < rawCutoffMs)
        {
            var compactCommand = connection.CreateCommand();
            compactCommand.CommandText =
                """
                INSERT OR REPLACE INTO metric_rollups_1m (
                    panel_key,
                    panel_title,
                    series_key,
                    series_name,
                    category,
                    unit,
                    timestamp_ms,
                    value
                )
                SELECT
                    panel_key,
                    panel_title,
                    series_key,
                    series_name,
                    category,
                    unit,
                    (timestamp_ms / $bucketMs) * $bucketMs AS timestamp_ms,
                    AVG(value) AS value
                FROM metric_samples
                WHERE timestamp_ms >= $retentionCutoff
                  AND timestamp_ms < $rawCutoff
                GROUP BY
                    panel_key,
                    panel_title,
                    series_key,
                    series_name,
                    category,
                    unit,
                    (timestamp_ms / $bucketMs);
                """;
            compactCommand.Parameters.AddWithValue("$retentionCutoff", retentionCutoffMs);
            compactCommand.Parameters.AddWithValue("$rawCutoff", rawCutoffMs);
            compactCommand.Parameters.AddWithValue("$bucketMs", MinuteBucketMilliseconds);
            await compactCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var rawDeleteCutoffMs = Math.Max(retentionCutoffMs, rawCutoffMs);

        var deleteRawCommand = connection.CreateCommand();
        deleteRawCommand.CommandText = "DELETE FROM metric_samples WHERE timestamp_ms < $cutoff;";
        deleteRawCommand.Parameters.AddWithValue("$cutoff", rawDeleteCutoffMs);
        await deleteRawCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var deleteRollupCommand = connection.CreateCommand();
        deleteRollupCommand.CommandText = "DELETE FROM metric_rollups_1m WHERE timestamp_ms < $cutoff;";
        deleteRollupCommand.Parameters.AddWithValue("$cutoff", retentionCutoffMs);
        await deleteRollupCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var maintenanceCommand = connection.CreateCommand();
        maintenanceCommand.CommandText =
            """
            PRAGMA optimize;
            PRAGMA wal_checkpoint(PASSIVE);
            PRAGMA incremental_vacuum(64);
            """;
        await maintenanceCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class PanelAccumulator
    {
        private readonly Dictionary<string, SeriesAccumulator> _series = new(StringComparer.OrdinalIgnoreCase);

        public PanelAccumulator(string panelKey, string panelTitle, MetricCategory category, MetricUnit unit)
        {
            PanelKey = panelKey;
            PanelTitle = panelTitle;
            Category = category;
            Unit = unit;
        }

        public string PanelKey { get; }

        public string PanelTitle { get; }

        public MetricCategory Category { get; }

        public MetricUnit Unit { get; }

        public void AddPoint(string seriesKey, string seriesName, MetricPoint point)
        {
            if (!_series.TryGetValue(seriesKey, out var series))
            {
                series = new SeriesAccumulator(seriesKey, seriesName);
                _series.Add(seriesKey, series);
            }

            series.Points.Add(point);
        }

        public MetricSeriesHistory ToHistory() =>
            new(
                PanelKey,
                PanelTitle,
                Category,
                Unit,
                _series.Values
                    .Select(series => new MetricSeriesHistoryItem(series.SeriesKey, series.SeriesName, series.Points.ToArray()))
                    .ToArray());
    }

    private sealed class SeriesAccumulator
    {
        public SeriesAccumulator(string seriesKey, string seriesName)
        {
            SeriesKey = seriesKey;
            SeriesName = seriesName;
        }

        public string SeriesKey { get; }

        public string SeriesName { get; }

        public List<MetricPoint> Points { get; } = [];
    }
}
