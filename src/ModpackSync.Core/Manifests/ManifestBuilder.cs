using System.Security.Cryptography;
using ModpackSync.Contracts.Manifests;

namespace ModpackSync.Core.Manifests;

public sealed class ManifestBuilder
{
    public async Task<ModpackManifest> BuildAsync(
        string packName,
        string rootDirectory,
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

        string fullRootPath = Path.GetFullPath(rootDirectory);

        if (!Directory.Exists(fullRootPath))
        {
            throw new DirectoryNotFoundException(
                $"The modpack directory does not exist: {fullRootPath}");
        }

        var files = new List<ManifestFile>();

        foreach (string filePath in Directory.EnumerateFiles(
                     fullRootPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(
                    fullRootPath,
                    filePath)
                .Replace('\\', '/');

            if (relativePath.Equals(
        "manifest.json",
        StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FileInfo fileInfo = new(filePath);

            string hash = await CalculateSha256Async(
                filePath,
                cancellationToken);

            files.Add(new ManifestFile(
                relativePath,
                fileInfo.Length,
                hash));
        }

        return new ModpackManifest
        {
            PackName = packName,
            Version = version,
            CreatedAt = DateTimeOffset.UtcNow,
            Files = files
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static async Task<string> CalculateSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);

        byte[] hash = await SHA256.HashDataAsync(
            stream,
            cancellationToken);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}