using AI.CameraRecorder.Models;

namespace AI.CameraRecorder.Services;

public sealed class AiPipelineService
{
    private readonly FfmpegService _ffmpeg;
    private readonly string _realEsrgan;
    private readonly string _gfpgan;

    public AiPipelineService(FfmpegService ffmpeg)
    {
        _ffmpeg = ffmpeg;
        var baseDir = AppContext.BaseDirectory;
        _realEsrgan = Path.Combine(baseDir, "tools", "realesrgan", "realesrgan-ncnn-vulkan.exe");
        _gfpgan = Path.Combine(baseDir, "tools", "gfpgan", "gfpgan-ncnn-vulkan.exe");
    }

    public bool RealEsrganAvailable => File.Exists(_realEsrgan);
    public bool GfpganAvailable => File.Exists(_gfpgan);

    public async Task ProcessAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var probe = await _ffmpeg.ProbeAsync(job.SourcePath, cancellationToken);
        var work = Path.Combine(Path.GetTempPath(), "AI-Camera-Recorder", Guid.NewGuid().ToString("N"));
        var inputFrames = Path.Combine(work, "input");
        var enhancedFrames = Path.Combine(work, "enhanced");
        var faceFrames = Path.Combine(work, "faces");
        Directory.CreateDirectory(inputFrames);
        Directory.CreateDirectory(enhancedFrames);

        try
        {
            job.State = ProcessingJobState.Running;
            job.Progress = 5;
            await _ffmpeg.RunAsync(new[] { "-hide_banner", "-y", "-i", job.SourcePath, "-vsync", "0", Path.Combine(inputFrames, "frame_%08d.png") }, cancellationToken: cancellationToken);

            job.Progress = 20;
            if (!RealEsrganAvailable)
                throw new FileNotFoundException("Real-ESRGAN runtime is missing.", _realEsrgan);

            await RunToolAsync(_realEsrgan, new[]
            {
                "-i", inputFrames, "-o", enhancedFrames,
                "-n", "realesr-general-x4v3", "-s", "4", "-f", "png",
                "-dn", job.Denoise ? "0.25" : "0", "-t", "256", "-j", "2:2:2"
            }, cancellationToken);

            var finalFrames = enhancedFrames;
            job.Progress = 70;
            if (job.RestoreFaces)
            {
                if (!GfpganAvailable)
                    throw new FileNotFoundException("GFPGAN runtime is missing.", _gfpgan);
                Directory.CreateDirectory(faceFrames);
                await RunToolAsync(_gfpgan, new[] { "-i", enhancedFrames, "-o", faceFrames, "-s", "1" }, cancellationToken);
                finalFrames = faceFrames;
            }

            job.Progress = 82;
            Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);
            var encoder = job.Codec.Equals("AV1", StringComparison.OrdinalIgnoreCase) ? "av1_nvenc" : "hevc_nvenc";
            var filters = "scale=3840:2160:flags=lanczos" + (job.Sharpen ? ",unsharp=5:5:0.45:5:5:0.0" : string.Empty) + ",format=yuv420p";
            await _ffmpeg.RunAsync(new[]
            {
                "-hide_banner", "-y", "-framerate", probe.FramesPerSecond.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                "-i", Path.Combine(finalFrames, "frame_%08d.png"), "-i", job.SourcePath,
                "-map", "0:v:0", "-map", "1:a?", "-vf", filters,
                "-c:v", encoder, "-preset", "p6", "-tune", "hq", "-rc", "vbr", "-cq", "19", "-b:v", "0",
                "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", job.OutputPath
            }, cancellationToken: cancellationToken);

            job.Progress = 100;
            job.State = ProcessingJobState.Completed;
        }
        catch (OperationCanceledException)
        {
            job.State = ProcessingJobState.Cancelled;
            throw;
        }
        catch (Exception ex)
        {
            job.Error = ex.Message;
            job.State = ProcessingJobState.Failed;
            throw;
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); } catch { }
        }
    }

    private static async Task RunToolAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var info = new System.Diagnostics.ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException($"Unable to start {executable}.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(await process.StandardError.ReadToEndAsync(cancellationToken));
    }
}
