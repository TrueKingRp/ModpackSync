namespace ModpackSync.Contracts.Versions;

public sealed record PackVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid PackId { get; init; }

    public required string VersionLabel { get; init; }

    public string? ReleaseNotes { get; init; }

    public required string ManifestPath { get; init; }

    public DateTimeOffset CreatedAt { get; init; } =
        DateTimeOffset.UtcNow;

    public bool IsPublished { get; init; }
}