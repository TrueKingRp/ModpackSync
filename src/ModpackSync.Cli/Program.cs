using System.Text.Json;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Core.Manifests;
using ModpackSync.Core.Sync;

if (args.Length < 2)
{
    Console.WriteLine("Commands:");
    Console.WriteLine();
    Console.WriteLine("Create a manifest:");
    Console.WriteLine(
        "ModpackSync.Cli scan <pack name> <folder path>");
    Console.WriteLine();
    Console.WriteLine("Compare two manifests:");
    Console.WriteLine(
        "ModpackSync.Cli compare <old manifest> <new manifest>");
    return;
}

string command = args[0].ToLowerInvariant();

try
{
    switch (command)
    {
        case "scan":
            await RunScanAsync(args);
            break;

        case "compare":
            await RunCompareAsync(args);
            break;

        default:
            Console.Error.WriteLine(
                $"Unknown command: {command}");
            break;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Error: {ex.Message}");
}

static async Task RunScanAsync(string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine(
            "Usage: ModpackSync.Cli scan <pack name> <folder path>");
        return;
    }

    string packName = args[1];
    string folderPath = args[2];

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

    ModpackManifest manifest = await builder.BuildAsync(
        packName,
        folderPath);

    var options = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    string json = JsonSerializer.Serialize(
        manifest,
        options);

    await File.WriteAllTextAsync(
        manifestPath,
        json);

    Console.WriteLine();
    Console.WriteLine($"Files found: {manifest.Files.Count}");
    Console.WriteLine($"Manifest created: {manifestPath}");
}

static async Task RunCompareAsync(string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine(
            "Usage: ModpackSync.Cli compare <old manifest> <new manifest>");
        return;
    }

    string oldManifestPath = args[1];
    string newManifestPath = args[2];

    ModpackManifest oldManifest =
        await LoadManifestAsync(oldManifestPath);

    ModpackManifest newManifest =
        await LoadManifestAsync(newManifestPath);

    var comparer = new ManifestComparer();

    var comparison = comparer.Compare(
        oldManifest,
        newManifest);

    Console.WriteLine();
    Console.WriteLine(
        $"Added:     {comparison.AddedFiles.Count}");
    Console.WriteLine(
        $"Modified:  {comparison.ModifiedFiles.Count}");
    Console.WriteLine(
        $"Deleted:   {comparison.DeletedFiles.Count}");
    Console.WriteLine(
        $"Unchanged: {comparison.UnchangedFiles.Count}");

    PrintFiles("Added files", comparison.AddedFiles);
    PrintFiles("Modified files", comparison.ModifiedFiles);
    PrintFiles("Deleted files", comparison.DeletedFiles);
}

static async Task<ModpackManifest> LoadManifestAsync(
    string manifestPath)
{
    if (!File.Exists(manifestPath))
    {
        throw new FileNotFoundException(
            "Manifest file was not found.",
            manifestPath);
    }

    string json = await File.ReadAllTextAsync(manifestPath);

    ModpackManifest? manifest =
        JsonSerializer.Deserialize<ModpackManifest>(json);

    return manifest
        ?? throw new InvalidDataException(
            $"Could not read manifest: {manifestPath}");
}

static void PrintFiles(
    string heading,
    IReadOnlyList<ManifestFile> files)
{
    if (files.Count == 0)
    {
        return;
    }

    Console.WriteLine();
    Console.WriteLine(heading);
    Console.WriteLine(
        new string('-', heading.Length));

    foreach (ManifestFile file in files)
    {
        Console.WriteLine(file.RelativePath);
    }
}