namespace ModpackSync.Contracts.Sync;

public sealed record CategoryComparison
{
    public int Total { get; init; }

    public int Added { get; init; }

    public int Modified { get; init; }

    public int Deleted { get; init; }

    public int Unchanged { get; init; }
}