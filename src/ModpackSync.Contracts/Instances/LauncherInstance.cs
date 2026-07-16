namespace ModpackSync.Contracts.Instances;

public sealed record LauncherInstance
{
    public required string Name { get; init; }

    public required string InstancePath { get; init; }

    public required string MinecraftPath { get; init; }

    public bool IsManaged { get; init; }
}