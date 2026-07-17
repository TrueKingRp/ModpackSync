using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModpackSync.Contracts.Server.Versions;
using ModpackSync.Server.Services;

namespace ModpackSync.Server.Controllers;

[ApiController]
[Route("api/versions/{versionId:guid}/files")]
public sealed class VersionFilesController :
    ControllerBase
{
    private readonly IVersionFileService _versionFileService;
    private readonly ILogger<VersionFilesController> _logger;

    public VersionFilesController(
        IVersionFileService versionFileService,
        ILogger<VersionFilesController> logger)
    {
        _versionFileService =
            versionFileService;

        _logger =
            logger;
    }

    [HttpPut]
    [ProducesResponseType(
        typeof(ReplaceVersionFilesResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReplaceVersionFilesResponse>>
        ReplaceAsync(
            Guid versionId,
            [FromBody] ReplaceVersionFilesRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            ReplaceVersionFilesResponse response =
                await _versionFileService.ReplaceAsync(
                    versionId,
                    request,
                    cancellationToken);

            return Ok(
                response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(
                new
                {
                    code =
                        "version_not_found",

                    message =
                        ex.Message
                });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(
                new
                {
                    code =
                        "version_already_published",

                    message =
                        ex.Message
                });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    code =
                        "invalid_file_list",

                    message =
                        ex.Message
                });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(
                new
                {
                    code =
                        "file_validation_failed",

                    message =
                        ex.Message
                });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "A database error occurred while replacing files for version {VersionId}.",
                versionId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "database_error",

                    message =
                        "The version file list could not be saved."
                });
        }
    }
}