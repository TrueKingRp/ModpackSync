using ModpackSync.Contracts.Manifests;

namespace ModpackSync.Contracts.Sync;

public sealed record ManifestComparison
{
    public required IReadOnlyList<ManifestFile> AddedFiles { get; init; }

    public required IReadOnlyList<ManifestFile> ModifiedFiles { get; init; }

    public required IReadOnlyList<ManifestFile> DeletedFiles { get; init; }

    public required IReadOnlyList<ManifestFile> UnchangedFiles { get; init; }
}