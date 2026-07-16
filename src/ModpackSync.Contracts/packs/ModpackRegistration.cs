namespace ModpackSync.Contracts.Packs;

public sealed record ModpackRegistration
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public required string LocalPath { get; init; }

    public string? MinecraftVersion { get; init; }

    public string? ModLoader { get; init; }

    public string? ModLoaderVersion { get; init; }

    public DateTimeOffset CreatedAt { get; init; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset? LastScannedAt { get; init; }
}