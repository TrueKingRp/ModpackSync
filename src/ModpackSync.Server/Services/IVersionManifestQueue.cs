namespace ModpackSync.Server.Services;

public interface IVersionManifestQueue
{
    ValueTask QueueAsync(
        VersionManifestJob job,
        CancellationToken cancellationToken = default);

    ValueTask<VersionManifestJob> DequeueAsync(
        CancellationToken cancellationToken);
}