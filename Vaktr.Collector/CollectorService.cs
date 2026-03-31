using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;

namespace Vaktr.Collector;

public sealed class CollectorService : IAsyncDisposable
{
    private readonly IMetricCollector _collector;
    private readonly IMetricStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _loopCancellation;
    private PeriodicTimer? _timer;
    private Task? _loopTask;

    public CollectorService(IMetricCollector collector, IMetricStore store)
    {
        _collector = collector;
        _store = store;
    }

    public event EventHandler<MetricSnapshot>? SnapshotCollected;

    public async Task StartAsync(VaktrConfig config, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);

            await _store.InitializeAsync(config, cancellationToken).ConfigureAwait(false);
            await _store.PruneAsync(config, cancellationToken).ConfigureAwait(false);

            _loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(config.ScrapeIntervalSeconds));
            _loopTask = Task.Run(() => RunLoopAsync(_timer, _loopCancellation.Token));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await _collector.DisposeAsync().ConfigureAwait(false);
        await _store.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task RunLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        await CollectOnceAsync(cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await CollectOnceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CollectOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _collector.CollectAsync(cancellationToken).ConfigureAwait(false);
            await _store.AppendSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            SnapshotCollected?.Invoke(this, snapshot);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task StopInternalAsync()
    {
        if (_loopCancellation is not null)
        {
            await _loopCancellation.CancelAsync().ConfigureAwait(false);
        }

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _timer?.Dispose();
        _loopCancellation?.Dispose();

        _loopTask = null;
        _timer = null;
        _loopCancellation = null;
    }
}
