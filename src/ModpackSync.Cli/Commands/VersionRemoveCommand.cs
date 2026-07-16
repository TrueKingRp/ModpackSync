using ModpackSync.Core.Versions;

namespace ModpackSync.Cli.Commands;

public sealed class VersionRemoveCommand : ICommand
{
    private readonly VersionManager _versionManager;

    public VersionRemoveCommand(
        VersionManager versionManager)
    {
        _versionManager = versionManager;
    }

    public string Name => "version-remove";

    public string Usage =>
        "version-remove <version ID>";

    public async Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length < 1 ||
            !Guid.TryParse(
                args[0],
                out Guid versionId))
        {
            Console.WriteLine($"Usage: {Usage}");
            return;
        }

        bool removed =
            await _versionManager.RemoveVersionAsync(
                versionId,
                cancellationToken);

        Console.WriteLine(
            removed
                ? "Version removed."
                : "No version was found with that ID.");
    }
}