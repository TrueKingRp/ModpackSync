using ModpackSync.Contracts.Server.Packs;
using ModpackSync.Server.Entities;
using ModpackSync.Server.Repositories;

namespace ModpackSync.Server.Services;

public sealed class PackService : IPackService
{
    private readonly IPackRepository _packRepository;

    public PackService(
        IPackRepository packRepository)
    {
        _packRepository = packRepository;
    }

    public async Task<IReadOnlyList<ServerPackResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerPack> packs =
            await _packRepository.GetAllAsync(
                cancellationToken);

        return packs
            .Select(MapResponse)
            .ToList();
    }

    public async Task<ServerPackResponse?> GetByIdAsync(
        Guid packId,
        CancellationToken cancellationToken = default)
    {
        ServerPack? pack =
            await _packRepository.GetByIdAsync(
                packId,
                cancellationToken);

        return pack is null
            ? null
            : MapResponse(pack);
    }

    public async Task<ServerPackResponse> CreateOrGetAsync(
        CreatePackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException(
                "The pack name cannot be empty.",
                nameof(request));
        }

        string packName = request.Name.Trim();

        IReadOnlyList<ServerPack> existingPacks =
            await _packRepository.GetAllAsync(
                cancellationToken);

        ServerPack? existingPack =
            existingPacks.FirstOrDefault(pack =>
                pack.Name.Equals(
                    packName,
                    StringComparison.OrdinalIgnoreCase));

        if (existingPack is not null)
        {
            return MapResponse(existingPack);
        }

        var newPack = new ServerPack
        {
            Id = Guid.NewGuid(),
            Name = packName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        ServerPack createdPack =
            await _packRepository.AddAsync(
                newPack,
                cancellationToken);

        return MapResponse(createdPack);
    }

    private static ServerPackResponse MapResponse(
        ServerPack pack)
    {
        return new ServerPackResponse
        {
            Id = pack.Id,
            Name = pack.Name,
            CreatedAt = pack.CreatedAt,
            VersionCount = pack.Versions.Count
        };
    }
}