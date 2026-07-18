using ModpackSync.Contracts.Server.Versions;

namespace ModpackSync.Server.Services;

public sealed record VersionManifestJob(
    Guid VersionId,
    ReplaceVersionFilesRequest Request);