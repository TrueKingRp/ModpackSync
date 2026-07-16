using System.Text.Json;
using ModpackSync.Core.Manifests;

if (args.Length < 2)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("ModpackSync.Cli <pack name> <folder path>");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("ModpackSync.Cli \"North Academy\" \"F:\\Minecraft\\NorthAcademy\"");
    return;
}

string packName = args[0];
string folderPath = args[1];

try
{
    var builder = new ManifestBuilder();

    Console.WriteLine($"Scanning: {folderPath}");

    var manifest = await builder.BuildAsync(
        packName,
        folderPath);

    string outputPath = Path.Combine(
        folderPath,
        "manifest.json");

    var options = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    string json = JsonSerializer.Serialize(
        manifest,
        options);

    await File.WriteAllTextAsync(
        outputPath,
        json);

    Console.WriteLine();
    Console.WriteLine($"Files found: {manifest.Files.Count}");
    Console.WriteLine($"Manifest created: {outputPath}");
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Error: {ex.Message}");
}