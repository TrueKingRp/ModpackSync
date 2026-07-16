namespace ModpackSync.Server.Entities;

public sealed class ServerPackVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PackId { get; set; }

    public required string VersionLabel { get; set; }

    public string? ReleaseNotes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public bool IsComplete { get; set; }

    public bool IsPublished { get; set; }

    public ServerPack? Pack { get; set; }

    public List<VersionFile> Files { get; set; } = [];
}