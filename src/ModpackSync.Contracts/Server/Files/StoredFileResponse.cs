namespace ModpackSync.Contracts.Server.Files;

public sealed record StoredFileResponse
{
    public required string Sha256 { get; init; }

    public long Size { get; init; }

    public bool AlreadyExisted { get; init; }

    public required string DownloadUrl { get; init; }
}