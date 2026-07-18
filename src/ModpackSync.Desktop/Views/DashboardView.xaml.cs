using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ModpackSync.Contracts.Instances;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Versions;
using ModpackSync.Core.Instances;
using ModpackSync.Core.Packs;
using ModpackSync.Core.Settings;
using ModpackSync.Core.Versions;
using System.ComponentModel;
using System.Windows.Data;
using ModpackSync.Desktop.Models;

namespace ModpackSync.Desktop.Views;

public partial class DashboardView : UserControl
{
    private readonly PackManager _packManager;
    private readonly VersionManager _versionManager;
    private readonly InstanceSettingsManager _instanceSettingsManager;
    private readonly InstanceDiscoveryService _instanceDiscoveryService;
    private readonly ServerSettingsManager _serverSettingsManager;
    private HttpClient _httpClient;

    private readonly JsonSerializerOptions _jsonOptions;

    private readonly ObservableCollection<InstanceListItem> _instances = [];

    private readonly ICollectionView _instancesView;

    private bool _isInitialised;

    public event Action<ModpackRegistration>? OpenVersionsRequested;

    public ModpackRegistration? SelectedManagedPack =>
        GetSelectedInstance()?.ManagedPack;

    public DashboardView()
    {
        InitializeComponent();

        _packManager = new PackManager();
        _versionManager = new VersionManager();
        _instanceSettingsManager =
            new InstanceSettingsManager();

        _serverSettingsManager =
            new ServerSettingsManager();

        _instanceDiscoveryService =
            new InstanceDiscoveryService(
                _packManager);

        _httpClient =
            new HttpClient();

        _jsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        _instancesView =
            CollectionViewSource.GetDefaultView(
                _instances);

        _instancesView.Filter =
            FilterInstance;

        InstancesDataGrid.ItemsSource =
            _instancesView;

        Loaded += DashboardView_Loaded;
    }

    private async void DashboardView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_isInitialised)
        {
            return;
        }

        _isInitialised = true;

        try
        {
            SetBusyState(
                true,
                "Loading ModpackSync...");

            await _packManager.InitialiseAsync();
            await _versionManager.InitialiseAsync();
            await _instanceSettingsManager.InitialiseAsync();
            await _serverSettingsManager.InitialiseAsync();

            InstancesDirectoryTextBox.Text =
                _instanceSettingsManager
                    .Settings
                    .InstancesDirectory
                ?? string.Empty;

            await RefreshInstancesAsync();

            SetBusyState(
                false,
                $"Found {_instances.Count} Prism instances.");
        }
        catch (Exception ex)
        {
            ShowError(
                "ModpackSync could not be initialised.",
                ex);
        }
    }

    private async void BrowseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title =
                "Select your Prism Launcher instances folder",

            Multiselect = false
        };

        string? existingDirectory =
            _instanceSettingsManager
                .Settings
                .InstancesDirectory;

        if (!string.IsNullOrWhiteSpace(existingDirectory) &&
            Directory.Exists(existingDirectory))
        {
            dialog.InitialDirectory =
                existingDirectory;
        }

        bool? result =
            dialog.ShowDialog(
                Window.GetWindow(this));

        if (result != true)
        {
            return;
        }

        try
        {
            SetBusyState(
                true,
                "Saving instances folder...");

            await _instanceSettingsManager
                .SetInstancesDirectoryAsync(
                    dialog.FolderName);

            InstancesDirectoryTextBox.Text =
                dialog.FolderName;

            await RefreshInstancesAsync();

            SetBusyState(
                false,
                $"Found {_instances.Count} Prism instances.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The instances folder could not be saved.",
                ex);
        }
    }

    private async void RefreshButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            SetBusyState(
                true,
                "Refreshing instances...");

            await RefreshInstancesAsync();

            SetBusyState(
                false,
                $"Found {_instances.Count} Prism instances.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The instances could not be refreshed.",
                ex);
        }
    }

    private async Task RefreshInstancesAsync()
    {
        string? selectedPath =
            GetSelectedInstance()?.MinecraftPath;

        _instances.Clear();

        string? instancesDirectory =
            _instanceSettingsManager
                .Settings
                .InstancesDirectory;

        if (!string.IsNullOrWhiteSpace(
        selectedPath))
        {
            SelectInstanceByPath(
                selectedPath);
        }

        _instancesView.Refresh();

        UpdateActionButtons();

        await Task.CompletedTask;

        IReadOnlyList<LauncherInstance> discoveredInstances =
            _instanceDiscoveryService.Discover(
                instancesDirectory);

        foreach (LauncherInstance instance
                 in discoveredInstances)
        {
            ModpackRegistration? managedPack =
                _packManager.Packs.FirstOrDefault(
                    pack =>
                        PathsAreEqual(
                            pack.LocalPath,
                            instance.MinecraftPath));

            _instances.Add(
                new InstanceListItem
                {
                    Instance = instance,
                    ManagedPack = managedPack
                });
        }

        if (!string.IsNullOrWhiteSpace(
                selectedPath))
        {
            SelectInstanceByPath(
                selectedPath);
        }

        UpdateActionButtons();

        await Task.CompletedTask;
    }

    private async void ManagePackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        if (selectedItem is null ||
            selectedItem.IsManaged)
        {
            return;
        }

        try
        {
            SetBusyState(
                true,
                $"Registering {selectedItem.Name}...");

            await _packManager.AddPackAsync(
                selectedItem.Name,
                selectedItem.MinecraftPath);

            await RefreshInstancesAsync();

            SelectInstanceByPath(
                selectedItem.MinecraftPath);

            SetBusyState(
                false,
                $"{selectedItem.Name} is now managed.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The pack could not be registered.",
                ex);
        }
    }

    private async void ScanPackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        if (selectedItem?.ManagedPack is null)
        {
            return;
        }

        try
        {
            SetBusyState(
                true,
                $"Scanning {selectedItem.Name}...");

            ModpackManifest manifest =
                await _packManager.ScanPackAsync(
                    selectedItem.ManagedPack.Id);

            await RefreshInstancesAsync();

            SelectInstanceByPath(
                selectedItem.MinecraftPath);

            SetBusyState(
                false,
                $"Scan complete: {manifest.Files.Count} files found.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The pack could not be scanned.",
                ex);
        }
    }

    private async void CreateVersionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        if (selectedItem?.ManagedPack is null)
        {
            return;
        }

        try
        {
            await _versionManager.InitialiseAsync();

            IReadOnlyList<PackVersion> existingVersions =
                _versionManager.GetVersions(
                    selectedItem.ManagedPack.Id);

            var dialog =
                new CreateVersionWindow(
                    selectedItem.Name,
                    existingVersions.Select(
                        version =>
                            version.VersionLabel))
                {
                    Owner = Window.GetWindow(this)
                };

            bool? result =
                dialog.ShowDialog();

            if (result != true)
            {
                return;
            }

            SetBusyState(
                true,
                $"Creating version {dialog.VersionLabel}...");

            await _packManager.ScanPackAsync(
                selectedItem.ManagedPack.Id);

            PackVersion version =
                await _versionManager.CreateVersionAsync(
                    selectedItem.ManagedPack,
                    dialog.VersionLabel,
                    dialog.ReleaseNotes);

            SetBusyState(
                false,
                $"Created version {version.VersionLabel}.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The version could not be created.",
                ex);
        }
    }

    private void ViewVersionsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ModpackRegistration? managedPack =
            SelectedManagedPack;

        if (managedPack is null)
        {
            return;
        }

        OpenVersionsRequested?.Invoke(
            managedPack);
    }

    private async void UploadButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        ModpackRegistration? managedPack =
            selectedItem?.ManagedPack;

        if (selectedItem is null ||
            managedPack is null)
        {
            return;
        }

        try
        {
            SetBusyState(
                true,
                $"Preparing {selectedItem.Name} for upload...");

            await _versionManager.InitialiseAsync();

            PackVersion? localVersion =
                _versionManager
                    .GetVersions(managedPack.Id)
                    .OrderByDescending(
                        version =>
                            version.CreatedAt)
                    .FirstOrDefault();

            if (localVersion is null)
            {
                throw new InvalidOperationException(
                    "This pack has no local versions to upload.");
            }

            if (string.IsNullOrWhiteSpace(
                    localVersion.ManifestPath) ||
                !File.Exists(
                    localVersion.ManifestPath))
            {
                throw new FileNotFoundException(
                    "The selected version's manifest could not be found.",
                    localVersion.ManifestPath);
            }

            ModpackManifest manifest =
                await LoadManifestAsync(
                    localVersion.ManifestPath);

            await PrepareHttpClientAsync();

            SetBusyState(
                true,
                "Finding the pack on the server...");

            ServerPack serverPack =
                await GetOrCreateServerPackAsync(
                    managedPack.Name);

            SetBusyState(
                true,
                $"Preparing server version {localVersion.VersionLabel}...");

            ServerVersion serverVersion =
                await GetOrCreateServerVersionAsync(
                    serverPack.Id,
                    localVersion);

            SetBusyState(
                true,
                "Checking which files are already on the server...");

            HashSet<string> missingHashes =
                await GetMissingHashesAsync(
                    manifest.Files.Select(
                        file =>
                            file.Sha256));

            int uploadedCount = 0;

            foreach (ManifestFile manifestFile
         in manifest.Files)
            {
                if (!missingHashes.Contains(
                        manifestFile.Sha256))
                {
                    continue;
                }

                string sourcePath =
                    GetManifestSourcePath(
                        managedPack.LocalPath,
                        manifestFile.RelativePath);

                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException(
                        $"A manifest file could not be found: " +
                        $"{manifestFile.RelativePath}",
                        sourcePath);
                }

                uploadedCount++;

                ShowProgress(
                    uploadedCount,
                    missingHashes.Count,
                    $"Uploading file {uploadedCount} of " +
                    $"{missingHashes.Count}: " +
                    $"{manifestFile.RelativePath}");

                await UploadFileAsync(
                    manifestFile.Sha256,
                    sourcePath);
            }

            ShowIndeterminateProgress(
                "Sending the manifest to the server...");

            await AttachVersionFilesAsync(
                serverVersion.Id,
                manifest.Files);

            SetBusyState(
                false,
                $"Version {localVersion.VersionLabel} was uploaded. " +
                "The server is processing its manifest.");

            MessageBox.Show(
                $"Version {localVersion.VersionLabel} was uploaded." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "The server is processing the manifest in the background. " +
                "The version may take a few minutes to become available.",
                "ModpackSync",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError(
                "The pack could not be uploaded.",
                ex);
        }
    }

    private async void DownloadButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        ModpackRegistration? managedPack =
            selectedItem?.ManagedPack;

        if (selectedItem is null ||
            managedPack is null)
        {
            return;
        }

        try
        {
            SetBusyState(
                true,
                $"Finding {selectedItem.Name} on the server...");

            await PrepareHttpClientAsync();

            ServerPack? serverPack =
                await FindServerPackAsync(
                    managedPack.Name);

            if (serverPack is null)
            {
                throw new InvalidOperationException(
                    "This pack does not exist on the server.");
            }

            IReadOnlyList<ServerVersion> versions =
                await GetServerVersionsAsync(
                    serverPack.Id);

            ServerVersion? latestVersion =
                versions
                    .OrderByDescending(
                        version =>
                            version.CreatedAt)
                    .FirstOrDefault();

            if (latestVersion is null)
            {
                throw new InvalidOperationException(
                    "This pack has no server versions to download.");
            }

            var dialog =
                new SaveFileDialog
                {
                    Title =
                        "Save downloaded modpack archive",

                    Filter =
                        "ZIP archive (*.zip)|*.zip",

                    DefaultExt =
                        ".zip",

                    AddExtension =
                        true,

                    FileName =
                        MakeSafeFileName(
                            $"{serverPack.Name}-" +
                            $"{latestVersion.VersionLabel}.zip")
                };

            bool? result =
                dialog.ShowDialog(
                    Window.GetWindow(this));

            if (result != true)
            {
                SetBusyState(
                    false,
                    "Download cancelled.");

                return;
            }

            SetBusyState(
                true,
                $"Downloading version " +
                $"{latestVersion.VersionLabel}...");

            await DownloadArchiveAsync(
                latestVersion.Id,
                dialog.FileName);

            SetBusyState(
                false,
                $"Downloaded {latestVersion.VersionLabel}.");

            MessageBox.Show(
                $"The archive was saved to:" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                dialog.FileName,
                "ModpackSync",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError(
                "The pack could not be downloaded.",
                ex);
        }
    }

    private async Task PrepareHttpClientAsync()
    {
        await _serverSettingsManager.InitialiseAsync();

        string serverUrl =
            _serverSettingsManager.Settings.ServerUrl;

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new InvalidOperationException(
                "No server URL has been configured.");
        }

        serverUrl =
            serverUrl.TrimEnd('/') + "/";

        var requestedBaseAddress =
            new Uri(
                serverUrl,
                UriKind.Absolute);

        // HttpClient.BaseAddress cannot be changed after the first
        // request has been sent. Recreate the client only if the
        // configured server address has changed.
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress =
                requestedBaseAddress;
        }
        else if (_httpClient.BaseAddress !=
                 requestedBaseAddress)
        {
            _httpClient.Dispose();

            _httpClient =
                new HttpClient
                {
                    BaseAddress =
                        requestedBaseAddress
                };
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            null;

        string apiKey =
            _serverSettingsManager.Settings.ApiKey;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey);
        }
    }

    private async Task<ModpackManifest> LoadManifestAsync(
        string manifestPath)
    {
        string json =
            await File.ReadAllTextAsync(
                manifestPath);

        return JsonSerializer.Deserialize<ModpackManifest>(
                   json,
                   _jsonOptions)
               ?? throw new InvalidDataException(
                   "The version manifest is invalid.");
    }

    private async Task<ServerPack> GetOrCreateServerPackAsync(
        string packName)
    {
        ServerPack? existingPack =
            await FindServerPackAsync(
                packName);

        if (existingPack is not null)
        {
            return existingPack;
        }

        string json =
            JsonSerializer.Serialize(
                new
                {
                    Name = packName
                },
                _jsonOptions);

        using var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        using HttpResponseMessage response =
            await _httpClient.PostAsync(
                "api/packs",
                content);

        await EnsureSuccessAsync(
            response,
            "The server pack could not be created.");

        return await DeserializeResponseAsync<ServerPack>(
            response);
    }

    private async Task<ServerPack?> FindServerPackAsync(
        string packName)
    {
        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                "api/packs");

        await EnsureSuccessAsync(
            response,
            "The server pack list could not be loaded.");

        IReadOnlyList<ServerPack> packs =
            await DeserializeResponseAsync<List<ServerPack>>(
                response);

        return packs.FirstOrDefault(
            pack =>
                pack.Name.Equals(
                    packName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ServerVersion>
        GetOrCreateServerVersionAsync(
            Guid serverPackId,
            PackVersion localVersion)
    {
        IReadOnlyList<ServerVersion> versions =
            await GetServerVersionsAsync(
                serverPackId);

        ServerVersion? existingVersion =
            versions.FirstOrDefault(
                version =>
                    version.VersionLabel.Equals(
                        localVersion.VersionLabel,
                        StringComparison.OrdinalIgnoreCase));

        if (existingVersion is not null)
        {
            return existingVersion;
        }

        string json =
            JsonSerializer.Serialize(
                new
                {
                    VersionLabel =
                        localVersion.VersionLabel,

                    ReleaseNotes =
                        localVersion.ReleaseNotes
                },
                _jsonOptions);

        using var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        using HttpResponseMessage response =
            await _httpClient.PostAsync(
                $"api/packs/{serverPackId}/versions",
                content);

        await EnsureSuccessAsync(
            response,
            "The server version could not be created.");

        return await DeserializeResponseAsync<ServerVersion>(
            response);
    }

    private async Task<IReadOnlyList<ServerVersion>>
        GetServerVersionsAsync(
            Guid serverPackId)
    {
        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"api/packs/{serverPackId}/versions");

        await EnsureSuccessAsync(
            response,
            "The server version list could not be loaded.");

        return await DeserializeResponseAsync<
            List<ServerVersion>>(
                response);
    }

    private async Task<HashSet<string>>
        GetMissingHashesAsync(
            IEnumerable<string> hashes)
    {
        string[] distinctHashes =
            hashes
                .Where(
                    hash =>
                        !string.IsNullOrWhiteSpace(hash))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var missingHashes =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        const int batchSize =
            1000;

        for (int offset = 0;
             offset < distinctHashes.Length;
             offset += batchSize)
        {
            string[] batch =
                distinctHashes
                    .Skip(offset)
                    .Take(batchSize)
                    .ToArray();

            ShowProgress(
                Math.Min(
                    offset + batch.Length,
                    distinctHashes.Length),
                distinctHashes.Length,
                $"Checking files on the server: " +
                $"{Math.Min(offset + batch.Length, distinctHashes.Length)} " +
                $"of {distinctHashes.Length}");

            string json =
                JsonSerializer.Serialize(
                    new
                    {
                        Sha256Hashes =
                            batch
                    },
                    _jsonOptions);

            using var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            using HttpResponseMessage response =
                await _httpClient.PostAsync(
                    "api/files/check",
                    content);

            await EnsureSuccessAsync(
                response,
                "The server could not check existing files.");

            FileCheckResponse result =
                await DeserializeResponseAsync<FileCheckResponse>(
                    response);

            foreach (string missingHash
                     in result.MissingHashes)
            {
                missingHashes.Add(
                    missingHash);
            }
        }

        return missingHashes;
    }

    private async Task UploadFileAsync(
        string sha256,
        string filePath)
    {
        await using FileStream stream =
            new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

        using var content =
            new StreamContent(
                stream);

        content.Headers.ContentType =
            new MediaTypeHeaderValue(
                "application/octet-stream");

        using HttpResponseMessage response =
            await _httpClient.PutAsync(
                $"api/files/{sha256}",
                content);

        await EnsureSuccessAsync(
            response,
            $"The file '{Path.GetFileName(filePath)}' " +
            "could not be uploaded.");
    }

    private async Task AttachVersionFilesAsync(
        Guid serverVersionId,
        IReadOnlyList<ManifestFile> files)
    {
        var request =
            new
            {
                Files =
                    files.Select(
                        file =>
                            new
                            {
                                RelativePath =
                                    file.RelativePath,

                                Sha256 =
                                    file.Sha256,

                                Size =
                                    file.Size
                            })
                        .ToArray()
            };

        string json =
            JsonSerializer.Serialize(
                request,
                _jsonOptions);

        using var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        using HttpResponseMessage response =
            await _httpClient.PutAsync(
                $"api/versions/{serverVersionId}/files",
                content);

        await EnsureSuccessAsync(
            response,
            "The manifest could not be attached to the version.");
    }

    private async Task DownloadArchiveAsync(
        Guid serverVersionId,
        string destinationPath)
    {
        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"api/versions/{serverVersionId}/archive",
                HttpCompletionOption.ResponseHeadersRead);

        await EnsureSuccessAsync(
            response,
            "The server archive could not be downloaded.");

        string? destinationDirectory =
            Path.GetDirectoryName(
                destinationPath);

        if (!string.IsNullOrWhiteSpace(
                destinationDirectory))
        {
            Directory.CreateDirectory(
                destinationDirectory);
        }

        await using Stream sourceStream =
            await response.Content
                .ReadAsStreamAsync();

        await using FileStream destinationStream =
            new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

        await sourceStream.CopyToAsync(
            destinationStream);
    }

    private async Task<T> DeserializeResponseAsync<T>(
        HttpResponseMessage response)
    {
        string json =
            await response.Content
                .ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(
                   json,
                   _jsonOptions)
               ?? throw new InvalidDataException(
                   "The server returned an invalid response.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string heading)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body =
            await response.Content
                .ReadAsStringAsync();

        string responseDetails =
            string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ??
                  "Unknown server error."
                : body;

        throw new HttpRequestException(
            $"{heading}{Environment.NewLine}" +
            $"HTTP {(int)response.StatusCode} " +
            $"{response.StatusCode}{Environment.NewLine}" +
            responseDetails);
    }

    private static string GetManifestSourcePath(
        string packDirectory,
        string relativePath)
    {
        string normalisedRelativePath =
            relativePath
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar)
                .Replace(
                    '\\',
                    Path.DirectorySeparatorChar);

        return Path.Combine(
            packDirectory,
            normalisedRelativePath);
    }

    private static string MakeSafeFileName(
        string fileName)
    {
        foreach (char invalidCharacter
                 in Path.GetInvalidFileNameChars())
        {
            fileName =
                fileName.Replace(
                    invalidCharacter,
                    '_');
        }

        return fileName;
    }

    private static string FormatFileSize(
    long byteCount)
    {
        string[] units =
        [
            "B",
        "KB",
        "MB",
        "GB",
        "TB"
        ];

        double size = byteCount;
        int unit = 0;

        while (size >= 1024 &&
               unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    private bool FilterInstance(
    object item)
    {
        if (item is not InstanceListItem instance)
        {
            return false;
        }

        string searchText =
            PackSearchTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                searchText))
        {
            return true;
        }

        return ContainsSearchText(
                   instance.Name,
                   searchText) ||
               ContainsSearchText(
                   instance.MinecraftPath,
                   searchText) ||
               ContainsSearchText(
                   instance.Status,
                   searchText);
    }

    private static bool ContainsSearchText(
        string? value,
        string searchText)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(
                   searchText,
                   StringComparison.OrdinalIgnoreCase);
    }

    private void PackSearchTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        bool hasSearchText =
            !string.IsNullOrWhiteSpace(
                PackSearchTextBox.Text);

        SearchPlaceholderTextBlock.Visibility =
            hasSearchText
                ? Visibility.Collapsed
                : Visibility.Visible;

        ClearSearchButton.Visibility =
            hasSearchText
                ? Visibility.Visible
                : Visibility.Collapsed;

        _instancesView.Refresh();

        /*
         * A selected row may become hidden after filtering.
         * Clear the selection when that happens so the action
         * buttons do not operate on an invisible pack.
         */
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        if (selectedItem is not null &&
            !_instancesView.Contains(
                selectedItem))
        {
            InstancesDataGrid.SelectedItem =
                null;
        }

        UpdateActionButtons();
    }

    private void ClearSearchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        PackSearchTextBox.Clear();
        PackSearchTextBox.Focus();
    }

    private void InstancesDataGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateActionButtons();
    }

    private InstanceListItem? GetSelectedInstance()
    {
        return InstancesDataGrid.SelectedItem
            as InstanceListItem;
    }

    private void UpdateActionButtons()
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        bool hasSelection =
            selectedItem is not null;

        bool isManaged =
            selectedItem?.IsManaged == true;

        ManagePackButton.IsEnabled =
            hasSelection && !isManaged;

        ScanPackButton.IsEnabled =
            isManaged;

        CreateVersionButton.IsEnabled =
            isManaged;

        ViewVersionsButton.IsEnabled =
            isManaged;

        UploadButton.IsEnabled =
            isManaged;

        DownloadButton.IsEnabled =
            isManaged;
    }

    private void SelectInstanceByPath(
        string minecraftPath)
    {
        InstanceListItem? matchingItem =
            _instances.FirstOrDefault(
                item =>
                    PathsAreEqual(
                        item.MinecraftPath,
                        minecraftPath));

        if (matchingItem is null)
        {
            return;
        }

        InstancesDataGrid.SelectedItem =
            matchingItem;

        InstancesDataGrid.ScrollIntoView(
            matchingItem);
    }

    private void ShowProgress(
    long completed,
    long total,
    string message)
    {
        StatusTextBlock.Text =
            message;

        OperationProgressBar.Visibility =
            Visibility.Visible;

        OperationProgressBar.IsIndeterminate =
            total <= 0;

        if (total <= 0)
        {
            return;
        }

        OperationProgressBar.Minimum = 0;
        OperationProgressBar.Maximum = total;
        OperationProgressBar.Value =
            Math.Clamp(completed, 0, total);
    }

    private void ShowIndeterminateProgress(
        string message)
    {
        StatusTextBlock.Text =
            message;

        OperationProgressBar.Visibility =
            Visibility.Visible;

        OperationProgressBar.IsIndeterminate =
            true;
    }

    private void HideProgress()
    {
        OperationProgressBar.IsIndeterminate =
            false;

        OperationProgressBar.Value =
            0;

        OperationProgressBar.Visibility =
            Visibility.Collapsed;
    }

    private void SetBusyState(
        bool isBusy,
        string message)
    {
        StatusTextBlock.Text =
            message;

        if (!isBusy)
        {
            HideProgress();
        }

        BrowseButton.IsEnabled =
            !isBusy;

        RefreshButton.IsEnabled =
            !isBusy;

        InstancesDataGrid.IsEnabled =
            !isBusy;

        if (isBusy)
        {
            ManagePackButton.IsEnabled = false;
            ScanPackButton.IsEnabled = false;
            CreateVersionButton.IsEnabled = false;
            ViewVersionsButton.IsEnabled = false;
            UploadButton.IsEnabled = false;
            DownloadButton.IsEnabled = false;

            return;
        }

        UpdateActionButtons();
    }

    private void ShowError(
        string heading,
        Exception exception)
    {
        SetBusyState(
            false,
            exception.Message);

        MessageBox.Show(
            $"{heading}{Environment.NewLine}{Environment.NewLine}" +
            exception.Message,
            "ModpackSync",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static bool PathsAreEqual(
        string firstPath,
        string secondPath)
    {
        string firstFullPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    firstPath));

        string secondFullPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    secondPath));

        return firstFullPath.Equals(
            secondFullPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ServerPack
    {
        public Guid Id { get; init; }

        public string Name { get; init; } =
            string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public int VersionCount { get; init; }
    }

    private sealed record ServerVersion
    {
        public Guid Id { get; init; }

        public Guid PackId { get; init; }

        public string VersionLabel { get; init; } =
            string.Empty;

        public string ReleaseNotes { get; init; } =
            string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public bool IsComplete { get; init; }

        public bool IsPublished { get; init; }

        public int FileCount { get; init; }
    }

    private sealed record FileCheckResponse
    {
        public IReadOnlyList<string> ExistingHashes
        {
            get;
            init;
        } = [];

        public IReadOnlyList<string> MissingHashes
        {
            get;
            init;
        } = [];
    }
}