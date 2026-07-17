namespace ModpackSync.Server.Services;

public sealed class VersionArchiveResult :
    IAsyncDisposable
{
    public required FileStream Stream { get; init; }

    public required string FileName { get; init; }

    public required string TemporaryFilePath { get; init; }

    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();

        try
        {
            if (File.Exists(
                    TemporaryFilePath))
            {
                File.Delete(
                    TemporaryFilePath);
            }
        }
        catch
        {
            // The operating system can clean up any remaining
            // temporary archive later.
        }
    }
}