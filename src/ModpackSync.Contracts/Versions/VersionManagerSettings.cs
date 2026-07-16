namespace ModpackSync.Contracts.Versions;

public sealed record VersionManagerSettings
{
    public List<PackVersion> Versions { get; init; } = [];
}