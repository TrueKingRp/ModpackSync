using ModpackSync.Contracts.Server.Packs;

namespace ModpackSync.Server.Services;

public interface IPackService
{
    Task<IReadOnlyList<ServerPackResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ServerPackResponse?> GetByIdAsync(
        Guid packId,
        CancellationToken cancellationToken = default);

    Task<ServerPackResponse> CreateOrGetAsync(
        CreatePackRequest request,
        CancellationToken cancellationToken = default);
}