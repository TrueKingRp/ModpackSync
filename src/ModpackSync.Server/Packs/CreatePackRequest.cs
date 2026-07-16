namespace ModpackSync.Contracts.Server.Packs;

public sealed record CreatePackRequest
{
    public required string Name { get; init; }
}