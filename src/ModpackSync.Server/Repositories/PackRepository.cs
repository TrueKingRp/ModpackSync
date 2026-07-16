using Microsoft.EntityFrameworkCore;
using ModpackSync.Server.Data;
using ModpackSync.Server.Entities;

namespace ModpackSync.Server.Repositories;

public sealed class PackRepository : IPackRepository
{
    private readonly ModpackSyncDbContext _database;

    public PackRepository(
        ModpackSyncDbContext database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<ServerPack>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _database.Packs
            .AsNoTracking()
            .Include(pack => pack.Versions)
            .OrderBy(pack => pack.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServerPack?> GetByIdAsync(
        Guid packId,
        CancellationToken cancellationToken = default)
    {
        return await _database.Packs
            .AsNoTracking()
            .Include(pack => pack.Versions)
            .FirstOrDefaultAsync(
                pack => pack.Id == packId,
                cancellationToken);
    }

    public async Task<ServerPack?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        string normalisedName = name.Trim();

        return await _database.Packs
            .AsNoTracking()
            .Include(pack => pack.Versions)
            .FirstOrDefaultAsync(
                pack => pack.Name == normalisedName,
                cancellationToken);
    }

    public async Task<ServerPack> AddAsync(
        ServerPack pack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);

        _database.Packs.Add(pack);

        await _database.SaveChangesAsync(
            cancellationToken);

        return pack;
    }
}