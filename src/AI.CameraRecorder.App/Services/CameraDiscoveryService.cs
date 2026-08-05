using AI.CameraRecorder.Models;
using Windows.Media.Capture.Frames;

namespace AI.CameraRecorder.Services;

public sealed class CameraDiscoveryService
{
    public async Task<IReadOnlyList<CameraDeviceInfo>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        var groups = await MediaFrameSourceGroup.FindAllAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return groups
            .Where(group => group.SourceInfos.Any(info => info.SourceKind is MediaFrameSourceKind.Color or MediaFrameSourceKind.Infrared))
            .Select(group => new CameraDeviceInfo(group.Id, group.DisplayName))
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
