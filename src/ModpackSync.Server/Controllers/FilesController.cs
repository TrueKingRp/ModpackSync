using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModpackSync.Contracts.Server.Files;
using ModpackSync.Server.Entities;
using ModpackSync.Server.Repositories;
using ModpackSync.Server.Storage;

namespace ModpackSync.Server.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    private const int MaximumHashesPerCheck =
        1000;

    private readonly IBlobStorageService _blobStorageService;
    private readonly IStoredFileRepository _storedFileRepository;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IBlobStorageService blobStorageService,
        IStoredFileRepository storedFileRepository,
        ILogger<FilesController> logger)
    {
        _blobStorageService =
            blobStorageService;

        _storedFileRepository =
            storedFileRepository;

        _logger =
            logger;
    }

    [HttpPut("{sha256}")]
    [Consumes("application/octet-stream")]
    [ProducesResponseType(
        typeof(StoredFileResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(StoredFileResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StoredFileResponse>>
        UploadAsync(
            string sha256,
            CancellationToken cancellationToken)
    {
        if (Request.ContentLength == 0)
        {
            return BadRequest(
                new
                {
                    code =
                        "empty_file",

                    message =
                        "The uploaded file cannot be empty."
                });
        }

        try
        {
            BlobWriteResult result =
                await _blobStorageService.StoreAsync(
                    Request.Body,
                    sha256,
                    cancellationToken);

            await RegisterStoredFileAsync(
                result,
                cancellationToken);

            string downloadUrl =
                Url.Action(
                    action: nameof(Download),
                    controller: "Files",
                    values: new
                    {
                        sha256 =
                            result.Sha256
                    },
                    protocol: Request.Scheme)
                ?? $"/api/files/{result.Sha256}";

            var response =
                new StoredFileResponse
                {
                    Sha256 =
                        result.Sha256,

                    Size =
                        result.Size,

                    AlreadyExisted =
                        result.AlreadyExisted,

                    DownloadUrl =
                        downloadUrl
                };

            if (result.AlreadyExisted)
            {
                return Ok(
                    response);
            }

            return Created(
                downloadUrl,
                response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    code =
                        "invalid_sha256",

                    message =
                        ex.Message
                });
        }
        catch (InvalidDataException ex)
            when (ex.Message.Contains(
                "maximum size",
                StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new
                {
                    code =
                        "file_too_large",

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
                        "hash_mismatch",

                    message =
                        ex.Message
                });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Upload for {Sha256} was cancelled.",
                sha256);

            return new EmptyResult();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "A database error occurred while registering {Sha256}.",
                sha256);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "database_error",

                    message =
                        "The file was stored, but its database record could not be created."
                });
        }
        catch (IOException ex)
        {
            _logger.LogError(
                ex,
                "A storage error occurred while uploading {Sha256}.",
                sha256);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "storage_error",

                    message =
                        "The file could not be written to server storage."
                });
        }
    }

    [HttpPost("check")]
    [ProducesResponseType(
        typeof(FileCheckResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FileCheckResponse>>
        CheckFilesAsync(
            [FromBody] FileCheckRequest request,
            CancellationToken cancellationToken)
    {
        if (request.Sha256Hashes is null)
        {
            return BadRequest(
                new
                {
                    code =
                        "hashes_required",

                    message =
                        "A list of SHA256 hashes is required."
                });
        }

        if (request.Sha256Hashes.Count >
            MaximumHashesPerCheck)
        {
            return BadRequest(
                new
                {
                    code =
                        "too_many_hashes",

                    message =
                        $"A maximum of {MaximumHashesPerCheck} hashes may be checked at once."
                });
        }

        var validHashes =
            new List<string>();

        foreach (string hash
                 in request.Sha256Hashes)
        {
            if (!TryNormaliseSha256(
                    hash,
                    out string? normalisedHash))
            {
                return BadRequest(
                    new
                    {
                        code =
                            "invalid_sha256",

                        message =
                            $"'{hash}' is not a valid SHA256 hash."
                    });
            }

            validHashes.Add(
                normalisedHash);
        }

        string[] distinctHashes =
            validHashes
                .Distinct(
                    StringComparer.Ordinal)
                .ToArray();

        HashSet<string> databaseHashes =
            await _storedFileRepository
                .GetExistingHashesAsync(
                    distinctHashes,
                    cancellationToken);

        var existingHashes =
            new List<string>();

        var missingHashes =
            new List<string>();

        foreach (string hash
                 in distinctHashes)
        {
            bool existsInDatabase =
                databaseHashes.Contains(
                    hash);

            bool existsInStorage =
                _blobStorageService.Exists(
                    hash);

            if (existsInDatabase &&
                existsInStorage)
            {
                existingHashes.Add(
                    hash);
            }
            else
            {
                missingHashes.Add(
                    hash);
            }
        }

        return Ok(
            new FileCheckResponse
            {
                ExistingHashes =
                    existingHashes,

                MissingHashes =
                    missingHashes
            });
    }

    [HttpGet("{sha256}", Name = nameof(Download))]
    [Produces("application/octet-stream")]
    [ProducesResponseType(
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        string sha256,
        CancellationToken cancellationToken)
    {
        try
        {
            Stream stream =
                await _blobStorageService.OpenReadAsync(
                    sha256,
                    cancellationToken);

            string normalisedSha256 =
                sha256.Trim()
                    .ToLowerInvariant();

            Response.Headers.ETag =
                $"\"{normalisedSha256}\"";

            return File(
                stream,
                "application/octet-stream",
                enableRangeProcessing: true);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    code =
                        "invalid_sha256",

                    message =
                        ex.Message
                });
        }
        catch (FileNotFoundException)
        {
            return NotFound(
                new
                {
                    code =
                        "file_not_found",

                    message =
                        $"No stored file exists with SHA256 '{sha256}'."
                });
        }
    }

    [HttpHead("{sha256}")]
    [ProducesResponseType(
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public IActionResult Exists(
        string sha256)
    {
        try
        {
            string blobPath =
                _blobStorageService.GetBlobPath(
                    sha256);

            if (!System.IO.File.Exists(
                    blobPath))
            {
                return NotFound();
            }

            var fileInfo =
                new FileInfo(
                    blobPath);

            Response.ContentLength =
                fileInfo.Length;

            Response.ContentType =
                "application/octet-stream";

            Response.Headers.ETag =
                $"\"{sha256.Trim().ToLowerInvariant()}\"";

            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    code =
                        "invalid_sha256",

                    message =
                        ex.Message
                });
        }
    }

    private async Task RegisterStoredFileAsync(
        BlobWriteResult result,
        CancellationToken cancellationToken)
    {
        StoredFile? existingRecord =
            await _storedFileRepository
                .GetBySha256Async(
                    result.Sha256,
                    cancellationToken);

        if (existingRecord is not null)
        {
            if (existingRecord.Size !=
                result.Size)
            {
                throw new InvalidDataException(
                    $"Stored file '{result.Sha256}' has a database size of " +
                    $"{existingRecord.Size} bytes, but the physical file is " +
                    $"{result.Size} bytes.");
            }

            return;
        }

        var storedFile =
            new StoredFile
            {
                Sha256 =
                    result.Sha256,

                Size =
                    result.Size,

                StoragePath =
                    result.StoragePath,

                CreatedAt =
                    DateTimeOffset.UtcNow
            };

        try
        {
            await _storedFileRepository.AddAsync(
                storedFile,
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            StoredFile? recordCreatedByAnotherRequest =
                await _storedFileRepository
                    .GetBySha256Async(
                        result.Sha256,
                        cancellationToken);

            if (recordCreatedByAnotherRequest is null)
            {
                throw;
            }
        }
    }

    private static bool TryNormaliseSha256(
        string? sha256,
        out string normalisedHash)
    {
        normalisedHash =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                sha256))
        {
            return false;
        }

        string candidate =
            sha256.Trim()
                .ToLowerInvariant();

        if (candidate.Length != 64)
        {
            return false;
        }

        foreach (char character
                 in candidate)
        {
            bool isNumber =
                character is >= '0' and <= '9';

            bool isHexLetter =
                character is >= 'a' and <= 'f';

            if (!isNumber &&
                !isHexLetter)
            {
                return false;
            }
        }

        normalisedHash =
            candidate;

        return true;
    }
}