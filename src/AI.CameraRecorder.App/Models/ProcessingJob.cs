using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AI.CameraRecorder.Models;

public enum ProcessingJobState { Queued, Running, Completed, Failed, Cancelled }

public sealed class ProcessingJob : INotifyPropertyChanged
{
    private ProcessingJobState _state = ProcessingJobState.Queued;
    private double _progress;
    private string? _error;

    public required string SourcePath { get; init; }
    public required string OutputPath { get; init; }
    public bool Denoise { get; init; }
    public bool Sharpen { get; init; }
    public bool RestoreFaces { get; init; }
    public string Codec { get; init; } = "H.265";

    public ProcessingJobState State { get => _state; set { _state = value; Raise(); Raise(nameof(ProgressText)); } }
    public double Progress { get => _progress; set { _progress = Math.Clamp(value, 0, 100); Raise(); Raise(nameof(ProgressText)); } }
    public string ProgressText => $"{Progress:0}%";
    public string? Error { get => _error; set { _error = value; Raise(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
