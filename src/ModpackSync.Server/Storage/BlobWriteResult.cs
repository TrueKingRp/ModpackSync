namespace ModpackSync.Server.Storage;

public sealed record BlobWriteResult
{
    public required string Sha256 { get; init; }

    public required string StoragePath { get; init; }

    public long Size { get; init; }

    public bool AlreadyExisted { get; init; }
}