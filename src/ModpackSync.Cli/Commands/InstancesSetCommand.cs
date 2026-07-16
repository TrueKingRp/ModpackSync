using ModpackSync.Core.Instances;

namespace ModpackSync.Cli.Commands;

public sealed class InstancesSetCommand : ICommand
{
    private readonly InstanceSettingsManager _settingsManager;

    public InstancesSetCommand(
        InstanceSettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public string Name => "instances-set";

    public string Usage =>
        "instances-set <instances directory>";

    public async Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length < 1)
        {
            Console.WriteLine($"Usage: {Usage}");
            return;
        }

        string instancesDirectory = args[0];

        await _settingsManager.SetInstancesDirectoryAsync(
            instancesDirectory,
            cancellationToken);

        Console.WriteLine();
        Console.WriteLine(
            "Instances directory saved.");

        Console.WriteLine(
            $"Path: {_settingsManager.Settings.InstancesDirectory}");
    }
}