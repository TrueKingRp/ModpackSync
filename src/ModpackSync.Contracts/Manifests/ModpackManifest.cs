namespace ModpackSync.Contracts.Manifests;

public sealed record ModpackManifest
{
    public required string PackName { get; init; }

    public int Version { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public required IReadOnlyList<ManifestFile> Files { get; init; }
}