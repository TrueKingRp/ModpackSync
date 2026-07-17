using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ModpackSync.Core.Settings;

namespace ModpackSync.Desktop.Views;

public partial class SettingsView : UserControl
{
    private readonly ServerSettingsManager _settingsManager;
    private readonly HttpClient _httpClient;

    private bool _isInitialised;
    private bool _isLoadingSettings;
    private bool _isBusy;

    public event Action<bool>? ConnectionStatusChanged;

    public SettingsView()
    {
        InitializeComponent();

        _settingsManager =
            new ServerSettingsManager();

        _httpClient =
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromSeconds(10)
            };

        Loaded += SettingsView_Loaded;
        Unloaded += SettingsView_Unloaded;
    }

    private async void SettingsView_Loaded(
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
            _isLoadingSettings = true;

            await _settingsManager
                .InitialiseAsync();

            ServerUrlTextBox.Text =
                _settingsManager
                    .Settings
                    .ServerUrl;

            ApiKeyPasswordBox.Password =
                _settingsManager
                    .Settings
                    .ApiKey;

            SaveButton.IsEnabled =
                false;

            SetStatus(
                "Connection has not been tested.",
                "WarningBrush");

            _isLoadingSettings = false;
        }
        catch (Exception ex)
        {
            _isLoadingSettings = false;

            ShowError(
                "The server settings could not be loaded.",
                ex);
        }
    }

    private void SettingsView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _httpClient.CancelPendingRequests();
    }

    private void SettingsField_Changed(
        object sender,
        TextChangedEventArgs e)
    {
        MarkSettingsAsChanged();
    }

    private void ApiKeyPasswordBox_PasswordChanged(
        object sender,
        RoutedEventArgs e)
    {
        MarkSettingsAsChanged();
    }

    private void MarkSettingsAsChanged()
    {
        if (_isLoadingSettings ||
            _isBusy)
        {
            return;
        }

        SaveButton.IsEnabled =
            true;

        SetStatus(
            "Settings have unsaved changes.",
            "WarningBrush");
    }

    private async void SaveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            SetBusyState(
                true,
                "Saving server settings...");

            await SaveSettingsAsync();

            SetBusyState(
                false,
                "Server settings saved.");

            SaveButton.IsEnabled =
                false;
        }
        catch (Exception ex)
        {
            ShowError(
                "The server settings could not be saved.",
                ex);
        }
    }

    private async void TestConnectionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            SetBusyState(
                true,
                "Testing server connection...");

            await SaveSettingsAsync();

            SaveButton.IsEnabled =
                false;

            bool connected =
                await TestConnectionAsync();

            if (!connected)
            {
                return;
            }

            SetBusyState(
                false,
                "Connected to the ModpackSync server.",
                "SuccessBrush");

            ConnectionStatusChanged?.Invoke(
                true);
        }
        catch (TaskCanceledException)
        {
            SetBusyState(
                false,
                "The connection attempt timed out.",
                "ErrorBrush");

            ConnectionStatusChanged?.Invoke(
                false);
        }
        catch (HttpRequestException ex)
        {
            SetBusyState(
                false,
                $"The server could not be reached: {ex.Message}",
                "ErrorBrush");

            ConnectionStatusChanged?.Invoke(
                false);
        }
        catch (Exception ex)
        {
            ShowError(
                "The connection could not be tested.",
                ex);

            ConnectionStatusChanged?.Invoke(
                false);
        }
    }

    private async Task SaveSettingsAsync()
    {
        await _settingsManager.UpdateAsync(
            ServerUrlTextBox.Text,
            ApiKeyPasswordBox.Password);
    }

    private async Task<bool> TestConnectionAsync()
    {
        string serverUrl =
            _settingsManager
                .Settings
                .ServerUrl;

        string apiKey =
            _settingsManager
                .Settings
                .ApiKey;

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                $"{serverUrl}/api/packs");

        if (!string.IsNullOrWhiteSpace(
                apiKey))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey);

            request.Headers.TryAddWithoutValidation(
                "X-Api-Key",
                apiKey);
        }

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                request);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if (response.StatusCode is
            HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden)
        {
            SetBusyState(
                false,
                "The server responded, but the API key was rejected.",
                "ErrorBrush");

            ConnectionStatusChanged?.Invoke(
                false);

            return false;
        }

        SetBusyState(
            false,
            $"The server responded with HTTP {(int)response.StatusCode} " +
            $"{response.ReasonPhrase}.",
            "ErrorBrush");

        ConnectionStatusChanged?.Invoke(
            false);

        return false;
    }

    private void SetBusyState(
        bool isBusy,
        string message,
        string statusBrushKey = "WarningBrush")
    {
        _isBusy =
            isBusy;

        ServerUrlTextBox.IsEnabled =
            !isBusy;

        ApiKeyPasswordBox.IsEnabled =
            !isBusy;

        SaveButton.IsEnabled =
            !isBusy;

        TestConnectionButton.IsEnabled =
            !isBusy;

        SetStatus(
            message,
            statusBrushKey);
    }

    private void SetStatus(
        string message,
        string brushKey)
    {
        StatusTextBlock.Text =
            message;

        ConnectionStatusEllipse.Fill =
            (Brush)FindResource(
                brushKey);
    }

    private void ShowError(
        string heading,
        Exception exception)
    {
        SetBusyState(
            false,
            exception.Message,
            "ErrorBrush");

        MessageBox.Show(
            $"{heading}{Environment.NewLine}{Environment.NewLine}" +
            exception.Message,
            "ModpackSync",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}