using System.Diagnostics;
using System.Text;
using AI.CameraRecorder.Models;

namespace AI.CameraRecorder.Services;

public sealed class FfmpegService
{
    private Process? _recordingProcess;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FfmpegService()
    {
        var baseDir = AppContext.BaseDirectory;
        _ffmpegPath = ResolveExecutable(Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe"), "ffmpeg.exe");
        _ffprobePath = ResolveExecutable(Path.Combine(baseDir, "tools", "ffmpeg", "ffprobe.exe"), "ffprobe.exe");
    }

    public bool IsAvailable => File.Exists(_ffmpegPath) || Path.GetFileName(_ffmpegPath).Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase);

    public async Task<string> StartRecordingAsync(CameraDeviceInfo camera, RecordingProfile profile, string outputDirectory, CancellationToken cancellationToken = default)
    {
        if (_recordingProcess is { HasExited: false })
            throw new InvalidOperationException("Recording is already active.");

        Directory.CreateDirectory(outputDirectory);
        var output = Path.Combine(outputDirectory, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mkv");
        var encoder = profile.Codec.Equals("AV1", StringComparison.OrdinalIgnoreCase) ? "av1_nvenc" : "hevc_nvenc";
        var args = new[]
        {
            "-hide_banner", "-y", "-f", "dshow",
            "-video_size", $"{profile.Width}x{profile.Height}",
            "-framerate", profile.FramesPerSecond.ToString(),
            "-i", $"video={QuoteDevice(camera.DisplayName)}",
            "-c:v", encoder, "-preset", "p6", "-tune", "hq",
            "-rc", "vbr", "-cq", "18", "-b:v", "0",
            "-pix_fmt", "yuv420p", output
        };

        _recordingProcess = StartProcess(_ffmpegPath, args, redirectInput: true);
        await Task.Delay(300, cancellationToken);
        if (_recordingProcess.HasExited)
        {
            var error = await _recordingProcess.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"FFmpeg failed to start recording: {error}");
        }
        return output;
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        var process = _recordingProcess;
        if (process is null || process.HasExited) return;
        await process.StandardInput.WriteLineAsync("q");
        await process.StandardInput.FlushAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }
        finally { _recordingProcess = null; }
    }

    public async Task<VideoProbe> ProbeAsync(string source, CancellationToken cancellationToken = default)
    {
        var args = new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height,avg_frame_rate,duration", "-of", "default=noprint_wrappers=1", source };
        using var process = StartProcess(_ffprobePath, args, redirectInput: false);
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException(error);

        var values = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        return new VideoProbe(
            int.Parse(values["width"]),
            int.Parse(values["height"]),
            ParseFrameRate(values.GetValueOrDefault("avg_frame_rate", "30/1")));
    }

    public async Task RunAsync(IEnumerable<string> arguments, IProgress<string>? log = null, CancellationToken cancellationToken = default)
    {
        using var process = StartProcess(_ffmpegPath, arguments, redirectInput: false);
        while (!process.StandardError.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await process.StandardError.ReadLineAsync(cancellationToken);
            if (line is not null) log?.Report(line);
        }
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}.");
    }

    private static Process StartProcess(string fileName, IEnumerable<string> arguments, bool redirectInput)
    {
        var info = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = redirectInput,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new InvalidOperationException($"Unable to start {fileName}.");
    }

    private static string ResolveExecutable(string bundled, string fallback) => File.Exists(bundled) ? bundled : fallback;
    private static string QuoteDevice(string name) => $"\"{name.Replace("\"", "\\\"")}\"";
    private static double ParseFrameRate(string value)
    {
        var parts = value.Split('/');
        return parts.Length == 2 && double.TryParse(parts[0], out var n) && double.TryParse(parts[1], out var d) && d != 0 ? n / d : 30;
    }
}

public sealed record VideoProbe(int Width, int Height, double FramesPerSecond);
