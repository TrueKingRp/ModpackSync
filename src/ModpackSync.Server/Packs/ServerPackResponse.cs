namespace ModpackSync.Contracts.Server.Packs;

public sealed record ServerPackResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required int VersionCount { get; init; }
}