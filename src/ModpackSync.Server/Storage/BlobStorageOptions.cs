namespace ModpackSync.Server.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName =
        "BlobStorage";

    public string RootPath { get; set; } =
        "storage";

    public long MaximumFileSizeBytes { get; set; } =
        2L * 1024L * 1024L * 1024L;
}