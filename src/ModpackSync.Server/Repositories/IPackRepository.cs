using ModpackSync.Server.Entities;

namespace ModpackSync.Server.Repositories;

public interface IPackRepository
{
    Task<IReadOnlyList<ServerPack>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ServerPack?> GetByIdAsync(
        Guid packId,
        CancellationToken cancellationToken = default);

    Task<ServerPack?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<ServerPack> AddAsync(
        ServerPack pack,
        CancellationToken cancellationToken = default);
}