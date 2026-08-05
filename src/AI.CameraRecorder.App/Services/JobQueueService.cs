using System.Collections.ObjectModel;
using System.Threading.Channels;
using AI.CameraRecorder.Models;

namespace AI.CameraRecorder.Services;

public sealed class JobQueueService : IAsyncDisposable
{
    private readonly AiPipelineService _pipeline;
    private readonly Channel<ProcessingJob> _channel = Channel.CreateUnbounded<ProcessingJob>();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;

    public ObservableCollection<ProcessingJob> Jobs { get; } = new();

    public JobQueueService(AiPipelineService pipeline)
    {
        _pipeline = pipeline;
        _worker = Task.Run(WorkerAsync);
    }

    public void Enqueue(ProcessingJob job)
    {
        Jobs.Add(job);
        if (!_channel.Writer.TryWrite(job))
            throw new InvalidOperationException("Unable to queue processing job.");
    }

    private async Task WorkerAsync()
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(_shutdown.Token))
            {
                try { await _pipeline.ProcessAsync(job, _shutdown.Token); }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { break; }
                catch { }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _shutdown.Cancel();
        try { await _worker; } catch (OperationCanceledException) { }
        _shutdown.Dispose();
    }
}
