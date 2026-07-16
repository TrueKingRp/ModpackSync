using System.Text.Json;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Sync;
using ModpackSync.Core.Manifests;
using ModpackSync.Core.Sync;

if (args.Length < 1)
{
    PrintUsage();
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

        default:
            Console.Error.WriteLine(
                $"Unknown command: {command}");

            Console.WriteLine();

            PrintUsage();
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
    string folderPath = Path.GetFullPath(args[2]);

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
            folderPath);

    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    string manifestJson = JsonSerializer.Serialize(
        newManifest,
        jsonOptions);

    await File.WriteAllTextAsync(
        manifestPath,
        manifestJson);

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

        PrintCurrentCategoryTotals(newManifest);
        return;
    }

    ModpackManifest oldManifest =
        await LoadManifestAsync(oldManifestPath);

    var comparer = new ManifestComparer();

    ManifestComparison comparison =
        comparer.Compare(
            oldManifest,
            newManifest);

    PrintComparison(comparison);
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

    string json =
        await File.ReadAllTextAsync(manifestPath);

    ModpackManifest? manifest =
        JsonSerializer.Deserialize<ModpackManifest>(json);

    return manifest
        ?? throw new InvalidDataException(
            $"Could not read manifest: {manifestPath}");
}

static void PrintComparison(
    ManifestComparison comparison)
{
    Console.WriteLine();
    Console.WriteLine("Changes since previous scan");
    Console.WriteLine("---------------------------");

    Console.WriteLine(
        $"Added:     {comparison.AddedFiles.Count}");

    Console.WriteLine(
        $"Modified:  {comparison.ModifiedFiles.Count}");

    Console.WriteLine(
        $"Deleted:   {comparison.DeletedFiles.Count}");

    Console.WriteLine(
        $"Unchanged: {comparison.UnchangedFiles.Count}");

    PrintCategory(
        "Mods",
        comparison.Mods);

    PrintCategory(
        "Blockbuster models",
        comparison.BlockbusterModels);

    PrintCategory(
        "Chameleon models",
        comparison.ChameleonModels);

    PrintChangedFiles(
        "Added files",
        comparison.AddedFiles);

    PrintChangedFiles(
        "Modified files",
        comparison.ModifiedFiles);

    PrintChangedFiles(
        "Deleted files",
        comparison.DeletedFiles);
}

static void PrintCategory(
    string categoryName,
    CategoryComparison comparison)
{
    Console.WriteLine();
    Console.WriteLine(categoryName);
    Console.WriteLine(
        new string('-', categoryName.Length));

    Console.WriteLine(
        $"Total:     {comparison.Total}");

    Console.WriteLine(
        $"Added:     {comparison.Added}");

    Console.WriteLine(
        $"Modified:  {comparison.Modified}");

    Console.WriteLine(
        $"Deleted:   {comparison.Deleted}");

    Console.WriteLine(
        $"Unchanged: {comparison.Unchanged}");
}

static void PrintChangedFiles(
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

static void PrintCurrentCategoryTotals(
    ModpackManifest manifest)
{
    int mods = manifest.Files.Count(file =>
        FileCategoryClassifier.GetCategory(
            file.RelativePath) == FileCategory.Mod);

    int blockbusterModels = manifest.Files.Count(file =>
        FileCategoryClassifier.GetCategory(
            file.RelativePath) == FileCategory.BlockbusterModel);

    int chameleonModels = manifest.Files.Count(file =>
        FileCategoryClassifier.GetCategory(
            file.RelativePath) == FileCategory.ChameleonModel);

    Console.WriteLine();
    Console.WriteLine("Current pack metrics");
    Console.WriteLine("--------------------");
    Console.WriteLine($"Mods:               {mods}");
    Console.WriteLine($"Blockbuster models: {blockbusterModels}");
    Console.WriteLine($"Chameleon models:   {chameleonModels}");
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine();

    Console.WriteLine(
        "ModpackSync.Cli scan <pack name> <folder path>");

    Console.WriteLine();

    Console.WriteLine("Example:");

    Console.WriteLine(
        "ModpackSync.Cli scan \"Tensura Slime\" " +
        "\"F:\\PrismLauncher\\instances\\Tensura (Slime)\\minecraft\"");
}