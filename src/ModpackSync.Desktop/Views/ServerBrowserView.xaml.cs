using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using ModpackSync.Core.Settings;

namespace ModpackSync.Desktop.Views;

public partial class ServerBrowserView : UserControl
{
    private readonly ServerSettingsManager _serverSettingsManager;

    private readonly ObservableCollection<ServerPack> _packs = [];
    private readonly ObservableCollection<ServerVersion> _versions = [];

    private readonly ICollectionView _packsView;

    private readonly JsonSerializerOptions _jsonOptions;

    private HttpClient _httpClient;

    private bool _isInitialised;

    public ServerBrowserView()
    {
        InitializeComponent();

        _serverSettingsManager =
            new ServerSettingsManager();

        _httpClient =
            new HttpClient();

        _jsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        _packsView =
            CollectionViewSource.GetDefaultView(
                _packs);

        _packsView.Filter =
            FilterPack;

        PacksDataGrid.ItemsSource =
            _packsView;

        VersionsDataGrid.ItemsSource =
            _versions;

        Loaded +=
            ServerBrowserView_Loaded;
    }

    private async void ServerBrowserView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_isInitialised)
        {
            return;
        }

        _isInitialised = true;

        await LoadPacksAsync();
    }

    private async void RefreshButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await LoadPacksAsync();
    }

    private async Task LoadPacksAsync()
    {
        try
        {
            SetBusyState(
                true,
                "Loading packs from the server...");

            await PrepareHttpClientAsync();

            Guid? selectedPackId =
                GetSelectedPack()?.Id;

            using HttpResponseMessage response =
                await _httpClient.GetAsync(
                    "api/packs");

            await EnsureSuccessAsync(
                response,
                "The server pack list could not be loaded.");

            IReadOnlyList<ServerPack> packs =
                await DeserializeResponseAsync<List<ServerPack>>(
                    response);

            _packs.Clear();
            _versions.Clear();

            foreach (ServerPack pack
                     in packs.OrderBy(
                         pack => pack.Name,
                         StringComparer.OrdinalIgnoreCase))
            {
                _packs.Add(
                    pack);
            }

            _packsView.Refresh();

            if (selectedPackId.HasValue)
            {
                ServerPack? selectedPack =
                    _packs.FirstOrDefault(
                        pack =>
                            pack.Id == selectedPackId.Value);

                if (selectedPack is not null &&
                    _packsView.Contains(selectedPack))
                {
                    PacksDataGrid.SelectedItem =
                        selectedPack;

                    PacksDataGrid.ScrollIntoView(
                        selectedPack);
                }
            }

            if (PacksDataGrid.SelectedItem is null)
            {
                SelectedPackTextBlock.Text =
                    "Select a pack";
            }

            SetBusyState(
                false,
                $"Loaded {_packs.Count} server packs.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The server packs could not be loaded.",
                ex);
        }
    }

    private async void PacksDataGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        ServerPack? selectedPack =
            GetSelectedPack();

        _versions.Clear();

        SelectedPackTextBlock.Text =
            selectedPack is null
                ? "Select a pack"
                : selectedPack.Name;

        UpdateActionButtons();

        if (selectedPack is null)
        {
            return;
        }

        await LoadVersionsAsync(
            selectedPack);
    }

    private async Task LoadVersionsAsync(
        ServerPack pack)
    {
        try
        {
            SetBusyState(
                true,
                $"Loading versions for {pack.Name}...");

            await PrepareHttpClientAsync();

            using HttpResponseMessage response =
                await _httpClient.GetAsync(
                    $"api/packs/{pack.Id}/versions");

            await EnsureSuccessAsync(
                response,
                "The server version list could not be loaded.");

            IReadOnlyList<ServerVersion> versions =
                await DeserializeResponseAsync<List<ServerVersion>>(
                    response);

            _versions.Clear();

            foreach (ServerVersion version
                     in versions.OrderByDescending(
                         version => version.CreatedAt))
            {
                _versions.Add(
                    version);
            }

            SetBusyState(
                false,
                $"Loaded {_versions.Count} versions for {pack.Name}.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The server versions could not be loaded.",
                ex);
        }
    }

    private void VersionsDataGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateActionButtons();
    }

    private async void DownloadVersionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ServerPack? selectedPack =
            GetSelectedPack();

        ServerVersion? selectedVersion =
            GetSelectedVersion();

        if (selectedPack is null ||
            selectedVersion is null)
        {
            return;
        }

        var dialog =
            new SaveFileDialog
            {
                Title =
                    "Save server modpack archive",

                Filter =
                    "ZIP archive (*.zip)|*.zip",

                DefaultExt =
                    ".zip",

                AddExtension =
                    true,

                FileName =
                    MakeSafeFileName(
                        $"{selectedPack.Name}-" +
                        $"{selectedVersion.VersionLabel}.zip")
            };

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
                $"Downloading {selectedVersion.VersionLabel}...");

            await PrepareHttpClientAsync();

            using HttpResponseMessage response =
                await _httpClient.GetAsync(
                    $"api/versions/{selectedVersion.Id}/archive",
                    HttpCompletionOption.ResponseHeadersRead);

            await EnsureSuccessAsync(
                response,
                "The server archive could not be downloaded.");

            string? destinationDirectory =
                Path.GetDirectoryName(
                    dialog.FileName);

            if (!string.IsNullOrWhiteSpace(
                    destinationDirectory))
            {
                Directory.CreateDirectory(
                    destinationDirectory);
            }

            await using Stream sourceStream =
                await response.Content.ReadAsStreamAsync();

            await using FileStream destinationStream =
                new(
                    dialog.FileName,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

            await sourceStream.CopyToAsync(
                destinationStream);

            SetBusyState(
                false,
                $"Downloaded {selectedVersion.VersionLabel}.");

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
                "The version could not be downloaded.",
                ex);
        }
    }

    private async void CopyArchiveUrlButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ServerVersion? selectedVersion =
            GetSelectedVersion();

        if (selectedVersion is null)
        {
            return;
        }

        try
        {
            await PrepareHttpClientAsync();

            Uri archiveUri =
                new(
                    _httpClient.BaseAddress!,
                    $"api/versions/{selectedVersion.Id}/archive");

            Clipboard.SetText(
                archiveUri.AbsoluteUri);

            StatusTextBlock.Text =
                "Archive URL copied to the clipboard.";
        }
        catch (Exception ex)
        {
            ShowError(
                "The archive URL could not be copied.",
                ex);
        }
    }

    private void ServerSearchTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        bool hasSearchText =
            !string.IsNullOrWhiteSpace(
                ServerSearchTextBox.Text);

        SearchPlaceholderTextBlock.Visibility =
            hasSearchText
                ? Visibility.Collapsed
                : Visibility.Visible;

        ClearSearchButton.Visibility =
            hasSearchText
                ? Visibility.Visible
                : Visibility.Collapsed;

        _packsView.Refresh();

        ServerPack? selectedPack =
            GetSelectedPack();

        if (selectedPack is not null &&
            !_packsView.Contains(selectedPack))
        {
            PacksDataGrid.SelectedItem =
                null;

            _versions.Clear();

            SelectedPackTextBlock.Text =
                "Select a pack";
        }

        UpdateActionButtons();
    }

    private void ClearSearchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ServerSearchTextBox.Clear();
        ServerSearchTextBox.Focus();
    }

    private bool FilterPack(
        object item)
    {
        if (item is not ServerPack pack)
        {
            return false;
        }

        string searchText =
            ServerSearchTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                searchText))
        {
            return true;
        }

        return pack.Name.Contains(
            searchText,
            StringComparison.OrdinalIgnoreCase);
    }

    private ServerPack? GetSelectedPack()
    {
        return PacksDataGrid.SelectedItem
            as ServerPack;
    }

    private ServerVersion? GetSelectedVersion()
    {
        return VersionsDataGrid.SelectedItem
            as ServerVersion;
    }

    private void UpdateActionButtons()
    {
        bool hasSelection =
            GetSelectedVersion() is not null;

        CopyArchiveUrlButton.IsEnabled =
            hasSelection;

        DownloadVersionButton.IsEnabled =
            hasSelection;
    }

    private async Task PrepareHttpClientAsync()
    {
        await _serverSettingsManager.InitialiseAsync();

        string serverUrl =
            _serverSettingsManager
                .Settings
                .ServerUrl;

        if (string.IsNullOrWhiteSpace(
                serverUrl))
        {
            throw new InvalidOperationException(
                "No server URL has been configured. Open Settings and enter the server address.");
        }

        serverUrl =
            serverUrl.TrimEnd('/') + "/";

        var requestedBaseAddress =
            new Uri(
                serverUrl,
                UriKind.Absolute);

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
            _serverSettingsManager
                .Settings
                .ApiKey;

        if (!string.IsNullOrWhiteSpace(
                apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey);
        }
    }

    private async Task<T> DeserializeResponseAsync<T>(
        HttpResponseMessage response)
    {
        string json =
            await response.Content.ReadAsStringAsync();

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
            await response.Content.ReadAsStringAsync();

        string details =
            string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ??
                  "Unknown server error."
                : body;

        throw new HttpRequestException(
            $"{heading}{Environment.NewLine}" +
            $"HTTP {(int)response.StatusCode} " +
            $"{response.StatusCode}{Environment.NewLine}" +
            details);
    }

    private void SetBusyState(
        bool isBusy,
        string message)
    {
        StatusTextBlock.Text =
            message;

        OperationProgressBar.Visibility =
            isBusy
                ? Visibility.Visible
                : Visibility.Collapsed;

        RefreshButton.IsEnabled =
            !isBusy;

        ServerSearchTextBox.IsEnabled =
            !isBusy;

        ClearSearchButton.IsEnabled =
            !isBusy;

        PacksDataGrid.IsEnabled =
            !isBusy;

        VersionsDataGrid.IsEnabled =
            !isBusy;

        if (isBusy)
        {
            CopyArchiveUrlButton.IsEnabled =
                false;

            DownloadVersionButton.IsEnabled =
                false;
        }
        else
        {
            UpdateActionButtons();
        }
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

    private sealed record ServerPack
    {
        public Guid Id
        {
            get;
            init;
        }

        public string Name
        {
            get;
            init;
        } = string.Empty;

        public DateTimeOffset CreatedAt
        {
            get;
            init;
        }

        public int VersionCount
        {
            get;
            init;
        }
    }

    private sealed record ServerVersion
    {
        public Guid Id
        {
            get;
            init;
        }

        public Guid PackId
        {
            get;
            init;
        }

        public string VersionLabel
        {
            get;
            init;
        } = string.Empty;

        public string ReleaseNotes
        {
            get;
            init;
        } = string.Empty;

        public DateTimeOffset CreatedAt
        {
            get;
            init;
        }

        public bool IsComplete
        {
            get;
            init;
        }

        public bool IsPublished
        {
            get;
            init;
        }

        public int FileCount
        {
            get;
            init;
        }

        public string Status
        {
            get
            {
                if (!IsComplete)
                {
                    return "Processing";
                }

                if (IsPublished)
                {
                    return "Published";
                }

                return "Complete";
            }
        }
    }
}