using ModpackSync.Contracts.Server.Versions;

namespace ModpackSync.Server.Services;

public interface IVersionFileService
{
    Task<ReplaceVersionFilesResponse> ReplaceAsync(
        Guid versionId,
        ReplaceVersionFilesRequest request,
        CancellationToken cancellationToken = default);
}