using System.Text.Json;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Sync;
using ModpackSync.Core.Manifests;
using ModpackSync.Core.Packs;
using ModpackSync.Core.Sync;

var packManager = new PackManager();

try
{
    await packManager.InitialiseAsync();

    if (args.Length < 1)
    {
        PrintUsage();
        return;
    }

    string command = args[0].ToLowerInvariant();

    switch (command)
    {
        case "scan":
            await RunScanAsync(args);
            break;

        case "pack-add":
            await AddPackAsync(
                packManager,
                args);
            break;

        case "pack-list":
            ListPacks(packManager);
            break;

        case "pack-remove":
            await RemovePackAsync(
                packManager,
                args);
            break;

        case "pack-scan":
            await ScanRegisteredPackAsync(
                packManager,
                args);
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
            "Usage: scan <pack name> <folder path>");

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

    await SaveManifestAsync(
        manifestPath,
        newManifest);

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

static async Task AddPackAsync(
    PackManager manager,
    string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine(
            "Usage: pack-add <name> <folder path> " +
            "[Minecraft version] [loader] [loader version]");

        return;
    }

    string name = args[1];
    string folderPath = args[2];

    string? minecraftVersion =
        args.Length >= 4
            ? args[3]
            : null;

    string? modLoader =
        args.Length >= 5
            ? args[4]
            : null;

    string? modLoaderVersion =
        args.Length >= 6
            ? args[5]
            : null;

    ModpackRegistration pack =
        await manager.AddPackAsync(
            name,
            folderPath,
            minecraftVersion,
            modLoader,
            modLoaderVersion);

    Console.WriteLine();
    Console.WriteLine("Pack registered.");
    Console.WriteLine($"ID:        {pack.Id}");
    Console.WriteLine($"Name:      {pack.Name}");
    Console.WriteLine($"Path:      {pack.LocalPath}");
    Console.WriteLine(
        $"Minecraft: {pack.MinecraftVersion ?? "Not set"}");
    Console.WriteLine(
        $"Loader:    {FormatLoader(pack)}");
    Console.WriteLine(
        $"Registry:  {manager.SettingsFilePath}");
}

static void ListPacks(
    PackManager manager)
{
    IReadOnlyList<ModpackRegistration> packs =
        manager.Packs;

    if (packs.Count == 0)
    {
        Console.WriteLine(
            "No modpacks are currently registered.");

        return;
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
            $"Loader:    {FormatLoader(pack)}");

        Console.WriteLine(
            $"Scanned:   {FormatDate(pack.LastScannedAt)}");
    }
}

static async Task RemovePackAsync(
    PackManager manager,
    string[] args)
{
    if (args.Length < 2 ||
        !Guid.TryParse(
            args[1],
            out Guid packId))
    {
        Console.WriteLine(
            "Usage: pack-remove <pack ID>");

        return;
    }

    bool removed =
        await manager.RemovePackAsync(packId);

    Console.WriteLine(
        removed
            ? "Pack removed from the local registry."
            : "No registered pack was found with that ID.");
}

static async Task ScanRegisteredPackAsync(
    PackManager manager,
    string[] args)
{
    if (args.Length < 2 ||
        !Guid.TryParse(
            args[1],
            out Guid packId))
    {
        Console.WriteLine(
            "Usage: pack-scan <pack ID>");

        return;
    }

    ModpackRegistration? pack =
        manager.GetPack(packId);

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
        await manager.ScanPackAsync(packId);

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

static async Task SaveManifestAsync(
    string manifestPath,
    ModpackManifest manifest)
{
    var jsonOptions =
        new JsonSerializerOptions
        {
            WriteIndented = true
        };

    string json =
        JsonSerializer.Serialize(
            manifest,
            jsonOptions);

    await File.WriteAllTextAsync(
        manifestPath,
        json);
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
        await File.ReadAllTextAsync(
            manifestPath);

    ModpackManifest? manifest =
        JsonSerializer.Deserialize<ModpackManifest>(
            json);

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
        Console.WriteLine(
            file.RelativePath);
    }
}

static void PrintCurrentCategoryTotals(
    ModpackManifest manifest)
{
    int mods =
        manifest.Files.Count(file =>
            FileCategoryClassifier.GetCategory(
                file.RelativePath) ==
            FileCategory.Mod);

    int blockbusterModels =
        manifest.Files.Count(file =>
            FileCategoryClassifier.GetCategory(
                file.RelativePath) ==
            FileCategory.BlockbusterModel);

    int chameleonModels =
        manifest.Files.Count(file =>
            FileCategoryClassifier.GetCategory(
                file.RelativePath) ==
            FileCategory.ChameleonModel);

    Console.WriteLine();
    Console.WriteLine("Current pack metrics");
    Console.WriteLine("--------------------");
    Console.WriteLine(
        $"Mods:               {mods}");
    Console.WriteLine(
        $"Blockbuster models: {blockbusterModels}");
    Console.WriteLine(
        $"Chameleon models:   {chameleonModels}");
}

static string FormatLoader(
    ModpackRegistration pack)
{
    if (string.IsNullOrWhiteSpace(
            pack.ModLoader))
    {
        return "Not set";
    }

    if (string.IsNullOrWhiteSpace(
            pack.ModLoaderVersion))
    {
        return pack.ModLoader;
    }

    return
        $"{pack.ModLoader} {pack.ModLoaderVersion}";
}

static string FormatDate(
    DateTimeOffset? date)
{
    return date?
        .ToLocalTime()
        .ToString("dd MMM yyyy HH:mm")
        ?? "Never";
}

static void PrintUsage()
{
    Console.WriteLine(
        "ModpackSync CLI");

    Console.WriteLine();

    Console.WriteLine(
        "Direct folder scan:");

    Console.WriteLine(
        "scan <pack name> <folder path>");

    Console.WriteLine();

    Console.WriteLine(
        "Register a pack:");

    Console.WriteLine(
        "pack-add <name> <folder path> " +
        "[Minecraft version] [loader] [loader version]");

    Console.WriteLine();

    Console.WriteLine(
        "List registered packs:");

    Console.WriteLine(
        "pack-list");

    Console.WriteLine();

    Console.WriteLine(
        "Scan a registered pack:");

    Console.WriteLine(
        "pack-scan <pack ID>");

    Console.WriteLine();

    Console.WriteLine(
        "Remove a registered pack:");

    Console.WriteLine(
        "pack-remove <pack ID>");
}