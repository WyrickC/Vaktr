using Vaktr.Core.Models;

namespace Vaktr.Core.Interfaces;

public interface IMetricCollector : IAsyncDisposable
{
    Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken);
}

public interface IMetricStore : IAsyncDisposable
{
    Task InitializeAsync(VaktrConfig config, CancellationToken cancellationToken);

    Task AppendSnapshotAsync(MetricSnapshot snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<MetricSeriesHistory>> LoadHistoryAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken);

    Task PruneAsync(VaktrConfig config, CancellationToken cancellationToken);
}

public interface IConfigStore
{
    Task<VaktrConfig> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(VaktrConfig config, CancellationToken cancellationToken);
}
