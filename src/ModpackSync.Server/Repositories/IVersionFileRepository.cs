using ModpackSync.Server.Entities;

namespace ModpackSync.Server.Repositories;

public interface IVersionFileRepository
{
    Task ReplaceAsync(
        Guid versionId,
        IReadOnlyCollection<VersionFile> files,
        CancellationToken cancellationToken = default);
}