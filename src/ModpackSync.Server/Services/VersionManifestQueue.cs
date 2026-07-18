using System.Threading.Channels;

namespace ModpackSync.Server.Services;

public sealed class VersionManifestQueue :
    IVersionManifestQueue
{
    private readonly Channel<VersionManifestJob> _channel;

    public VersionManifestQueue()
    {
        _channel =
            Channel.CreateBounded<VersionManifestJob>(
                new BoundedChannelOptions(20)
                {
                    FullMode =
                        BoundedChannelFullMode.Wait,

                    SingleReader =
                        true,

                    SingleWriter =
                        false
                });
    }

    public ValueTask QueueAsync(
        VersionManifestJob job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        return _channel.Writer.WriteAsync(
            job,
            cancellationToken);
    }

    public ValueTask<VersionManifestJob> DequeueAsync(
        CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(
            cancellationToken);
    }
}