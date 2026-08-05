namespace AI.CameraRecorder.Models;

public sealed record CameraDeviceInfo(string Id, string DisplayName);

public sealed record RecordingProfile(int Width, int Height, int FramesPerSecond, string Codec)
{
    public static RecordingProfile Parse(string resolution, string fps, string codec)
    {
        var dimensions = resolution.Split('x', StringSplitOptions.TrimEntries);
        if (dimensions.Length != 2 || !int.TryParse(dimensions[0], out var width) || !int.TryParse(dimensions[1], out var height))
            throw new ArgumentException("Invalid resolution.", nameof(resolution));
        if (!int.TryParse(fps, out var framesPerSecond) || framesPerSecond is < 1 or > 240)
            throw new ArgumentException("Invalid FPS.", nameof(fps));
        return new(width, height, framesPerSecond, codec);
    }
}
