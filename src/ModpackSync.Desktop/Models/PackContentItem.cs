namespace ModpackSync.Desktop.Models;

public sealed class PackContentItem
{
    public bool IsIncluded { get; set; }

    public string Name { get; init; } =
        string.Empty;

    public string RelativePath { get; init; } =
        string.Empty;

    public string EntryType { get; init; } =
        string.Empty;

    public int FileCount { get; init; }

    public long Size { get; init; }

    public string FormattedSize =>
        FormatSize(Size);

    private static string FormatSize(
        long byteCount)
    {
        string[] units =
        [
            "B",
            "KB",
            "MB",
            "GB",
            "TB"
        ];

        double size =
            byteCount;

        int unit =
            0;

        while (size >= 1024 &&
               unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}