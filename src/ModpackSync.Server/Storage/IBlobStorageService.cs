namespace ModpackSync.Server.Storage;

public interface IBlobStorageService
{
    string RootDirectory { get; }

    bool Exists(
        string sha256);

    string GetBlobPath(
        string sha256);

    Task<BlobWriteResult> StoreAsync(
        Stream source,
        string expectedSha256,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string sha256,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string sha256,
        CancellationToken cancellationToken = default);
}