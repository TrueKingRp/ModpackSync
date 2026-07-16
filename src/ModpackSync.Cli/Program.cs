using ModpackSync.Cli.Commands;
using ModpackSync.Core.Instances;
using ModpackSync.Core.Packs;
using ModpackSync.Core.Versions;

var packManager = new PackManager();
var versionManager = new VersionManager();

var instanceSettingsManager =
    new InstanceSettingsManager();

var instanceDiscoveryService =
    new InstanceDiscoveryService(
        packManager);

try
{
    await packManager.InitialiseAsync();
    await versionManager.InitialiseAsync();
    await instanceSettingsManager.InitialiseAsync();

    ICommand[] commands =
    [
        new ScanCommand(),

        new PackAddCommand(
            packManager),

        new PackListCommand(
            packManager),

        new PackRemoveCommand(
            packManager),

        new PackScanCommand(
            packManager),

        new InstancesSetCommand(
            instanceSettingsManager),

        new InstancesListCommand(
            instanceSettingsManager,
            instanceDiscoveryService),

        new VersionCreateCommand(
            packManager,
            versionManager),

        new VersionListCommand(
            packManager,
            versionManager),

        new VersionPublishCommand(
            versionManager),

        new VersionRemoveCommand(
            versionManager)
    ];

    if (args.Length == 0)
    {
        PrintUsage(commands);
        return;
    }

    string commandName =
        args[0].Trim();

    ICommand? command =
        commands.FirstOrDefault(command =>
            command.Name.Equals(
                commandName,
                StringComparison.OrdinalIgnoreCase));

    if (command is null)
    {
        Console.Error.WriteLine(
            $"Unknown command: {commandName}");

        Console.WriteLine();

        PrintUsage(commands);
        return;
    }

    string[] commandArguments =
        args.Skip(1)
            .ToArray();

    await command.ExecuteAsync(
        commandArguments);
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(
        $"Error: {ex.Message}");
}

static void PrintUsage(
    IEnumerable<ICommand> commands)
{
    Console.WriteLine(
        "ModpackSync CLI");

    Console.WriteLine();

    Console.WriteLine(
        "Available commands:");

    foreach (ICommand command in commands)
    {
        Console.WriteLine(
            $"  {command.Usage}");
    }
}