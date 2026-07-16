using Microsoft.EntityFrameworkCore;
using ModpackSync.Server.Data;
using ModpackSync.Server.Entities;

namespace ModpackSync.Server.Repositories;

public sealed class VersionRepository : IVersionRepository
{
    private readonly ModpackSyncDbContext _database;

    public VersionRepository(
        ModpackSyncDbContext database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<ServerPackVersion>> GetByPackIdAsync(
        Guid packId,
        CancellationToken cancellationToken = default)
    {
        List<ServerPackVersion> versions =
            await _database.Versions
                .AsNoTracking()
                .Include(version => version.Files)
                .Where(version => version.PackId == packId)
                .ToListAsync(cancellationToken);

        return versions
            .OrderByDescending(version => version.CreatedAt)
            .ToList();
    }

    public async Task<ServerPackVersion?> GetByIdAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        return await _database.Versions
            .AsNoTracking()
            .Include(version => version.Files)
            .FirstOrDefaultAsync(
                version => version.Id == versionId,
                cancellationToken);
    }

    public async Task<ServerPackVersion?> GetByLabelAsync(
        Guid packId,
        string versionLabel,
        CancellationToken cancellationToken = default)
    {
        string normalisedLabel =
            versionLabel.Trim();

        return await _database.Versions
            .AsNoTracking()
            .Include(version => version.Files)
            .FirstOrDefaultAsync(
                version =>
                    version.PackId == packId &&
                    version.VersionLabel == normalisedLabel,
                cancellationToken);
    }

    public async Task<ServerPackVersion> AddAsync(
        ServerPackVersion version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        _database.Versions.Add(version);

        await _database.SaveChangesAsync(
            cancellationToken);

        return version;
    }
}