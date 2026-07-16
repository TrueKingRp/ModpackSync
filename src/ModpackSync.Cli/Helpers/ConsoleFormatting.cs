using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Sync;
using ModpackSync.Core.Sync;

namespace ModpackSync.Cli.Helpers;

public static class ConsoleFormatting
{
    public static string FormatLoader(
        ModpackRegistration pack)
    {
        if (string.IsNullOrWhiteSpace(pack.ModLoader))
        {
            return "Not set";
        }

        if (string.IsNullOrWhiteSpace(pack.ModLoaderVersion))
        {
            return pack.ModLoader;
        }

        return $"{pack.ModLoader} {pack.ModLoaderVersion}";
    }

    public static string FormatDate(
        DateTimeOffset? date)
    {
        return date?
            .ToLocalTime()
            .ToString("dd MMM yyyy HH:mm")
            ?? "Never";
    }

    public static void PrintComparison(
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

    public static void PrintCurrentCategoryTotals(
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

    private static void PrintCategory(
        string categoryName,
        CategoryComparison comparison)
    {
        Console.WriteLine();
        Console.WriteLine(categoryName);
        Console.WriteLine(
            new string('-', categoryName.Length));

        Console.WriteLine($"Total:     {comparison.Total}");
        Console.WriteLine($"Added:     {comparison.Added}");
        Console.WriteLine($"Modified:  {comparison.Modified}");
        Console.WriteLine($"Deleted:   {comparison.Deleted}");
        Console.WriteLine($"Unchanged: {comparison.Unchanged}");
    }

    private static void PrintChangedFiles(
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
}