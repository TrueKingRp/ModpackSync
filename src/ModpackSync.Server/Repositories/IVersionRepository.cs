using ModpackSync.Server.Entities;

namespace ModpackSync.Server.Repositories;

public interface IVersionRepository
{
    Task<IReadOnlyList<ServerPackVersion>> GetByPackIdAsync(
        Guid packId,
        CancellationToken cancellationToken = default);

    Task<ServerPackVersion?> GetByIdAsync(
        Guid versionId,
        CancellationToken cancellationToken = default);

    Task<ServerPackVersion?> GetByLabelAsync(
        Guid packId,
        string versionLabel,
        CancellationToken cancellationToken = default);

    Task<ServerPackVersion> AddAsync(
        ServerPackVersion version,
        CancellationToken cancellationToken = default);
}