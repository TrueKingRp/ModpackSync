using ModpackSync.Contracts.Instances;
using ModpackSync.Contracts.Packs;

namespace ModpackSync.Desktop.Models;

public sealed class InstanceListItem
{
    public required LauncherInstance Instance { get; init; }

    public ModpackRegistration? ManagedPack { get; init; }

    public string Name => Instance.Name;

    public string MinecraftPath => Instance.MinecraftPath;

    public bool IsManaged => ManagedPack is not null;

    public string Status =>
        IsManaged
            ? "Managed"
            : "Not managed";
}