using ModpackSync.Contracts.Server.Versions;
using ModpackSync.Server.Entities;
using ModpackSync.Server.Repositories;

namespace ModpackSync.Server.Services;

public sealed class VersionService : IVersionService
{
    private readonly IPackRepository _packRepository;
    private readonly IVersionRepository _versionRepository;

    public VersionService(
        IPackRepository packRepository,
        IVersionRepository versionRepository)
    {
        _packRepository = packRepository;
        _versionRepository = versionRepository;
    }

    public async Task<IReadOnlyList<ServerVersionResponse>> GetByPackIdAsync(
        Guid packId,
        CancellationToken cancellationToken = default)
    {
        ServerPack? pack =
            await _packRepository.GetByIdAsync(
                packId,
                cancellationToken);

        if (pack is null)
        {
            throw new KeyNotFoundException(
                $"No pack exists with ID '{packId}'.");
        }

        IReadOnlyList<ServerPackVersion> versions =
            await _versionRepository.GetByPackIdAsync(
                packId,
                cancellationToken);

        return versions
            .Select(MapResponse)
            .ToList();
    }

    public async Task<ServerVersionResponse?> GetByIdAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        ServerPackVersion? version =
            await _versionRepository.GetByIdAsync(
                versionId,
                cancellationToken);

        return version is null
            ? null
            : MapResponse(version);
    }

    public async Task<ServerVersionResponse> CreateAsync(
        Guid packId,
        CreateServerVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(
                request.VersionLabel))
        {
            throw new ArgumentException(
                "The version label cannot be empty.",
                nameof(request));
        }

        ServerPack? pack =
            await _packRepository.GetByIdAsync(
                packId,
                cancellationToken);

        if (pack is null)
        {
            throw new KeyNotFoundException(
                $"No pack exists with ID '{packId}'.");
        }

        string versionLabel =
            request.VersionLabel.Trim();

        ServerPackVersion? existingVersion =
            await _versionRepository.GetByLabelAsync(
                packId,
                versionLabel,
                cancellationToken);

        if (existingVersion is not null)
        {
            return MapResponse(existingVersion);
        }

        var version = new ServerPackVersion
        {
            Id = Guid.NewGuid(),
            PackId = packId,
            VersionLabel = versionLabel,
            ReleaseNotes = NormaliseOptionalValue(
                request.ReleaseNotes),
            CreatedAt = DateTimeOffset.UtcNow,
            IsComplete = false,
            IsPublished = false
        };

        ServerPackVersion createdVersion =
            await _versionRepository.AddAsync(
                version,
                cancellationToken);

        return MapResponse(createdVersion);
    }

    private static ServerVersionResponse MapResponse(
        ServerPackVersion version)
    {
        return new ServerVersionResponse
        {
            Id = version.Id,
            PackId = version.PackId,
            VersionLabel = version.VersionLabel,
            ReleaseNotes = version.ReleaseNotes,
            CreatedAt = version.CreatedAt,
            IsComplete = version.IsComplete,
            IsPublished = version.IsPublished,
            FileCount = version.Files.Count
        };
    }

    private static string? NormaliseOptionalValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}