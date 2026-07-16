using System.Text.Json;
using ModpackSync.Cli.Helpers;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Sync;
using ModpackSync.Core.Packs;
using ModpackSync.Core.Sync;

namespace ModpackSync.Cli.Commands;

public sealed class PackScanCommand : ICommand
{
    private readonly PackManager _packManager;

    public PackScanCommand(
        PackManager packManager)
    {
        _packManager = packManager;
    }

    public string Name => "pack-scan";

    public string Usage =>
        "pack-scan <pack ID>";

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

        ModpackRegistration? pack =
            _packManager.GetPack(packId);

        if (pack is null)
        {
            Console.WriteLine(
                "No registered pack was found with that ID.");

            return;
        }

        string manifestPath = Path.Combine(
            pack.LocalPath,
            "manifest.json");

        string oldManifestPath = Path.Combine(
            pack.LocalPath,
            "OLDManifest.json");

        Console.WriteLine(
            $"Scanning registered pack: {pack.Name}");

        ModpackManifest newManifest =
            await _packManager.ScanPackAsync(
                packId,
                cancellationToken);

        Console.WriteLine();
        Console.WriteLine(
            $"Files found: {newManifest.Files.Count}");

        Console.WriteLine(
            $"Manifest created: {manifestPath}");

        if (!File.Exists(oldManifestPath))
        {
            Console.WriteLine();
            Console.WriteLine(
                "No previous manifest was available for comparison.");

            ConsoleFormatting.PrintCurrentCategoryTotals(
                newManifest);

            return;
        }

        ModpackManifest oldManifest =
            await LoadManifestAsync(
                oldManifestPath,
                cancellationToken);

        var comparer = new ManifestComparer();

        ManifestComparison comparison =
            comparer.Compare(
                oldManifest,
                newManifest);

        ConsoleFormatting.PrintComparison(
            comparison);
    }

    private static async Task<ModpackManifest> LoadManifestAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        string json =
            await File.ReadAllTextAsync(
                manifestPath,
                cancellationToken);

        ModpackManifest? manifest =
            JsonSerializer.Deserialize<ModpackManifest>(
                json);

        return manifest
            ?? throw new InvalidDataException(
                $"Could not read manifest: {manifestPath}");
    }
}