namespace ModpackSync.Contracts.Server.Files;

public sealed record FileCheckResponse
{
    public required IReadOnlyList<string> ExistingHashes
    {
        get;
        init;
    }

    public required IReadOnlyList<string> MissingHashes
    {
        get;
        init;
    }
}