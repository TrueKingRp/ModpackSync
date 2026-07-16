using ModpackSync.Contracts.Server.Versions;

namespace ModpackSync.Server.Services;

public interface IVersionService
{
    Task<IReadOnlyList<ServerVersionResponse>> GetByPackIdAsync(
        Guid packId,
        CancellationToken cancellationToken = default);

    Task<ServerVersionResponse?> GetByIdAsync(
        Guid versionId,
        CancellationToken cancellationToken = default);

    Task<ServerVersionResponse> CreateAsync(
        Guid packId,
        CreateServerVersionRequest request,
        CancellationToken cancellationToken = default);
}