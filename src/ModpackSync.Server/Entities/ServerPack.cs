namespace ModpackSync.Server.Entities;

public sealed class ServerPack
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public List<ServerPackVersion> Versions { get; set; } = [];
}