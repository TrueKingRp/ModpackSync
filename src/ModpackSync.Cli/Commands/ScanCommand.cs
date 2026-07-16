using System.Text.Json;
using ModpackSync.Cli.Helpers;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Sync;
using ModpackSync.Core.Manifests;
using ModpackSync.Core.Sync;

namespace ModpackSync.Cli.Commands;

public sealed class ScanCommand : ICommand
{
    public string Name => "scan";

    public string Usage =>
        "scan <pack name> <folder path>";

    public async Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length < 2)
        {
            Console.WriteLine($"Usage: {Usage}");
            return;
        }

        string packName = args[0];
        string folderPath =
            Path.GetFullPath(args[1]);

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(
                $"The modpack folder does not exist: {folderPath}");
        }

        string manifestPath = Path.Combine(
            folderPath,
            "manifest.json");

        string oldManifestPath = Path.Combine(
            folderPath,
            "OLDManifest.json");

        if (File.Exists(oldManifestPath))
        {
            File.Delete(oldManifestPath);

            Console.WriteLine(
                $"Deleted previous old manifest: {oldManifestPath}");
        }

        if (File.Exists(manifestPath))
        {
            File.Move(
                manifestPath,
                oldManifestPath);

            Console.WriteLine(
                $"Renamed existing manifest to: {oldManifestPath}");
        }

        var builder = new ManifestBuilder();

        Console.WriteLine($"Scanning: {folderPath}");

        ModpackManifest newManifest =
            await builder.BuildAsync(
                packName,
                folderPath,
                cancellationToken: cancellationToken);

        await SaveManifestAsync(
            manifestPath,
            newManifest,
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

    private static async Task SaveManifestAsync(
        string manifestPath,
        ModpackManifest manifest,
        CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json =
            JsonSerializer.Serialize(
                manifest,
                options);

        await File.WriteAllTextAsync(
            manifestPath,
            json,
            cancellationToken);
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