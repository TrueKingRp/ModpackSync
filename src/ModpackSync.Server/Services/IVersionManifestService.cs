using ModpackSync.Contracts.Server.Versions;

namespace ModpackSync.Server.Services;

public interface IVersionManifestService
{
    Task<VersionManifestResponse?> GetAsync(
        Guid versionId,
        CancellationToken cancellationToken = default);
}