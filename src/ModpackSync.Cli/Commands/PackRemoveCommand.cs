using ModpackSync.Core.Packs;

namespace ModpackSync.Cli.Commands;

public sealed class PackRemoveCommand : ICommand
{
    private readonly PackManager _packManager;

    public PackRemoveCommand(
        PackManager packManager)
    {
        _packManager = packManager;
    }

    public string Name => "pack-remove";

    public string Usage =>
        "pack-remove <pack ID>";

    public async Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length < 1 ||
            !Guid.TryParse(
                args[0],
                out Guid packId))
        {
            Console.WriteLine($"Usage: {Usage}");
            return;
        }

        bool removed =
            await _packManager.RemovePackAsync(
                packId,
                cancellationToken);

        Console.WriteLine(
            removed
                ? "Pack removed from the local registry."
                : "No registered pack was found with that ID.");
    }
}