using ModpackSync.Contracts.Instances;
using ModpackSync.Core.Packs;

namespace ModpackSync.Core.Instances;

public sealed class InstanceDiscoveryService
{
    private readonly PackManager _packManager;

    public InstanceDiscoveryService(
        PackManager packManager)
    {
        _packManager = packManager;
    }

    public IReadOnlyList<LauncherInstance> Discover(
        string instancesDirectory)
    {
        if (string.IsNullOrWhiteSpace(
                instancesDirectory))
        {
            throw new ArgumentException(
                "The instances directory cannot be empty.",
                nameof(instancesDirectory));
        }

        string fullInstancesDirectory =
            Path.GetFullPath(instancesDirectory);

        if (!Directory.Exists(fullInstancesDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The instances directory does not exist: " +
                fullInstancesDirectory);
        }

        var discoveredInstances =
            new List<LauncherInstance>();

        foreach (string instancePath in
                 Directory.EnumerateDirectories(
                     fullInstancesDirectory))
        {
            bool hasPrismMetadata =
                File.Exists(
                    Path.Combine(
                        instancePath,
                        "instance.cfg")) ||
                File.Exists(
                    Path.Combine(
                        instancePath,
                        "mmc-pack.json"));

            if (!hasPrismMetadata)
            {
                continue;
            }

            string? minecraftPath =
                FindMinecraftDirectory(
                    instancePath);

            if (minecraftPath is null)
            {
                continue;
            }

            string instanceName =
                GetInstanceName(
                    instancePath);

            bool isManaged =
                _packManager.Packs.Any(pack =>
                    PathsAreEqual(
                        pack.LocalPath,
                        minecraftPath));

            discoveredInstances.Add(
                new LauncherInstance
                {
                    Name = instanceName,
                    InstancePath = instancePath,
                    MinecraftPath = minecraftPath,
                    IsManaged = isManaged
                });
        }

        return discoveredInstances
            .OrderBy(
                instance => instance.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? FindMinecraftDirectory(
        string instancePath)
    {
        string minecraftPath =
            Path.Combine(
                instancePath,
                "minecraft");

        if (Directory.Exists(minecraftPath))
        {
            return minecraftPath;
        }

        string dotMinecraftPath =
            Path.Combine(
                instancePath,
                ".minecraft");

        if (Directory.Exists(dotMinecraftPath))
        {
            return dotMinecraftPath;
        }

        return null;
    }

    private static string GetInstanceName(
        string instancePath)
    {
        string instanceConfigPath =
            Path.Combine(
                instancePath,
                "instance.cfg");

        if (File.Exists(instanceConfigPath))
        {
            foreach (string line in
                     File.ReadLines(instanceConfigPath))
            {
                if (!line.StartsWith(
                        "name=",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string configuredName =
                    line["name=".Length..].Trim();

                if (!string.IsNullOrWhiteSpace(
                        configuredName))
                {
                    return configuredName;
                }
            }
        }

        return Path.GetFileName(instancePath);
    }

    private static bool PathsAreEqual(
        string firstPath,
        string secondPath)
    {
        string firstFullPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(firstPath));

        string secondFullPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(secondPath));

        return firstFullPath.Equals(
            secondFullPath,
            StringComparison.OrdinalIgnoreCase);
    }
}