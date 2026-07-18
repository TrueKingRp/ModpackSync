using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using ModpackSync.Contracts.Server.Versions;
using ModpackSync.Server.Services;

namespace ModpackSync.Server.Controllers;

[ApiController]
[Route("api/versions/{versionId:guid}/files")]
public sealed class VersionFilesController :
    ControllerBase
{
    private const int MaximumFilesPerVersion =
        20_000;

    private readonly IVersionManifestQueue _manifestQueue;
    private readonly ILogger<VersionFilesController> _logger;

    public VersionFilesController(
        IVersionManifestQueue manifestQueue,
        ILogger<VersionFilesController> logger)
    {
        _manifestQueue =
            manifestQueue;

        _logger =
            logger;
    }

    [HttpPut]
    [ProducesResponseType(
        StatusCodes.Status202Accepted)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ReplaceAsync(
        Guid versionId,
        [FromBody] ReplaceVersionFilesRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            request.Files is null)
        {
            return BadRequest(
                new
                {
                    code =
                        "file_list_required",

                    message =
                        "A file list is required."
                });
        }

        if (request.Files.Count >
            MaximumFilesPerVersion)
        {
            return BadRequest(
                new
                {
                    code =
                        "too_many_files",

                    message =
                        $"A version cannot contain more than " +
                        $"{MaximumFilesPerVersion} files."
                });
        }

        /*
         * Copy the request into new objects before putting it into
         * the background queue. This avoids retaining any objects
         * associated with the HTTP request.
         */
        var queuedRequest =
            new ReplaceVersionFilesRequest
            {
                Files =
                    request.Files
                        .Select(
                            file =>
                                new VersionFileItemRequest
                                {
                                    RelativePath =
                                        file.RelativePath,

                                    Sha256 =
                                        file.Sha256,

                                    Size =
                                        file.Size
                                })
                        .ToList()
            };

        try
        {
            await _manifestQueue.QueueAsync(
                new VersionManifestJob(
                    versionId,
                    queuedRequest),
                cancellationToken);
        }
        catch (ChannelClosedException ex)
        {
            _logger.LogError(
                ex,
                "The manifest queue is unavailable.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    code =
                        "manifest_queue_unavailable",

                    message =
                        "The manifest could not be queued."
                });
        }

        return Accepted(
            new
            {
                versionId,
                status =
                    "processing",

                fileCount =
                    queuedRequest.Files.Count
            });
    }
}