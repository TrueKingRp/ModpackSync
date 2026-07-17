using Microsoft.EntityFrameworkCore;
using ModpackSync.Server.Data;
using ModpackSync.Server.Entities;

namespace ModpackSync.Server.Repositories;

public sealed class VersionFileRepository :
    IVersionFileRepository
{
    private readonly ModpackSyncDbContext _database;

    public VersionFileRepository(
        ModpackSyncDbContext database)
    {
        _database = database;
    }

    public async Task ReplaceAsync(
        Guid versionId,
        IReadOnlyCollection<VersionFile> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            files);

        await using var transaction =
            await _database.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            await _database.VersionFiles
                .Where(
                    file =>
                        file.VersionId == versionId)
                .ExecuteDeleteAsync(
                    cancellationToken);

            if (files.Count > 0)
            {
                _database.VersionFiles.AddRange(
                    files);

                await _database.SaveChangesAsync(
                    cancellationToken);
            }

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }
}