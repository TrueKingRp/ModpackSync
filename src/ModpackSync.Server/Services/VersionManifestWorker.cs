using ModpackSync.Contracts.Server.Versions;

namespace ModpackSync.Server.Services;

public sealed class VersionManifestWorker :
    BackgroundService
{
    private readonly IVersionManifestQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VersionManifestWorker> _logger;

    public VersionManifestWorker(
        IVersionManifestQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<VersionManifestWorker> logger)
    {
        _queue =
            queue;

        _scopeFactory =
            scopeFactory;

        _logger =
            logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Version manifest worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            VersionManifestJob job;

            try
            {
                job =
                    await _queue.DequeueAsync(
                        stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using AsyncServiceScope scope =
                    _scopeFactory.CreateAsyncScope();

                IVersionFileService versionFileService =
                    scope.ServiceProvider
                        .GetRequiredService<IVersionFileService>();

                _logger.LogInformation(
                    "Processing manifest for version {VersionId}.",
                    job.VersionId);

                ReplaceVersionFilesResponse result =
                    await versionFileService.ReplaceAsync(
                        job.VersionId,
                        job.Request,
                        stoppingToken);

                _logger.LogInformation(
                    "Manifest completed for version {VersionId}. " +
                    "{FileCount} files, {TotalSize} bytes.",
                    result.VersionId,
                    result.FileCount,
                    result.TotalSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Manifest processing failed for version {VersionId}.",
                    job.VersionId);
            }
        }

        _logger.LogInformation(
            "Version manifest worker stopped.");
    }
}