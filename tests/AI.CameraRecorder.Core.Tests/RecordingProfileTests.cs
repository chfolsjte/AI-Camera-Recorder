using AI.CameraRecorder.Models;

namespace AI.CameraRecorder.Core.Tests;

public sealed class RecordingProfileTests
{
    [Theory]
    [InlineData("1920x1080", "30", "H.265", 1920, 1080, 30)]
    [InlineData("2560x1440", "60", "AV1", 2560, 1440, 60)]
    [InlineData("3840x2160", "30", "H.265", 3840, 2160, 30)]
    public void Parse_valid_profile(string resolution, string fps, string codec, int width, int height, int expectedFps)
    {
        var profile = RecordingProfile.Parse(resolution, fps, codec);
        Assert.Equal(width, profile.Width);
        Assert.Equal(height, profile.Height);
        Assert.Equal(expectedFps, profile.FramesPerSecond);
        Assert.Equal(codec, profile.Codec);
    }

    [Theory]
    [InlineData("bad", "30")]
    [InlineData("1920x1080", "0")]
    [InlineData("1920x1080", "300")]
    public void Parse_rejects_invalid_profile(string resolution, string fps)
        => Assert.Throws<ArgumentException>(() => RecordingProfile.Parse(resolution, fps, "H.265"));

    [Fact]
    public void Processing_job_clamps_progress()
    {
        var job = new ProcessingJob { SourcePath = "in.mp4", OutputPath = "out.mp4" };
        job.Progress = 120;
        Assert.Equal(100, job.Progress);
        job.Progress = -10;
        Assert.Equal(0, job.Progress);
    }
}
