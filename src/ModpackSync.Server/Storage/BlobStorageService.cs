using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ModpackSync.Server.Storage;

public sealed class BlobStorageService :
    IBlobStorageService
{
    private const int BufferSize =
        1024 * 128;

    private readonly long _maximumFileSizeBytes;

    public string RootDirectory { get; }

    public BlobStorageService(
        IOptions<BlobStorageOptions> options,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        BlobStorageOptions settings =
            options.Value;

        if (string.IsNullOrWhiteSpace(
                settings.RootPath))
        {
            throw new InvalidOperationException(
                "BlobStorage:RootPath cannot be empty.");
        }

        if (settings.MaximumFileSizeBytes <= 0)
        {
            throw new InvalidOperationException(
                "BlobStorage:MaximumFileSizeBytes must be greater than zero.");
        }

        RootDirectory =
            Path.IsPathRooted(settings.RootPath)
                ? Path.GetFullPath(
                    settings.RootPath)
                : Path.GetFullPath(
                    Path.Combine(
                        environment.ContentRootPath,
                        settings.RootPath));

        _maximumFileSizeBytes =
            settings.MaximumFileSizeBytes;

        Directory.CreateDirectory(
            RootDirectory);
    }

    public bool Exists(
        string sha256)
    {
        string blobPath =
            GetBlobPath(sha256);

        return File.Exists(
            blobPath);
    }

    public string GetBlobPath(
        string sha256)
    {
        string normalisedHash =
            NormaliseSha256(sha256);

        string hashDirectory =
            Path.Combine(
                RootDirectory,
                "blobs",
                normalisedHash[..2]);

        return Path.Combine(
            hashDirectory,
            normalisedHash);
    }

    public async Task<BlobWriteResult> StoreAsync(
        Stream source,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
        {
            throw new ArgumentException(
                "The source stream must be readable.",
                nameof(source));
        }

        string normalisedExpectedHash =
            NormaliseSha256(
                expectedSha256);

        string finalPath =
            GetBlobPath(
                normalisedExpectedHash);

        if (File.Exists(finalPath))
        {
            var existingFile =
                new FileInfo(finalPath);

            return new BlobWriteResult
            {
                Sha256 =
                    normalisedExpectedHash,

                StoragePath =
                    finalPath,

                Size =
                    existingFile.Length,

                AlreadyExisted =
                    true
            };
        }

        string? finalDirectory =
            Path.GetDirectoryName(
                finalPath);

        if (string.IsNullOrWhiteSpace(
                finalDirectory))
        {
            throw new InvalidOperationException(
                "The blob storage directory could not be determined.");
        }

        Directory.CreateDirectory(
            finalDirectory);

        string temporaryPath =
            finalPath +
            "." +
            Guid.NewGuid().ToString("N") +
            ".tmp";

        byte[] buffer =
            ArrayPool<byte>.Shared.Rent(
                BufferSize);

        long totalBytes =
            0;

        try
        {
            using IncrementalHash hasher =
                IncrementalHash.CreateHash(
                    HashAlgorithmName.SHA256);

            await using (
                var destination =
                    new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        BufferSize,
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan))
            {
                while (true)
                {
                    int bytesRead =
                        await source.ReadAsync(
                            buffer.AsMemory(
                                0,
                                buffer.Length),
                            cancellationToken);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytes +=
                        bytesRead;

                    if (totalBytes >
                        _maximumFileSizeBytes)
                    {
                        throw new InvalidDataException(
                            $"The uploaded file exceeds the maximum size of " +
                            $"{_maximumFileSizeBytes} bytes.");
                    }

                    hasher.AppendData(
                        buffer,
                        0,
                        bytesRead);

                    await destination.WriteAsync(
                        buffer.AsMemory(
                            0,
                            bytesRead),
                        cancellationToken);
                }

                await destination.FlushAsync(
                    cancellationToken);
            }

            string calculatedHash =
                Convert.ToHexString(
                        hasher.GetHashAndReset())
                    .ToLowerInvariant();

            if (!calculatedHash.Equals(
                    normalisedExpectedHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The uploaded file's SHA256 hash does not match " +
                    $"the expected hash. Expected '{normalisedExpectedHash}', " +
                    $"but calculated '{calculatedHash}'.");
            }

            if (File.Exists(finalPath))
            {
                File.Delete(
                    temporaryPath);

                var existingFile =
                    new FileInfo(finalPath);

                return new BlobWriteResult
                {
                    Sha256 =
                        normalisedExpectedHash,

                    StoragePath =
                        finalPath,

                    Size =
                        existingFile.Length,

                    AlreadyExisted =
                        true
                };
            }

            File.Move(
                temporaryPath,
                finalPath);

            return new BlobWriteResult
            {
                Sha256 =
                    normalisedExpectedHash,

                StoragePath =
                    finalPath,

                Size =
                    totalBytes,

                AlreadyExisted =
                    false
            };
        }
        catch
        {
            if (File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }

            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(
                buffer);
        }
    }

    public Task<Stream> OpenReadAsync(
        string sha256,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string blobPath =
            GetBlobPath(
                sha256);

        if (!File.Exists(blobPath))
        {
            throw new FileNotFoundException(
                $"No stored file exists with SHA256 '{sha256}'.",
                blobPath);
        }

        Stream stream =
            new FileStream(
                blobPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        return Task.FromResult(
            stream);
    }

    public Task<bool> DeleteAsync(
        string sha256,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string blobPath =
            GetBlobPath(
                sha256);

        if (!File.Exists(blobPath))
        {
            return Task.FromResult(
                false);
        }

        File.Delete(
            blobPath);

        RemoveEmptyParentDirectory(
            blobPath);

        return Task.FromResult(
            true);
    }

    private void RemoveEmptyParentDirectory(
        string blobPath)
    {
        string? directory =
            Path.GetDirectoryName(
                blobPath);

        if (string.IsNullOrWhiteSpace(directory) ||
            !Directory.Exists(directory))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(
                directory)
            .Any())
        {
            return;
        }

        Directory.Delete(
            directory);
    }

    private static string NormaliseSha256(
        string sha256)
    {
        if (string.IsNullOrWhiteSpace(
                sha256))
        {
            throw new ArgumentException(
                "The SHA256 hash cannot be empty.",
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

            bool isLowercaseHex =
                character is >= 'a' and <= 'f';

            if (!isNumber &&
                !isLowercaseHex)
            {
                throw new ArgumentException(
                    "The SHA256 hash contains invalid characters.",
                    nameof(sha256));
            }
        }

        return normalisedHash;
    }
}