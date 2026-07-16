using System.Text.Json;
using ModpackSync.Contracts.Instances;

namespace ModpackSync.Core.Instances;

public sealed class InstanceSettingsManager
{
    private readonly string _settingsFilePath;

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

    public InstanceSettingsManager(
        string? settingsFilePath = null)
    {
        _settingsFilePath =
            settingsFilePath ??
            GetDefaultSettingsFilePath();
    }

    public InstanceDiscoverySettings Settings { get; private set; } =
        new();

    public async Task InitialiseAsync(
        CancellationToken cancellationToken = default)
    {
        string? directory =
            Path.GetDirectoryName(
                _settingsFilePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_settingsFilePath))
        {
            await SaveAsync(cancellationToken);
            return;
        }

        string json =
            await File.ReadAllTextAsync(
                _settingsFilePath,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            Settings =
                new InstanceDiscoverySettings();

            return;
        }

        Settings =
            JsonSerializer.Deserialize<InstanceDiscoverySettings>(
                json,
                _jsonOptions)
            ?? new InstanceDiscoverySettings();
    }

    public async Task SetInstancesDirectoryAsync(
        string instancesDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                instancesDirectory))
        {
            throw new ArgumentException(
                "The instances directory cannot be empty.",
                nameof(instancesDirectory));
        }

        string fullPath =
            Path.GetFullPath(instancesDirectory);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"The instances directory does not exist: {fullPath}");
        }

        Settings =
            new InstanceDiscoverySettings
            {
                InstancesDirectory = fullPath
            };

        await SaveAsync(cancellationToken);
    }

    public async Task SaveAsync(
        CancellationToken cancellationToken = default)
    {
        string? directory =
            Path.GetDirectoryName(
                _settingsFilePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json =
            JsonSerializer.Serialize(
                Settings,
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
            "instance-settings.json");
    }
}