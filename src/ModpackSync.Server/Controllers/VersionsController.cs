using Microsoft.AspNetCore.Mvc;
using ModpackSync.Contracts.Server.Versions;
using ModpackSync.Server.Services;

namespace ModpackSync.Server.Controllers;

[ApiController]
public sealed class VersionsController : ControllerBase
{
    private readonly IVersionService _versionService;

    public VersionsController(
        IVersionService versionService)
    {
        _versionService = versionService;
    }

    [HttpGet("api/packs/{packId:guid}/versions")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ServerVersionResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ServerVersionResponse>>>
        GetByPackIdAsync(
            Guid packId,
            CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<ServerVersionResponse> versions =
                await _versionService.GetByPackIdAsync(
                    packId,
                    cancellationToken);

            return Ok(versions);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(
                new
                {
                    message = ex.Message
                });
        }
    }

    [HttpGet("api/versions/{versionId:guid}")]
    [ProducesResponseType(
        typeof(ServerVersionResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServerVersionResponse>>
        GetByIdAsync(
            Guid versionId,
            CancellationToken cancellationToken)
    {
        ServerVersionResponse? version =
            await _versionService.GetByIdAsync(
                versionId,
                cancellationToken);

        if (version is null)
        {
            return NotFound(
                new
                {
                    message =
                        $"No version exists with ID '{versionId}'."
                });
        }

        return Ok(version);
    }

    [HttpPost("api/packs/{packId:guid}/versions")]
    [ProducesResponseType(
        typeof(ServerVersionResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServerVersionResponse>>
        CreateAsync(
            Guid packId,
            [FromBody] CreateServerVersionRequest request,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                request.VersionLabel))
        {
            return BadRequest(
                new
                {
                    message =
                        "The version label cannot be empty."
                });
        }

        try
        {
            ServerVersionResponse version =
                await _versionService.CreateAsync(
                    packId,
                    request,
                    cancellationToken);

            return Ok(version);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(
                new
                {
                    message = ex.Message
                });
        }
    }
}