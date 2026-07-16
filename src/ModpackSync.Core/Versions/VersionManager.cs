using System.Text.Json;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Versions;

namespace ModpackSync.Core.Versions;

public sealed class VersionManager
{
    private readonly string _versionsDirectory;
    private readonly string _registryPath;
    private readonly JsonSerializerOptions _jsonOptions;

    private VersionManagerSettings _settings = new();

    public VersionManager(
        string? versionsDirectory = null)
    {
        _versionsDirectory =
            versionsDirectory ??
            GetDefaultVersionsDirectory();

        _registryPath = Path.Combine(
            _versionsDirectory,
            "versions.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public string VersionsDirectory =>
        _versionsDirectory;

    public async Task InitialiseAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(
            _versionsDirectory);

        if (!File.Exists(_registryPath))
        {
            _settings =
                new VersionManagerSettings();

            await SaveAsync(cancellationToken);
            return;
        }

        string json =
            await File.ReadAllTextAsync(
                _registryPath,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            _settings =
                new VersionManagerSettings();

            return;
        }

        _settings =
            JsonSerializer.Deserialize<VersionManagerSettings>(
                json,
                _jsonOptions)
            ?? new VersionManagerSettings();
    }

    public IReadOnlyList<PackVersion> GetVersions(
        Guid packId)
    {
        return _settings.Versions
            .Where(version =>
                version.PackId == packId)
            .OrderByDescending(version =>
                version.CreatedAt)
            .ToList();
    }

    public PackVersion? GetVersion(
        Guid versionId)
    {
        return _settings.Versions
            .FirstOrDefault(version =>
                version.Id == versionId);
    }

    public PackVersion? GetPublishedVersion(
        Guid packId)
    {
        return _settings.Versions
            .Where(version =>
                version.PackId == packId &&
                version.IsPublished)
            .OrderByDescending(version =>
                version.CreatedAt)
            .FirstOrDefault();
    }

    public async Task<PackVersion> CreateVersionAsync(
        ModpackRegistration pack,
        string versionLabel,
        string? releaseNotes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);

        if (string.IsNullOrWhiteSpace(
                versionLabel))
        {
            throw new ArgumentException(
                "The version label cannot be empty.",
                nameof(versionLabel));
        }

        string trimmedVersionLabel =
            versionLabel.Trim();

        bool duplicateVersion =
            _settings.Versions.Any(version =>
                version.PackId == pack.Id &&
                version.VersionLabel.Equals(
                    trimmedVersionLabel,
                    StringComparison.OrdinalIgnoreCase));

        if (duplicateVersion)
        {
            throw new InvalidOperationException(
                $"Version '{trimmedVersionLabel}' already exists for this pack.");
        }

        string sourceManifestPath =
            Path.Combine(
                pack.LocalPath,
                "manifest.json");

        if (!File.Exists(sourceManifestPath))
        {
            throw new FileNotFoundException(
                "The pack must be scanned before creating a version.",
                sourceManifestPath);
        }

        await ValidateManifestAsync(
            sourceManifestPath,
            cancellationToken);

        Guid versionId = Guid.NewGuid();

        string versionDirectory =
            Path.Combine(
                _versionsDirectory,
                pack.Id.ToString(),
                versionId.ToString());

        Directory.CreateDirectory(
            versionDirectory);

        string storedManifestPath =
            Path.Combine(
                versionDirectory,
                "manifest.json");

        File.Copy(
            sourceManifestPath,
            storedManifestPath,
            overwrite: false);

        var version = new PackVersion
        {
            Id = versionId,
            PackId = pack.Id,
            VersionLabel =
                trimmedVersionLabel,
            ReleaseNotes =
                NormaliseOptionalValue(
                    releaseNotes),
            ManifestPath =
                storedManifestPath,
            CreatedAt =
                DateTimeOffset.UtcNow,
            IsPublished = false
        };

        _settings.Versions.Add(version);

        await SaveAsync(cancellationToken);

        return version;
    }

    public async Task<PackVersion> PublishVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        PackVersion version =
            GetVersion(versionId)
            ?? throw new KeyNotFoundException(
                $"No version exists with ID '{versionId}'.");

        for (int index = 0;
             index < _settings.Versions.Count;
             index++)
        {
            PackVersion current =
                _settings.Versions[index];

            if (current.PackId != version.PackId)
            {
                continue;
            }

            _settings.Versions[index] =
                current with
                {
                    IsPublished =
                        current.Id == versionId
                };
        }

        await SaveAsync(cancellationToken);

        return GetVersion(versionId)
            ?? throw new InvalidOperationException(
                "The published version could not be reloaded.");
    }

    public async Task<bool> RemoveVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        PackVersion? version =
            GetVersion(versionId);

        if (version is null)
        {
            return false;
        }

        _settings.Versions.Remove(version);

        string? versionDirectory =
            Path.GetDirectoryName(
                version.ManifestPath);

        if (!string.IsNullOrWhiteSpace(
                versionDirectory) &&
            Directory.Exists(versionDirectory))
        {
            Directory.Delete(
                versionDirectory,
                recursive: true);
        }

        await SaveAsync(cancellationToken);

        return true;
    }

    public async Task SaveAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(
            _versionsDirectory);

        string json =
            JsonSerializer.Serialize(
                _settings,
                _jsonOptions);

        string temporaryPath =
            _registryPath + ".tmp";

        await File.WriteAllTextAsync(
            temporaryPath,
            json,
            cancellationToken);

        File.Move(
            temporaryPath,
            _registryPath,
            overwrite: true);
    }

    private async Task ValidateManifestAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        string json =
            await File.ReadAllTextAsync(
                manifestPath,
                cancellationToken);

        ModpackManifest? manifest =
            JsonSerializer.Deserialize<ModpackManifest>(
                json,
                _jsonOptions);

        if (manifest is null)
        {
            throw new InvalidDataException(
                $"The manifest is invalid: {manifestPath}");
        }
    }

    private static string GetDefaultVersionsDirectory()
    {
        string applicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            applicationData,
            "ModpackSync",
            "versions");
    }

    private static string? NormaliseOptionalValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}