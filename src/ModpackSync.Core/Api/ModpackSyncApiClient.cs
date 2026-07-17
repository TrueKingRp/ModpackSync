using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ModpackSync.Contracts.Settings;
using ModpackSync.Contracts.Versions;
using ModpackSync.Core.Settings;

namespace ModpackSync.Core.Api;

public sealed class ModpackSyncApiClient :
    IModpackSyncApiClient
{
    private readonly HttpClient _httpClient;

    private readonly ServerSettingsManager
        _settingsManager;

    private readonly JsonSerializerOptions
        _jsonOptions;

    public ModpackSyncApiClient()
    {
        _httpClient =
            new HttpClient();

        _settingsManager =
            new ServerSettingsManager();

        _jsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
    }

    private async Task PrepareAsync()
    {
        await _settingsManager.InitialiseAsync();

        string server =
            _settingsManager.Settings.ServerUrl;

        if (string.IsNullOrWhiteSpace(server))
        {
            throw new InvalidOperationException(
                "No server has been configured.");
        }

        _httpClient.BaseAddress =
            new Uri(server);

        _httpClient.DefaultRequestHeaders.Clear();

        if (!string.IsNullOrWhiteSpace(
            _settingsManager.Settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _settingsManager.Settings.ApiKey);
        }
    }

    public async Task<ApiResult> TestConnectionAsync()
    {
        await PrepareAsync();

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "api/packs");

        return new ApiResult(
            response.IsSuccessStatusCode,
            response.ReasonPhrase ??
            "Unknown response");
    }

    public async Task<ApiResult> PublishVersionAsync(
        PackVersion version)
    {
        await PrepareAsync();

        string json =
            JsonSerializer.Serialize(
                version,
                _jsonOptions);

        HttpResponseMessage response =
            await _httpClient.PostAsync(
                "api/versions",
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"));

        return new ApiResult(
            response.IsSuccessStatusCode,
            response.ReasonPhrase ??
            "Unknown response");
    }

    public async Task<IReadOnlyList<PackVersion>>
        GetVersionsAsync(Guid packId)
    {
        await PrepareAsync();

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"api/versions/{packId}");

        response.EnsureSuccessStatusCode();

        string json =
            await response.Content
                .ReadAsStringAsync();

        return JsonSerializer.Deserialize<
            List<PackVersion>>(
                json,
                _jsonOptions)
            ?? [];
    }

    public async Task<ApiResult> DeleteVersionAsync(
        Guid versionId)
    {
        await PrepareAsync();

        HttpResponseMessage response =
            await _httpClient.DeleteAsync(
                $"api/versions/{versionId}");

        return new ApiResult(
            response.IsSuccessStatusCode,
            response.ReasonPhrase ??
            "Unknown response");
    }
}