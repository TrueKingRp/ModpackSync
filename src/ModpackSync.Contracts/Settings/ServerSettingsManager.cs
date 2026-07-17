using System.Text.Json;
using ModpackSync.Contracts.Settings;

namespace ModpackSync.Core.Settings;

public sealed class ServerSettingsManager
{
    private readonly string _settingsDirectory;
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ServerConnectionSettings Settings { get; private set; } =
        new();

    public ServerSettingsManager(
        string? settingsDirectory = null)
    {
        _settingsDirectory =
            settingsDirectory ??
            GetDefaultSettingsDirectory();

        _settingsPath =
            Path.Combine(
                _settingsDirectory,
                "server-settings.json");

        _jsonOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
    }

    public async Task InitialiseAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(
            _settingsDirectory);

        if (!File.Exists(_settingsPath))
        {
            Settings =
                new ServerConnectionSettings();

            await SaveAsync(
                cancellationToken);

            return;
        }

        string json =
            await File.ReadAllTextAsync(
                _settingsPath,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            Settings =
                new ServerConnectionSettings();

            return;
        }

        Settings =
            JsonSerializer.Deserialize<ServerConnectionSettings>(
                json,
                _jsonOptions)
            ?? new ServerConnectionSettings();
    }

    public async Task UpdateAsync(
        string serverUrl,
        string? apiKey,
        CancellationToken cancellationToken = default)
    {
        string normalisedServerUrl =
            NormaliseServerUrl(
                serverUrl);

        Settings =
            new ServerConnectionSettings
            {
                ServerUrl =
                    normalisedServerUrl,

                ApiKey =
                    apiKey?.Trim()
                    ?? string.Empty
            };

        await SaveAsync(
            cancellationToken);
    }

    public async Task SaveAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(
            _settingsDirectory);

        string json =
            JsonSerializer.Serialize(
                Settings,
                _jsonOptions);

        string temporaryPath =
            _settingsPath + ".tmp";

        await File.WriteAllTextAsync(
            temporaryPath,
            json,
            cancellationToken);

        File.Move(
            temporaryPath,
            _settingsPath,
            overwrite: true);
    }

    private static string NormaliseServerUrl(
        string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(
                serverUrl))
        {
            throw new ArgumentException(
                "The server URL cannot be empty.",
                nameof(serverUrl));
        }

        string trimmedUrl =
            serverUrl.Trim();

        if (!Uri.TryCreate(
                trimmedUrl,
                UriKind.Absolute,
                out Uri? uri))
        {
            throw new ArgumentException(
                "Enter a valid absolute server URL.",
                nameof(serverUrl));
        }

        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "The server URL must use HTTP or HTTPS.",
                nameof(serverUrl));
        }

        return trimmedUrl.TrimEnd('/');
    }

    private static string GetDefaultSettingsDirectory()
    {
        string applicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            applicationData,
            "ModpackSync",
            "settings");
    }
}