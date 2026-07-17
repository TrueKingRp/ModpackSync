using Microsoft.AspNetCore.Mvc;
using ModpackSync.Contracts.Server.Versions;
using ModpackSync.Server.Services;

namespace ModpackSync.Server.Controllers;

[ApiController]
[Route("api/versions/{versionId:guid}/manifest")]
public sealed class VersionManifestController :
    ControllerBase
{
    private readonly IVersionManifestService _manifestService;
    private readonly ILogger<VersionManifestController> _logger;

    public VersionManifestController(
        IVersionManifestService manifestService,
        ILogger<VersionManifestController> logger)
    {
        _manifestService =
            manifestService;

        _logger =
            logger;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(VersionManifestResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<VersionManifestResponse>>
        GetAsync(
            Guid versionId,
            CancellationToken cancellationToken)
    {
        try
        {
            VersionManifestResponse? manifest =
                await _manifestService.GetAsync(
                    versionId,
                    cancellationToken);

            if (manifest is null)
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

            return Ok(
                manifest);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(
                ex,
                "Version {VersionId} contains an invalid manifest.",
                versionId);

            return Conflict(
                new
                {
                    code =
                        "invalid_manifest",

                    message =
                        ex.Message
                });
        }
        catch (IOException ex)
        {
            _logger.LogError(
                ex,
                "A storage error occurred while reading the manifest for version {VersionId}.",
                versionId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "storage_error",

                    message =
                        "The version manifest could not be read from server storage."
                });
        }
    }
}