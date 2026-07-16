using Microsoft.AspNetCore.Mvc;
using ModpackSync.Contracts.Server.Packs;
using ModpackSync.Server.Services;

namespace ModpackSync.Server.Controllers;

[ApiController]
[Route("api/packs")]
public sealed class PacksController : ControllerBase
{
    private readonly IPackService _packService;

    public PacksController(
        IPackService packService)
    {
        _packService = packService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<ServerPackResponse>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServerPackResponse>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ServerPackResponse> packs =
            await _packService.GetAllAsync(
                cancellationToken);

        return Ok(packs);
    }

    [HttpGet("{packId:guid}")]
    [ProducesResponseType(
        typeof(ServerPackResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServerPackResponse>> GetByIdAsync(
        Guid packId,
        CancellationToken cancellationToken)
    {
        ServerPackResponse? pack =
            await _packService.GetByIdAsync(
                packId,
                cancellationToken);

        if (pack is null)
        {
            return NotFound(
                new
                {
                    message =
                        $"No pack exists with ID '{packId}'."
                });
        }

        return Ok(pack);
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(ServerPackResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServerPackResponse>> CreateAsync(
        [FromBody] CreatePackRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(
                new
                {
                    message =
                        "The pack name cannot be empty."
                });
        }

        ServerPackResponse pack =
            await _packService.CreateOrGetAsync(
                request,
                cancellationToken);

        return Ok(pack);
    }
}