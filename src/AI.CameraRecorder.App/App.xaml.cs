using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using AI.CameraRecorder.Services;

namespace AI.CameraRecorder;

public partial class App : Application
{
    private readonly IHost _host;
    private Window? _window;

    public App()
    {
        InitializeComponent();
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<CameraDiscoveryService>();
                services.AddSingleton<FfmpegService>();
                services.AddSingleton<AiPipelineService>();
                services.AddSingleton<JobQueueService>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();
        _window = _host.Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
