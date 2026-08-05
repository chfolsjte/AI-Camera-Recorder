using System.Collections.ObjectModel;
using AI.CameraRecorder.Models;
using AI.CameraRecorder.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AI.CameraRecorder;

public sealed partial class MainWindow : Window
{
    private readonly CameraDiscoveryService _cameras;
    private readonly FfmpegService _ffmpeg;
    private readonly AiPipelineService _pipeline;
    private readonly JobQueueService _queue;
    private string? _currentRecording;

    public ObservableCollection<ProcessingJob> Jobs => _queue.Jobs;

    public MainWindow(CameraDiscoveryService cameras, FfmpegService ffmpeg, AiPipelineService pipeline, JobQueueService queue)
    {
        InitializeComponent();
        _cameras = cameras;
        _ffmpeg = ffmpeg;
        _pipeline = pipeline;
        _queue = queue;
        Activated += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        Activated -= async (_, _) => await InitializeAsync();
        FfmpegStatus.Text = $"FFmpeg：{(_ffmpeg.IsAvailable ? "可用" : "缺少")}";
        AiStatus.Text = $"Real-ESRGAN：{(_pipeline.RealEsrganAvailable ? "可用" : "尚未安裝")}；GFPGAN：{(_pipeline.GfpganAvailable ? "可用" : "選配")}";
        GpuStatus.Text = "NVIDIA GPU：由 NVENC / ONNX Runtime / TensorRT-RTX 啟動時驗證";
        await RefreshCamerasAsync();
    }

    private async void RefreshCameras_Click(object sender, RoutedEventArgs e) => await RefreshCamerasAsync();

    private async Task RefreshCamerasAsync()
    {
        try
        {
            StatusText.Text = "正在掃描攝影機…";
            var devices = await _cameras.FindAllAsync();
            CameraBox.ItemsSource = devices;
            CameraBox.SelectedIndex = devices.Count > 0 ? 0 : -1;
            StatusText.Text = devices.Count > 0 ? $"找到 {devices.Count} 台攝影機" : "未找到攝影機";
        }
        catch (Exception ex) { StatusText.Text = $"攝影機掃描失敗：{ex.Message}"; }
    }

    private async void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        if (CameraBox.SelectedItem is not CameraDeviceInfo camera) return;
        try
        {
            var profile = RecordingProfile.Parse((string)ResolutionBox.SelectedItem, (string)FpsBox.SelectedItem, (string)CodecBox.SelectedItem);
            var recordings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AI Camera Recorder", "Recordings");
            _currentRecording = await _ffmpeg.StartRecordingAsync(camera, profile, recordings);
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusText.Text = $"錄影中：{Path.GetFileName(_currentRecording)}";
        }
        catch (Exception ex) { StatusText.Text = $"錄影啟動失敗：{ex.Message}"; }
    }

    private async void StopRecording_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _ffmpeg.StopRecordingAsync();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusText.Text = "錄影完成";
            if (_currentRecording is not null) Enqueue(_currentRecording);
        }
        catch (Exception ex) { StatusText.Text = $"停止錄影失敗：{ex.Message}"; }
    }

    private async void AddVideo_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".mp4"); picker.FileTypeFilter.Add(".mkv"); picker.FileTypeFilter.Add(".mov"); picker.FileTypeFilter.Add(".avi");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is not null) Enqueue(file.Path);
    }

    private void Enqueue(string source)
    {
        var outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AI Camera Recorder", "Output4K");
        var output = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(source) + "_AI_4K.mp4");
        _queue.Enqueue(new ProcessingJob
        {
            SourcePath = source,
            OutputPath = output,
            Denoise = DenoiseCheck.IsChecked == true,
            Sharpen = SharpenCheck.IsChecked == true,
            RestoreFaces = FaceCheck.IsChecked == true,
            Codec = (string)CodecBox.SelectedItem
        });
        StatusText.Text = "已加入 AI 處理佇列";
    }
}
