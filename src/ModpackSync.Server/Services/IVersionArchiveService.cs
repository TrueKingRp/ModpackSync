namespace ModpackSync.Server.Services;

public interface IVersionArchiveService
{
    Task<VersionArchiveResult?> CreateAsync(
        Guid versionId,
        CancellationToken cancellationToken = default);
}