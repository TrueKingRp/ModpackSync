using ModpackSync.Contracts.Instances;
using ModpackSync.Core.Instances;

namespace ModpackSync.Cli.Commands;

public sealed class InstancesListCommand : ICommand
{
    private readonly InstanceSettingsManager _settingsManager;
    private readonly InstanceDiscoveryService _discoveryService;

    public InstancesListCommand(
        InstanceSettingsManager settingsManager,
        InstanceDiscoveryService discoveryService)
    {
        _settingsManager = settingsManager;
        _discoveryService = discoveryService;
    }

    public string Name => "instances-list";

    public string Usage => "instances-list";

    public Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        string? instancesDirectory =
            _settingsManager.Settings.InstancesDirectory;

        if (string.IsNullOrWhiteSpace(instancesDirectory))
        {
            Console.WriteLine(
                "No instances directory has been configured.");

            Console.WriteLine(
                "Use: instances-set <instances directory>");

            return Task.CompletedTask;
        }

        IReadOnlyList<LauncherInstance> instances =
            _discoveryService.Discover(
                instancesDirectory);

        Console.WriteLine(
            $"Instances directory: {instancesDirectory}");

        Console.WriteLine(
            $"Discovered instances: {instances.Count}");

        foreach (LauncherInstance instance in instances)
        {
            Console.WriteLine();

            string heading =
                instance.Name +
                (instance.IsManaged
                    ? " [Managed]"
                    : string.Empty);

            Console.WriteLine(heading);
            Console.WriteLine(
                new string('-', heading.Length));

            Console.WriteLine(
                $"Instance:  {instance.InstancePath}");

            Console.WriteLine(
                $"Minecraft: {instance.MinecraftPath}");

            Console.WriteLine(
                $"Managed:   {(instance.IsManaged ? "Yes" : "No")}");
        }

        return Task.CompletedTask;
    }
}