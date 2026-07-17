namespace ModpackSync.Contracts.Settings;

public sealed record ServerConnectionSettings
{
    public string ServerUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;
}