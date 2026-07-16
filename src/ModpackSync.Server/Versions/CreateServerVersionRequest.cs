namespace ModpackSync.Contracts.Server.Versions;

public sealed record CreateServerVersionRequest
{
    public required string VersionLabel { get; init; }

    public string? ReleaseNotes { get; init; }
}