namespace ModpackSync.Contracts.Server.Versions;

public sealed record ManifestFileResponse
{
    public required string RelativePath { get; init; }

    public required string Sha256 { get; init; }

    public long Size { get; init; }

    public required string DownloadUrl { get; init; }
}