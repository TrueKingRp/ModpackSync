using ModpackSync.Cli.Helpers;
using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Versions;
using ModpackSync.Core.Packs;
using ModpackSync.Core.Versions;

namespace ModpackSync.Cli.Commands;

public sealed class VersionCreateCommand : ICommand
{
    private readonly PackManager _packManager;
    private readonly VersionManager _versionManager;

    public VersionCreateCommand(
        PackManager packManager,
        VersionManager versionManager)
    {
        _packManager = packManager;
        _versionManager = versionManager;
    }

    public string Name => "version-create";

    public string Usage =>
        "version-create <pack ID> " +
        "<version label> [release notes]";

    public async Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length < 2 ||
            !Guid.TryParse(
                args[0],
                out Guid packId))
        {
            Console.WriteLine($"Usage: {Usage}");
            return;
        }

        ModpackRegistration? pack =
            _packManager.GetPack(packId);

        if (pack is null)
        {
            Console.WriteLine(
                "No registered pack was found with that ID.");

            return;
        }

        string versionLabel = args[1];

        string? releaseNotes =
            args.Length >= 3
                ? args[2]
                : null;

        Console.WriteLine(
            $"Scanning {pack.Name} before creating the version...");

        await _packManager.ScanPackAsync(
            packId,
            cancellationToken);

        PackVersion version =
            await _versionManager.CreateVersionAsync(
                pack,
                versionLabel,
                releaseNotes,
                cancellationToken);

        Console.WriteLine();
        Console.WriteLine("Version created.");
        Console.WriteLine($"ID:       {version.Id}");
        Console.WriteLine($"Pack:     {pack.Name}");
        Console.WriteLine($"Version:  {version.VersionLabel}");
        Console.WriteLine(
            $"Created:  {ConsoleFormatting.FormatDate(version.CreatedAt)}");
        Console.WriteLine(
            $"Manifest: {version.ManifestPath}");
    }
}