using ModpackSync.Contracts.Packs;
using ModpackSync.Core.Packs;

var packManager = new PackManager();

try
{
    await packManager.InitialiseAsync();

    if (args.Length == 0)
    {
        PrintUsage();
        return;
    }

    string command = args[0].ToLowerInvariant();

    switch (command)
    {
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
            await ScanPackAsync(
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
        args.Length >= 4 ? args[3] : null;

    string? modLoader =
        args.Length >= 5 ? args[4] : null;

    string? modLoaderVersion =
        args.Length >= 6 ? args[5] : null;

    ModpackRegistration pack =
        await manager.AddPackAsync(
            name,
            folderPath,
            minecraftVersion,
            modLoader,
            modLoaderVersion);

    Console.WriteLine("Pack registered.");
    Console.WriteLine($"ID:      {pack.Id}");
    Console.WriteLine($"Name:    {pack.Name}");
    Console.WriteLine($"Path:    {pack.LocalPath}");
    Console.WriteLine(
        $"Config:  {manager.SettingsFilePath}");
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

        Console.WriteLine($"ID:   {pack.Id}");
        Console.WriteLine($"Path: {pack.LocalPath}");

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
        !Guid.TryParse(args[1], out Guid packId))
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

static async Task ScanPackAsync(
    PackManager manager,
    string[] args)
{
    if (args.Length < 2 ||
        !Guid.TryParse(args[1], out Guid packId))
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

    Console.WriteLine(
        $"Scanning {pack.Name}...");

    var manifest =
        await manager.ScanPackAsync(packId);

    Console.WriteLine();
    Console.WriteLine(
        $"Files found: {manifest.Files.Count}");

    Console.WriteLine(
        $"Manifest: {Path.Combine(pack.LocalPath, "manifest.json")}");
}

static string FormatLoader(
    ModpackRegistration pack)
{
    if (string.IsNullOrWhiteSpace(pack.ModLoader))
    {
        return "Not set";
    }

    if (string.IsNullOrWhiteSpace(
            pack.ModLoaderVersion))
    {
        return pack.ModLoader;
    }

    return $"{pack.ModLoader} {pack.ModLoaderVersion}";
}

static string FormatDate(
    DateTimeOffset? date)
{
    return date?.ToLocalTime()
        .ToString("dd MMM yyyy HH:mm")
        ?? "Never";
}

static void PrintUsage()
{
    Console.WriteLine("ModpackSync Pack Manager");
    Console.WriteLine();

    Console.WriteLine("Register a pack:");
    Console.WriteLine(
        "pack-add <name> <folder> " +
        "[Minecraft version] [loader] [loader version]");

    Console.WriteLine();

    Console.WriteLine("List packs:");
    Console.WriteLine("pack-list");

    Console.WriteLine();

    Console.WriteLine("Scan a registered pack:");
    Console.WriteLine("pack-scan <pack ID>");

    Console.WriteLine();

    Console.WriteLine("Remove a registered pack:");
    Console.WriteLine("pack-remove <pack ID>");
}