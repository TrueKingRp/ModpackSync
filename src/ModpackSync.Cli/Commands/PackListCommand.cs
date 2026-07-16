using ModpackSync.Cli.Helpers;
using ModpackSync.Contracts.Packs;
using ModpackSync.Core.Packs;

namespace ModpackSync.Cli.Commands;

public sealed class PackListCommand : ICommand
{
    private readonly PackManager _packManager;

    public PackListCommand(
        PackManager packManager)
    {
        _packManager = packManager;
    }

    public string Name => "pack-list";

    public string Usage => "pack-list";

    public Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModpackRegistration> packs =
            _packManager.Packs;

        if (packs.Count == 0)
        {
            Console.WriteLine(
                "No modpacks are currently registered.");

            return Task.CompletedTask;
        }

        Console.WriteLine(
            $"Registered packs: {packs.Count}");

        foreach (ModpackRegistration pack in packs)
        {
            Console.WriteLine();
            Console.WriteLine(pack.Name);
            Console.WriteLine(
                new string('-', pack.Name.Length));

            Console.WriteLine($"ID:        {pack.Id}");
            Console.WriteLine($"Path:      {pack.LocalPath}");
            Console.WriteLine(
                $"Minecraft: {pack.MinecraftVersion ?? "Not set"}");
            Console.WriteLine(
                $"Loader:    {ConsoleFormatting.FormatLoader(pack)}");
            Console.WriteLine(
                $"Scanned:   {ConsoleFormatting.FormatDate(pack.LastScannedAt)}");
        }

        return Task.CompletedTask;
    }
}