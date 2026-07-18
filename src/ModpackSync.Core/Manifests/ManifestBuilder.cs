using System.Security.Cryptography;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Core.Packs;

namespace ModpackSync.Core.Manifests;

public sealed class ManifestBuilder
{
    public Task<ModpackManifest> BuildAsync(
        string packName,
        string rootDirectory,
        int version = 1,
        CancellationToken cancellationToken = default)
    {
        return BuildAsync(
            packName,
            rootDirectory,
            selection: null,
            version,
            cancellationToken);
    }

    public async Task<ModpackManifest> BuildAsync(
        string packName,
        string rootDirectory,
        PackContentSelection? selection,
        int version = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packName))
        {
            throw new ArgumentException(
                "The pack name cannot be empty.",
                nameof(packName));
        }

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException(
                "The root directory cannot be empty.",
                nameof(rootDirectory));
        }

        string fullRootPath =
            Path.GetFullPath(
                rootDirectory);

        if (!Directory.Exists(fullRootPath))
        {
            throw new DirectoryNotFoundException(
                $"The modpack directory does not exist: {fullRootPath}");
        }

        IEnumerable<string> selectedFiles =
            selection is null
                ? Directory.EnumerateFiles(
                    fullRootPath,
                    "*",
                    SearchOption.AllDirectories)
                : EnumerateSelectedFiles(
                    fullRootPath,
                    selection);

        var files =
            new List<ManifestFile>();

        foreach (string filePath
                 in selectedFiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            string relativePath =
                Path.GetRelativePath(
                        fullRootPath,
                        filePath)
                    .Replace(
                        '\\',
                        '/');

            if (relativePath.Equals(
                    "manifest.json",
                    StringComparison.OrdinalIgnoreCase) ||
                relativePath.Equals(
                    "OLDManifest.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FileInfo fileInfo =
                new(filePath);

            string hash =
                await CalculateSha256Async(
                    filePath,
                    cancellationToken);

            files.Add(
                new ManifestFile(
                    relativePath,
                    fileInfo.Length,
                    hash));
        }

        return new ModpackManifest
        {
            PackName =
                packName,

            Version =
                version,

            CreatedAt =
                DateTimeOffset.UtcNow,

            Files =
                files
                    .OrderBy(
                        file =>
                            file.RelativePath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList()
        };
    }

    private static IEnumerable<string>
        EnumerateSelectedFiles(
            string rootDirectory,
            PackContentSelection selection)
    {
        var returnedFiles =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (string includedPath
                 in selection.IncludedPaths)
        {
            if (string.IsNullOrWhiteSpace(
                    includedPath))
            {
                continue;
            }

            string fullPath =
                Path.GetFullPath(
                    Path.Combine(
                        rootDirectory,
                        includedPath));

            if (!IsInsideRoot(
                    rootDirectory,
                    fullPath))
            {
                continue;
            }

            if (File.Exists(fullPath))
            {
                string relativePath =
                    Path.GetRelativePath(
                        rootDirectory,
                        fullPath);

                if (!IsExcluded(
                        relativePath,
                        selection.ExcludedPaths) &&
                    returnedFiles.Add(fullPath))
                {
                    yield return fullPath;
                }

                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                continue;
            }

            foreach (string filePath
                     in Directory.EnumerateFiles(
                         fullPath,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relativePath =
                    Path.GetRelativePath(
                        rootDirectory,
                        filePath);

                if (IsExcluded(
                        relativePath,
                        selection.ExcludedPaths))
                {
                    continue;
                }

                if (returnedFiles.Add(
                        filePath))
                {
                    yield return filePath;
                }
            }
        }
    }

    private static bool IsExcluded(
        string relativePath,
        IEnumerable<string> excludedPaths)
    {
        string normalisedRelativePath =
            relativePath
                .Replace(
                    '\\',
                    '/')
                .Trim('/');

        foreach (string excludedPath
                 in excludedPaths)
        {
            if (string.IsNullOrWhiteSpace(
                    excludedPath))
            {
                continue;
            }

            string normalisedExcludedPath =
                excludedPath
                    .Replace(
                        '\\',
                        '/')
                    .Trim('/');

            if (normalisedRelativePath.Equals(
                    normalisedExcludedPath,
                    StringComparison.OrdinalIgnoreCase) ||
                normalisedRelativePath.StartsWith(
                    normalisedExcludedPath + "/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideRoot(
        string rootDirectory,
        string candidatePath)
    {
        string fullRootPath =
            Path.GetFullPath(
                    rootDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        string fullCandidatePath =
            Path.GetFullPath(
                candidatePath);

        return fullCandidatePath.StartsWith(
            fullRootPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string>
        CalculateSha256Async(
            string filePath,
            CancellationToken cancellationToken)
    {
        await using FileStream stream =
            new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                useAsync: true);

        byte[] hash =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }
}