namespace ModpackSync.Contracts.Server.Versions;

public sealed record ServerVersionResponse
{
    public required Guid Id { get; init; }

    public required Guid PackId { get; init; }

    public required string VersionLabel { get; init; }

    public string? ReleaseNotes { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required bool IsComplete { get; init; }

    public required bool IsPublished { get; init; }

    public required int FileCount { get; init; }
}