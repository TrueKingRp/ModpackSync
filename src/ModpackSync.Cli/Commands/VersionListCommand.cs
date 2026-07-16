using ModpackSync.Cli.Helpers;
using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Versions;
using ModpackSync.Core.Packs;
using ModpackSync.Core.Versions;

namespace ModpackSync.Cli.Commands;

public sealed class VersionListCommand : ICommand
{
    private readonly PackManager _packManager;
    private readonly VersionManager _versionManager;

    public VersionListCommand(
        PackManager packManager,
        VersionManager versionManager)
    {
        _packManager = packManager;
        _versionManager = versionManager;
    }

    public string Name => "version-list";

    public string Usage =>
        "version-list <pack ID>";

    public Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length < 1 ||
            !Guid.TryParse(
                args[0],
                out Guid packId))
        {
            Console.WriteLine($"Usage: {Usage}");
            return Task.CompletedTask;
        }

        ModpackRegistration? pack =
            _packManager.GetPack(packId);

        if (pack is null)
        {
            Console.WriteLine(
                "No registered pack was found with that ID.");

            return Task.CompletedTask;
        }

        IReadOnlyList<PackVersion> versions =
            _versionManager.GetVersions(packId);

        if (versions.Count == 0)
        {
            Console.WriteLine(
                $"No versions exist for {pack.Name}.");

            return Task.CompletedTask;
        }

        Console.WriteLine(
            $"{pack.Name} versions: {versions.Count}");

        foreach (PackVersion version in versions)
        {
            string heading =
                version.VersionLabel +
                (version.IsPublished
                    ? " [Published]"
                    : string.Empty);

            Console.WriteLine();
            Console.WriteLine(heading);
            Console.WriteLine(
                new string('-', heading.Length));

            Console.WriteLine($"ID:       {version.Id}");
            Console.WriteLine(
                $"Created:  {ConsoleFormatting.FormatDate(version.CreatedAt)}");
            Console.WriteLine(
                $"Notes:    {version.ReleaseNotes ?? "None"}");
            Console.WriteLine(
                $"Manifest: {version.ManifestPath}");
        }

        return Task.CompletedTask;
    }
}