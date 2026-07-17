namespace ModpackSync.Contracts.Server.Versions;

public sealed record ReplaceVersionFilesRequest
{
    public required IReadOnlyList<VersionFileItemRequest> Files
    {
        get;
        init;
    }
}