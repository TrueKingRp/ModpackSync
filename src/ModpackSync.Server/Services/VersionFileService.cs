using System;
using ModpackSync.Contracts.Server.Versions;
using ModpackSync.Server.Entities;
using ModpackSync.Server.Repositories;
using ModpackSync.Server.Storage;

namespace ModpackSync.Server.Services;

public sealed class VersionFileService :
    IVersionFileService
{
    private const int MaximumFilesPerVersion =
        20_000;

    private const int MaximumRelativePathLength =
        1000;

    private readonly IVersionRepository _versionRepository;
    private readonly IStoredFileRepository _storedFileRepository;
    private readonly IVersionFileRepository _versionFileRepository;
    private readonly IBlobStorageService _blobStorageService;

    public VersionFileService(
        IVersionRepository versionRepository,
        IStoredFileRepository storedFileRepository,
        IVersionFileRepository versionFileRepository,
        IBlobStorageService blobStorageService)
    {
        _versionRepository =
            versionRepository;

        _storedFileRepository =
            storedFileRepository;

        _versionFileRepository =
            versionFileRepository;

        _blobStorageService =
            blobStorageService;
    }

    public async Task<ReplaceVersionFilesResponse> ReplaceAsync(
        Guid versionId,
        ReplaceVersionFilesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.Files is null)
        {
            throw new ArgumentException(
                "A file list is required.",
                nameof(request));
        }

        if (request.Files.Count >
            MaximumFilesPerVersion)
        {
            throw new ArgumentException(
                $"A version cannot contain more than " +
                $"{MaximumFilesPerVersion} files.",
                nameof(request));
        }

        ServerPackVersion? version =
            await _versionRepository.GetByIdAsync(
                versionId,
                cancellationToken);

        if (version is null)
        {
            throw new KeyNotFoundException(
                $"No version exists with ID '{versionId}'.");
        }

        if (version.IsPublished)
        {
            throw new InvalidOperationException(
                "Published versions cannot be modified.");
        }

        var preparedFiles =
            new List<PreparedVersionFile>(
                request.Files.Count);

        var paths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (VersionFileItemRequest item
                 in request.Files)
        {
            if (item is null)
            {
                throw new ArgumentException(
                    "The file list contains an empty entry.",
                    nameof(request));
            }

            string relativePath =
                NormaliseRelativePath(
                    item.RelativePath);

            if (!paths.Add(relativePath))
            {
                throw new ArgumentException(
                    $"The relative path '{relativePath}' appears more than once.",
                    nameof(request));
            }

            string sha256 =
                NormaliseSha256(
                    item.Sha256);

            if (item.Size < 0)
            {
                throw new ArgumentException(
                    $"File '{relativePath}' has an invalid negative size.",
                    nameof(request));
            }

            preparedFiles.Add(
                new PreparedVersionFile(
                    relativePath,
                    sha256,
                    item.Size));
        }

        string[] hashes =
            preparedFiles
                .Select(
                    file =>
                        file.Sha256)
                .Distinct(
                    StringComparer.Ordinal)
                .ToArray();

        IReadOnlyDictionary<string, StoredFile> storedFiles =
            await _storedFileRepository.GetByHashesAsync(
                hashes,
                cancellationToken);

        foreach (PreparedVersionFile file
                 in preparedFiles)
        {
            if (!storedFiles.TryGetValue(
                    file.Sha256,
                    out StoredFile? storedFile))
            {
                throw new InvalidDataException(
                    $"The blob '{file.Sha256}' has not been uploaded.");
            }

            if (!_blobStorageService.Exists(
                    file.Sha256))
            {
                throw new InvalidDataException(
                    $"The blob '{file.Sha256}' is registered in the database " +
                    "but is missing from physical storage.");
            }

            if (storedFile.Size !=
                file.Size)
            {
                throw new InvalidDataException(
                    $"File '{file.RelativePath}' declares a size of " +
                    $"{file.Size} bytes, but blob '{file.Sha256}' is " +
                    $"{storedFile.Size} bytes.");
            }
        }

        List<VersionFile> versionFiles =
            preparedFiles
                .Select(
                    file =>
                        new VersionFile
                        {
                            VersionId =
                                versionId,

                            RelativePath =
                                file.RelativePath,

                            Sha256 =
                                file.Sha256,

                            Size =
                                file.Size
                        })
                .ToList();

        await _versionFileRepository.ReplaceAsync(
            versionId,
            versionFiles,
            cancellationToken);

        long totalSize =
            versionFiles.Sum(
                file =>
                    file.Size);

        return new ReplaceVersionFilesResponse
        {
            VersionId =
                versionId,

            FileCount =
                versionFiles.Count,

            TotalSize =
                totalSize
        };
    }

    private static string NormaliseRelativePath(
        string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(
                relativePath))
        {
            throw new ArgumentException(
                "A relative path cannot be empty.",
                nameof(relativePath));
        }

        string normalisedPath =
            relativePath.Trim()
                .Replace('\\', '/');

        if (normalisedPath.Length >
            MaximumRelativePathLength)
        {
            throw new ArgumentException(
                $"The relative path cannot exceed " +
                $"{MaximumRelativePathLength} characters.",
                nameof(relativePath));
        }

        if (normalisedPath.StartsWith(
        "/",
        StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The path '{relativePath}' must be relative.",
                nameof(relativePath));
        }

        if (normalisedPath.Contains(
                ":",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The path '{relativePath}' contains an invalid colon.",
                nameof(relativePath));
        }

        if (normalisedPath.Contains(
                '\0'))
        {
            throw new ArgumentException(
                $"The path '{relativePath}' contains an invalid null character.",
                nameof(relativePath));
        }

        string[] segments =
            normalisedPath.Split(
                '/');

        foreach (string segment
                 in segments)
        {
            if (string.IsNullOrWhiteSpace(
                    segment))
            {
                throw new ArgumentException(
                    $"The path '{relativePath}' contains an empty directory name.",
                    nameof(relativePath));
            }

            if (segment is "." or "..")
            {
                throw new ArgumentException(
                    $"The path '{relativePath}' contains an unsafe path segment.",
                    nameof(relativePath));
            }
        }

        return string.Join(
            '/',
            segments);
    }

    private static string NormaliseSha256(
        string? sha256)
    {
        if (string.IsNullOrWhiteSpace(
                sha256))
        {
            throw new ArgumentException(
                "A SHA256 hash cannot be empty.",
                nameof(sha256));
        }

        string normalisedHash =
            sha256.Trim()
                .ToLowerInvariant();

        if (normalisedHash.Length != 64)
        {
            throw new ArgumentException(
                "A SHA256 hash must contain exactly 64 hexadecimal characters.",
                nameof(sha256));
        }

        foreach (char character
                 in normalisedHash)
        {
            bool isNumber =
                character is >= '0' and <= '9';

            bool isHexLetter =
                character is >= 'a' and <= 'f';

            if (!isNumber &&
                !isHexLetter)
            {
                throw new ArgumentException(
                    $"'{sha256}' is not a valid SHA256 hash.",
                    nameof(sha256));
            }
        }

        return normalisedHash;
    }

    private sealed record PreparedVersionFile(
        string RelativePath,
        string Sha256,
        long Size);
}