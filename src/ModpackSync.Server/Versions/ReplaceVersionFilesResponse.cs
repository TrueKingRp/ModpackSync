namespace ModpackSync.Contracts.Server.Versions;

public sealed record ReplaceVersionFilesResponse
{
    public Guid VersionId { get; init; }

    public int FileCount { get; init; }

    public long TotalSize { get; init; }
}