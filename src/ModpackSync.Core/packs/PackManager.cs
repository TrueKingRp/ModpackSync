using System.Text.Json;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Packs;
using ModpackSync.Core.Manifests;

namespace ModpackSync.Core.Packs;

public sealed class PackManager
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    private PackManagerSettings _settings = new();

    public PackManager(string? settingsFilePath = null)
    {
        _settingsFilePath =
            settingsFilePath ?? GetDefaultSettingsFilePath();

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public string SettingsFilePath => _settingsFilePath;

    public IReadOnlyList<ModpackRegistration> Packs =>
        _settings.Packs
            .OrderBy(
                pack => pack.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task InitialiseAsync(
        CancellationToken cancellationToken = default)
    {
        string? directory =
            Path.GetDirectoryName(_settingsFilePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_settingsFilePath))
        {
            _settings = new PackManagerSettings();

            await SaveAsync(cancellationToken);
            return;
        }

        string json = await File.ReadAllTextAsync(
            _settingsFilePath,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            _settings = new PackManagerSettings();
            return;
        }

        _settings =
            JsonSerializer.Deserialize<PackManagerSettings>(
                json,
                _jsonOptions)
            ?? new PackManagerSettings();
    }

    public async Task<ModpackRegistration> AddPackAsync(
        string name,
        string localPath,
        string? minecraftVersion = null,
        string? modLoader = null,
        string? modLoaderVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "The pack name cannot be empty.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(localPath))
        {
            throw new ArgumentException(
                "The local path cannot be empty.",
                nameof(localPath));
        }

        string fullLocalPath =
            Path.GetFullPath(localPath);

        if (!Directory.Exists(fullLocalPath))
        {
            throw new DirectoryNotFoundException(
                $"The modpack folder does not exist: {fullLocalPath}");
        }

        bool duplicateName =
            _settings.Packs.Any(pack =>
                pack.Name.Equals(
                    name.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        if (duplicateName)
        {
            throw new InvalidOperationException(
                $"A pack named '{name.Trim()}' is already registered.");
        }

        bool duplicatePath =
            _settings.Packs.Any(pack =>
                PathsAreEqual(
                    pack.LocalPath,
                    fullLocalPath));

        if (duplicatePath)
        {
            throw new InvalidOperationException(
                "That folder is already registered as a modpack.");
        }

        var pack = new ModpackRegistration
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            LocalPath = fullLocalPath,
            MinecraftVersion =
                NormaliseOptionalValue(minecraftVersion),
            ModLoader =
                NormaliseOptionalValue(modLoader),
            ModLoaderVersion =
                NormaliseOptionalValue(modLoaderVersion),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _settings.Packs.Add(pack);

        await SaveAsync(cancellationToken);

        return pack;
    }

    public ModpackRegistration? GetPack(Guid packId)
    {
        return _settings.Packs.FirstOrDefault(
            pack => pack.Id == packId);
    }

    public async Task<bool> RemovePackAsync(
        Guid packId,
        CancellationToken cancellationToken = default)
    {
        ModpackRegistration? pack =
            GetPack(packId);

        if (pack is null)
        {
            return false;
        }

        _settings.Packs.Remove(pack);

        await SaveAsync(cancellationToken);

        return true;
    }

    public async Task<ModpackManifest> ScanPackAsync(
        Guid packId,
        CancellationToken cancellationToken = default)
    {
        ModpackRegistration pack =
            GetPack(packId)
            ?? throw new KeyNotFoundException(
                $"No registered pack exists with ID '{packId}'.");

        if (!Directory.Exists(pack.LocalPath))
        {
            throw new DirectoryNotFoundException(
                $"The modpack folder no longer exists: {pack.LocalPath}");
        }

        string manifestPath =
            Path.Combine(
                pack.LocalPath,
                "manifest.json");

        string oldManifestPath =
            Path.Combine(
                pack.LocalPath,
                "OLDManifest.json");

        if (File.Exists(oldManifestPath))
        {
            File.Delete(oldManifestPath);
        }

        if (File.Exists(manifestPath))
        {
            File.Move(
                manifestPath,
                oldManifestPath);
        }

        var manifestBuilder =
            new ManifestBuilder();

        ModpackManifest manifest =
            await manifestBuilder.BuildAsync(
                pack.Name,
                pack.LocalPath,
                cancellationToken: cancellationToken);

        string manifestJson =
            JsonSerializer.Serialize(
                manifest,
                _jsonOptions);

        await File.WriteAllTextAsync(
            manifestPath,
            manifestJson,
            cancellationToken);

        int packIndex =
            _settings.Packs.FindIndex(
                savedPack => savedPack.Id == packId);

        _settings.Packs[packIndex] =
            pack with
            {
                LastScannedAt =
                    DateTimeOffset.UtcNow
            };

        await SaveAsync(cancellationToken);

        return manifest;
    }

    public async Task<ModpackManifest> ScanPackAsync(
    Guid packId,
    PackContentSelection selection,
    CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        ModpackRegistration pack =
            GetPack(packId)
            ?? throw new KeyNotFoundException(
                $"No registered pack exists with ID '{packId}'.");

        if (!Directory.Exists(pack.LocalPath))
        {
            throw new DirectoryNotFoundException(
                $"The modpack folder no longer exists: {pack.LocalPath}");
        }

        string manifestPath =
            Path.Combine(
                pack.LocalPath,
                "manifest.json");

        string oldManifestPath =
            Path.Combine(
                pack.LocalPath,
                "OLDManifest.json");

        if (File.Exists(oldManifestPath))
        {
            File.Delete(oldManifestPath);
        }

        if (File.Exists(manifestPath))
        {
            File.Move(
                manifestPath,
                oldManifestPath);
        }

        var manifestBuilder =
            new ManifestBuilder();

        ModpackManifest manifest =
            await manifestBuilder.BuildAsync(
                pack.Name,
                pack.LocalPath,
                selection,
                cancellationToken: cancellationToken);

        string manifestJson =
            JsonSerializer.Serialize(
                manifest,
                _jsonOptions);

        await File.WriteAllTextAsync(
            manifestPath,
            manifestJson,
            cancellationToken);

        int packIndex =
            _settings.Packs.FindIndex(
                savedPack =>
                    savedPack.Id == packId);

        _settings.Packs[packIndex] =
            pack with
            {
                LastScannedAt =
                    DateTimeOffset.UtcNow
            };

        await SaveAsync(
            cancellationToken);

        return manifest;
    }

    public async Task SaveAsync(
        CancellationToken cancellationToken = default)
    {
        string? directory =
            Path.GetDirectoryName(_settingsFilePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json =
            JsonSerializer.Serialize(
                _settings,
                _jsonOptions);

        string temporaryPath =
            _settingsFilePath + ".tmp";

        await File.WriteAllTextAsync(
            temporaryPath,
            json,
            cancellationToken);

        File.Move(
            temporaryPath,
            _settingsFilePath,
            overwrite: true);
    }

    private static string GetDefaultSettingsFilePath()
    {
        string applicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            applicationData,
            "ModpackSync",
            "packs.json");
    }

    private static string? NormaliseOptionalValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool PathsAreEqual(
        string firstPath,
        string secondPath)
    {
        string firstFullPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(firstPath));

        string secondFullPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(secondPath));

        return firstFullPath.Equals(
            secondFullPath,
            StringComparison.OrdinalIgnoreCase);
    }
}