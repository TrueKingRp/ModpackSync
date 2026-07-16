using ModpackSync.Contracts.Versions;
using ModpackSync.Core.Versions;

namespace ModpackSync.Cli.Commands;

public sealed class VersionPublishCommand : ICommand
{
    private readonly VersionManager _versionManager;

    public VersionPublishCommand(
        VersionManager versionManager)
    {
        _versionManager = versionManager;
    }

    public string Name => "version-publish";

    public string Usage =>
        "version-publish <version ID>";

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

        PackVersion version =
            await _versionManager.PublishVersionAsync(
                versionId,
                cancellationToken);

        Console.WriteLine(
            $"Version {version.VersionLabel} is now published.");
    }
}