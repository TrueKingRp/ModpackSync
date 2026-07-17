using ModpackSync.Server.Entities;

namespace ModpackSync.Server.Repositories;

public interface IStoredFileRepository
{
    Task<StoredFile?> GetBySha256Async(
        string sha256,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, StoredFile>> GetByHashesAsync(
        IEnumerable<string> sha256Hashes,
        CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetExistingHashesAsync(
        IEnumerable<string> sha256Hashes,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        StoredFile storedFile,
        CancellationToken cancellationToken = default);
}