using Microsoft.EntityFrameworkCore;
using ModpackSync.Server.Data;
using ModpackSync.Server.Entities;

namespace ModpackSync.Server.Repositories;

public sealed class StoredFileRepository :
    IStoredFileRepository
{
    private readonly ModpackSyncDbContext _database;

    public StoredFileRepository(
        ModpackSyncDbContext database)
    {
        _database = database;
    }

    public Task<StoredFile?> GetBySha256Async(
        string sha256,
        CancellationToken cancellationToken = default)
    {
        string normalisedHash =
            sha256.Trim()
                .ToLowerInvariant();

        return _database.StoredFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                file =>
                    file.Sha256 == normalisedHash,
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, StoredFile>>
        GetByHashesAsync(
            IEnumerable<string> sha256Hashes,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            sha256Hashes);

        string[] hashes =
            sha256Hashes
                .Where(
                    hash =>
                        !string.IsNullOrWhiteSpace(hash))
                .Select(
                    hash =>
                        hash.Trim()
                            .ToLowerInvariant())
                .Distinct(
                    StringComparer.Ordinal)
                .ToArray();

        if (hashes.Length == 0)
        {
            return new Dictionary<string, StoredFile>(
                StringComparer.Ordinal);
        }

        List<StoredFile> storedFiles =
            await _database.StoredFiles
                .AsNoTracking()
                .Where(
                    file =>
                        hashes.Contains(
                            file.Sha256))
                .ToListAsync(
                    cancellationToken);

        return storedFiles.ToDictionary(
            file => file.Sha256,
            StringComparer.Ordinal);
    }

    public async Task<HashSet<string>> GetExistingHashesAsync(
        IEnumerable<string> sha256Hashes,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, StoredFile> files =
            await GetByHashesAsync(
                sha256Hashes,
                cancellationToken);

        return files.Keys.ToHashSet(
            StringComparer.Ordinal);
    }

    public async Task AddAsync(
        StoredFile storedFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            storedFile);

        _database.StoredFiles.Add(
            storedFile);

        await _database.SaveChangesAsync(
            cancellationToken);
    }
}