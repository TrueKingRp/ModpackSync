namespace ModpackSync.Contracts.Server.Files;

public sealed record FileCheckRequest
{
    public required IReadOnlyList<string> Sha256Hashes
    {
        get;
        init;
    }
}