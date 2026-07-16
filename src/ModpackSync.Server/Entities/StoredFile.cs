namespace ModpackSync.Server.Entities;

public sealed class StoredFile
{
    public required string Sha256 { get; set; }

    public long Size { get; set; }

    public required string StoragePath { get; set; }

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public List<VersionFile> VersionFiles { get; set; } = [];
}