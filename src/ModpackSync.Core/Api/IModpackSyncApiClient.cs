using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Versions;

namespace ModpackSync.Core.Api;

public interface IModpackSyncApiClient
{
    Task<ApiResult> TestConnectionAsync();

    Task<ApiResult> PublishVersionAsync(
        PackVersion version);

    Task<IReadOnlyList<PackVersion>>
        GetVersionsAsync(
            Guid packId);

    Task<ApiResult> DeleteVersionAsync(
        Guid versionId);
}