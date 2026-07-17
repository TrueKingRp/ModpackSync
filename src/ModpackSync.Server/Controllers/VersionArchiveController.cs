using Microsoft.AspNetCore.Mvc;
using ModpackSync.Server.Services;

namespace ModpackSync.Server.Controllers;

[ApiController]
[Route("api/versions/{versionId:guid}/archive")]
public sealed class VersionArchiveController :
    ControllerBase
{
    private readonly IVersionArchiveService _archiveService;
    private readonly ILogger<VersionArchiveController> _logger;

    public VersionArchiveController(
        IVersionArchiveService archiveService,
        ILogger<VersionArchiveController> logger)
    {
        _archiveService =
            archiveService;

        _logger =
            logger;
    }

    [HttpGet]
    [Produces(
        "application/zip")]
    [ProducesResponseType(
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadAsync(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        try
        {
            VersionArchiveResult? archive =
                await _archiveService.CreateAsync(
                    versionId,
                    cancellationToken);

            if (archive is null)
            {
                return NotFound(
                    new
                    {
                        code =
                            "version_not_found",

                        message =
                            $"No version exists with ID '{versionId}'."
                    });
            }

            HttpContext.Response.OnCompleted(
                async () =>
                {
                    await archive.DisposeAsync();
                });

            return File(
                archive.Stream,
                contentType: "application/zip",
                fileDownloadName: archive.FileName,
                enableRangeProcessing: true);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(
                ex,
                "Version {VersionId} could not be packaged because its data is invalid.",
                versionId);

            return Conflict(
                new
                {
                    code =
                        "invalid_version_archive",

                    message =
                        ex.Message
                });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(
                ex,
                "Version {VersionId} references a missing blob.",
                versionId);

            return Conflict(
                new
                {
                    code =
                        "missing_blob",

                    message =
                        ex.Message
                });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (IOException ex)
        {
            _logger.LogError(
                ex,
                "A storage error occurred while creating the archive for version {VersionId}.",
                versionId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "archive_storage_error",

                    message =
                        "The server could not create the version archive."
                });
        }
    }
}