namespace ModpackSync.Contracts.Instances;

public sealed record InstanceDiscoverySettings
{
    public string? InstancesDirectory { get; init; }
}