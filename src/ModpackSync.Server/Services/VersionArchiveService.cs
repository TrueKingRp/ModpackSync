using System.IO.Compression;
using ModpackSync.Server.Entities;
using ModpackSync.Server.Repositories;
using ModpackSync.Server.Storage;

namespace ModpackSync.Server.Services;

public sealed class VersionArchiveService :
    IVersionArchiveService
{
    private readonly IVersionRepository _versionRepository;
    private readonly IPackRepository _packRepository;
    private readonly IBlobStorageService _blobStorageService;

    public VersionArchiveService(
        IVersionRepository versionRepository,
        IPackRepository packRepository,
        IBlobStorageService blobStorageService)
    {
        _versionRepository =
            versionRepository;

        _packRepository =
            packRepository;

        _blobStorageService =
            blobStorageService;
    }

    public async Task<VersionArchiveResult?> CreateAsync(
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

        ServerPack? pack =
            await _packRepository.GetByIdAsync(
                version.PackId,
                cancellationToken);

        if (pack is null)
        {
            throw new InvalidDataException(
                $"Version '{version.Id}' references pack " +
                $"'{version.PackId}', but that pack does not exist.");
        }

        ValidateVersionFiles(
            version);

        string temporaryArchivePath =
            Path.Combine(
                Path.GetTempPath(),
                $"modpacksync-{Guid.NewGuid():N}.zip");

        try
        {
            await CreateArchiveAsync(
                version,
                temporaryArchivePath,
                cancellationToken);

            FileStream archiveStream =
                new(
                    temporaryArchivePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 128,
                    options:
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan);

            string archiveFileName =
                CreateArchiveFileName(
                    pack.Name,
                    version.VersionLabel);

            return new VersionArchiveResult
            {
                Stream =
                    archiveStream,

                FileName =
                    archiveFileName,

                TemporaryFilePath =
                    temporaryArchivePath
            };
        }
        catch
        {
            TryDeleteFile(
                temporaryArchivePath);

            throw;
        }
    }

    private async Task CreateArchiveAsync(
        ServerPackVersion version,
        string archivePath,
        CancellationToken cancellationToken)
    {
        await using FileStream archiveFileStream =
            new(
                archivePath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1024 * 128,
                options:
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

        using ZipArchive archive =
            new(
                archiveFileStream,
                ZipArchiveMode.Create,
                leaveOpen: true);

        foreach (VersionFile versionFile in
                 version.Files.OrderBy(
                     file => file.RelativePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string archiveEntryPath =
                NormaliseAndValidateRelativePath(
                    versionFile.RelativePath);

            StoredFile storedFile =
                versionFile.StoredFile
                ?? throw new InvalidDataException(
                    $"Version file '{versionFile.RelativePath}' " +
                    $"references blob '{versionFile.Sha256}', but no " +
                    "matching stored-file record exists.");

            if (storedFile.Size !=
                versionFile.Size)
            {
                throw new InvalidDataException(
                    $"Version file '{versionFile.RelativePath}' declares " +
                    $"{versionFile.Size} bytes, but stored blob " +
                    $"'{versionFile.Sha256}' contains " +
                    $"{storedFile.Size} bytes.");
            }

            if (!_blobStorageService.Exists(
                    versionFile.Sha256))
            {
                throw new FileNotFoundException(
                    $"The physical blob '{versionFile.Sha256}' required " +
                    $"by '{versionFile.RelativePath}' is missing.");
            }

            string physicalBlobPath =
                ResolveStoredFilePath(
                    storedFile);

            if (!File.Exists(
                    physicalBlobPath))
            {
                throw new FileNotFoundException(
                    $"The stored blob file for '{versionFile.RelativePath}' " +
                    $"does not exist at '{physicalBlobPath}'.",
                    physicalBlobPath);
            }

            FileInfo blobInformation =
                new(
                    physicalBlobPath);

            if (blobInformation.Length !=
                versionFile.Size)
            {
                throw new InvalidDataException(
                    $"Blob '{versionFile.Sha256}' has a physical size of " +
                    $"{blobInformation.Length} bytes, but the version " +
                    $"expects {versionFile.Size} bytes.");
            }

            ZipArchiveEntry archiveEntry =
                archive.CreateEntry(
                    archiveEntryPath,
                    CompressionLevel.Fastest);

            await using Stream archiveEntryStream =
                archiveEntry.Open();

            await using FileStream blobStream =
                new(
                    physicalBlobPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 128,
                    options:
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan);

            await blobStream.CopyToAsync(
                archiveEntryStream,
                bufferSize: 1024 * 128,
                cancellationToken);
        }
    }

    private void ValidateVersionFiles(
        ServerPackVersion version)
    {
        if (version.Files.Count == 0)
        {
            throw new InvalidDataException(
                $"Version '{version.Id}' does not contain any files.");
        }

        HashSet<string> archivePaths =
            new(
                StringComparer.OrdinalIgnoreCase);

        foreach (VersionFile file in
                 version.Files)
        {
            string normalisedPath =
                NormaliseAndValidateRelativePath(
                    file.RelativePath);

            if (!archivePaths.Add(
                    normalisedPath))
            {
                throw new InvalidDataException(
                    $"The version contains more than one file using the " +
                    $"archive path '{normalisedPath}'.");
            }

            if (string.IsNullOrWhiteSpace(
                    file.Sha256))
            {
                throw new InvalidDataException(
                    $"Version file '{file.RelativePath}' has no SHA256 hash.");
            }

            if (file.Size < 0)
            {
                throw new InvalidDataException(
                    $"Version file '{file.RelativePath}' has an invalid size.");
            }
        }
    }

    private string ResolveStoredFilePath(
        StoredFile storedFile)
    {
        if (string.IsNullOrWhiteSpace(
                storedFile.StoragePath))
        {
            throw new InvalidDataException(
                $"Stored blob '{storedFile.Sha256}' has no storage path.");
        }

        if (Path.IsPathRooted(
                storedFile.StoragePath))
        {
            return Path.GetFullPath(
                storedFile.StoragePath);
        }

        return Path.GetFullPath(
            Path.Combine(
                _blobStorageService.RootDirectory,
                storedFile.StoragePath));
    }

    private static string NormaliseAndValidateRelativePath(
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(
                relativePath))
        {
            throw new InvalidDataException(
                "An archive entry has an empty relative path.");
        }

        string normalisedPath =
            relativePath
                .Trim()
                .Replace(
                    '\\',
                    '/');

        if (normalisedPath.StartsWith(
                "/",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Archive path '{relativePath}' cannot be absolute.");
        }

        if (normalisedPath.Contains(
                ":",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Archive path '{relativePath}' contains an invalid colon.");
        }

        string[] segments =
            normalisedPath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            throw new InvalidDataException(
                $"Archive path '{relativePath}' does not identify a file.");
        }

        foreach (string segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new InvalidDataException(
                    $"Archive path '{relativePath}' contains unsafe " +
                    "directory traversal.");
            }

            if (segment.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException(
                    $"Archive path '{relativePath}' contains invalid " +
                    $"characters in segment '{segment}'.");
            }
        }

        return string.Join(
            '/',
            segments);
    }

    private static string CreateArchiveFileName(
        string packName,
        string versionLabel)
    {
        string safePackName =
            SanitiseFileNamePart(
                packName,
                "Modpack");

        string safeVersionLabel =
            SanitiseFileNamePart(
                versionLabel,
                "Version");

        return $"{safePackName}-{safeVersionLabel}.zip";
    }

    private static string SanitiseFileNamePart(
        string value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return fallback;
        }

        char[] invalidCharacters =
            Path.GetInvalidFileNameChars();

        string safeValue =
            new(
                value
                    .Trim()
                    .Select(
                        character =>
                            invalidCharacters.Contains(
                                character)
                                ? '_'
                                : character)
                    .ToArray());

        safeValue =
            safeValue.Trim(
                '.',
                ' ');

        return string.IsNullOrWhiteSpace(
                safeValue)
            ? fallback
            : safeValue;
    }

    private static void TryDeleteFile(
        string filePath)
    {
        try
        {
            if (File.Exists(
                    filePath))
            {
                File.Delete(
                    filePath);
            }
        }
        catch
        {
            // Ignore temporary-file cleanup failures.
        }
    }
}