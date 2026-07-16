using ModpackSync.Cli.Helpers;
using ModpackSync.Contracts.Packs;
using ModpackSync.Core.Packs;

namespace ModpackSync.Cli.Commands;

public sealed class PackAddCommand : ICommand
{
    private readonly PackManager _packManager;

    public PackAddCommand(
        PackManager packManager)
    {
        _packManager = packManager;
    }

    public string Name => "pack-add";

    public string Usage =>
        "pack-add <name> <folder path> " +
        "[Minecraft version] [loader] [loader version]";

    public async Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length < 2)
        {
            Console.WriteLine($"Usage: {Usage}");
            return;
        }

        string name = args[0];
        string folderPath = args[1];

        string? minecraftVersion =
            args.Length >= 3
                ? args[2]
                : null;

        string? modLoader =
            args.Length >= 4
                ? args[3]
                : null;

        string? modLoaderVersion =
            args.Length >= 5
                ? args[4]
                : null;

        ModpackRegistration pack =
            await _packManager.AddPackAsync(
                name,
                folderPath,
                minecraftVersion,
                modLoader,
                modLoaderVersion,
                cancellationToken);

        Console.WriteLine();
        Console.WriteLine("Pack registered.");
        Console.WriteLine($"ID:        {pack.Id}");
        Console.WriteLine($"Name:      {pack.Name}");
        Console.WriteLine($"Path:      {pack.LocalPath}");
        Console.WriteLine(
            $"Minecraft: {pack.MinecraftVersion ?? "Not set"}");
        Console.WriteLine(
            $"Loader:    {ConsoleFormatting.FormatLoader(pack)}");
        Console.WriteLine(
            $"Registry:  {_packManager.SettingsFilePath}");
    }
}