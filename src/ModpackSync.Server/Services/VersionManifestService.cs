using ModpackSync.Contracts.Server.Versions;
using ModpackSync.Server.Entities;
using ModpackSync.Server.Repositories;
using ModpackSync.Server.Storage;

namespace ModpackSync.Server.Services;

public sealed class VersionManifestService :
    IVersionManifestService
{
    private readonly IVersionRepository _versionRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public VersionManifestService(
        IVersionRepository versionRepository,
        IBlobStorageService blobStorageService,
        IHttpContextAccessor httpContextAccessor)
    {
        _versionRepository =
            versionRepository;

        _blobStorageService =
            blobStorageService;

        _httpContextAccessor =
            httpContextAccessor;
    }

    public async Task<VersionManifestResponse?> GetAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        ServerPackVersion? version =
            await _versionRepository.GetByIdAsync(
                versionId,
                cancellationToken);

        if (version is null)
        {
            return null;
        }

        HttpContext? httpContext =
            _httpContextAccessor.HttpContext;

        string baseUrl =
            httpContext is null
                ? string.Empty
                : $"{httpContext.Request.Scheme}://" +
                  $"{httpContext.Request.Host}" +
                  $"{httpContext.Request.PathBase}";

        List<ManifestFileResponse> files =
            version.Files
                .OrderBy(
                    file =>
                        file.RelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    file =>
                        MapFile(
                            file,
                            baseUrl))
                .ToList();

        long totalSize =
            files.Sum(
                file =>
                    file.Size);

        return new VersionManifestResponse
        {
            VersionId =
                version.Id,

            PackId =
                version.PackId,

            VersionLabel =
                version.VersionLabel,

            ReleaseNotes =
                version.ReleaseNotes,

            CreatedAt =
                version.CreatedAt,

            IsComplete =
                version.IsComplete,

            IsPublished =
                version.IsPublished,

            FileCount =
                files.Count,

            TotalSize =
                totalSize,

            Files =
                files
        };
    }

    private ManifestFileResponse MapFile(
        VersionFile file,
        string baseUrl)
    {
        if (file.StoredFile is null)
        {
            throw new InvalidDataException(
                $"Version file '{file.RelativePath}' references blob " +
                $"'{file.Sha256}', but no matching stored-file record exists.");
        }

        if (file.StoredFile.Size !=
            file.Size)
        {
            throw new InvalidDataException(
                $"Version file '{file.RelativePath}' declares a size of " +
                $"{file.Size} bytes, but stored blob '{file.Sha256}' has a " +
                $"size of {file.StoredFile.Size} bytes.");
        }

        if (!_blobStorageService.Exists(
                file.Sha256))
        {
            throw new InvalidDataException(
                $"Version file '{file.RelativePath}' references blob " +
                $"'{file.Sha256}', but the physical blob is missing.");
        }

        string downloadPath =
            $"/api/files/{file.Sha256}";

        string downloadUrl =
            string.IsNullOrWhiteSpace(baseUrl)
                ? downloadPath
                : $"{baseUrl.TrimEnd('/')}{downloadPath}";

        return new ManifestFileResponse
        {
            RelativePath =
                file.RelativePath,

            Sha256 =
                file.Sha256,

            Size =
                file.Size,

            DownloadUrl =
                downloadUrl
        };
    }
}