namespace ModpackSync.Contracts.Server.Versions;

public sealed record VersionManifestResponse
{
    public Guid VersionId { get; init; }

    public Guid PackId { get; init; }

    public required string VersionLabel { get; init; }

    public string? ReleaseNotes { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public bool IsComplete { get; init; }

    public bool IsPublished { get; init; }

    public int FileCount { get; init; }

    public long TotalSize { get; init; }

    public required IReadOnlyList<ManifestFileResponse> Files
    {
        get;
        init;
    }
}