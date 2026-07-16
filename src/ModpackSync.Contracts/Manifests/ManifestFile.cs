namespace ModpackSync.Contracts.Manifests;

public sealed record ManifestFile(
    string RelativePath,
    long Size,
    string Sha256);