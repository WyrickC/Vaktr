using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;

namespace Vaktr.Collector;

public sealed class CollectorService : IAsyncDisposable
{
    private static readonly TimeSpan InitialCollectionTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan RecurringCollectionTimeout = TimeSpan.FromSeconds(8);

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
    public event EventHandler<Exception>? CollectionFailed;

    public async Task StartAsync(VaktrConfig config, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);

            await _store.InitializeAsync(config, cancellationToken).ConfigureAwait(false);
            await _store.PruneAsync(config, cancellationToken).ConfigureAwait(false);

            _loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await CollectOnceAsync(_loopCancellation.Token, InitialCollectionTimeout).ConfigureAwait(false);
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

            await CollectOnceAsync(cancellationToken, RecurringCollectionTimeout).ConfigureAwait(false);
        }
    }

    private async Task CollectOnceAsync(CancellationToken cancellationToken, TimeSpan timeout)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);

        try
        {
            var snapshot = await _collector.CollectAsync(timeoutCancellation.Token).ConfigureAwait(false);
            await _store.AppendSnapshotAsync(snapshot, timeoutCancellation.Token).ConfigureAwait(false);
            SnapshotCollected?.Invoke(this, snapshot);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            CollectionFailed?.Invoke(this, new TimeoutException($"Telemetry sampling exceeded {timeout.TotalSeconds:0.#} seconds."));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            CollectionFailed?.Invoke(this, ex);
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
